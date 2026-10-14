using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Serves the local control page and WebSocket requests without blocking Unity's main thread.</summary>
public sealed class LocalControlServer : MonoBehaviour
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaxHeaderBytes = 8192;
    private const int MaxMessageBytes = 2048;

    [SerializeField, Min(1)] private int port = 2468;
    [SerializeField] private TowerShop shop;
    [SerializeField] private ArmyEconomy economy;

    private readonly ConcurrentQueue<PendingMessage> pendingMessages = new ConcurrentQueue<PendingMessage>();
    private readonly ConcurrentQueue<string> backgroundErrors = new ConcurrentQueue<string>();
    private readonly ConcurrentDictionary<int, TcpClient> clients = new ConcurrentDictionary<int, TcpClient>();
    private readonly ConcurrentDictionary<int, WebSocketClient> webSocketClients =
        new ConcurrentDictionary<int, WebSocketClient>();
    private TcpListener listener;
    private CancellationTokenSource shutdown;
    private string pagePath;
    private string status = "Servidor detenido";
    private string accessUrl = "IP LAN no detectada";
    private string serializedShopState;
    private int nextClientId;
    private int observedShopRevision = -1;
    private int observedEconomyRevision = -1;
    private int outgoingStateRevision;

    [Serializable]
    private sealed class PurchaseCommand
    {
#pragma warning disable 0649 // Unity's JsonUtility fills these fields from the incoming JSON.
        public string type;
        public string army;
        public string requestId;
#pragma warning restore 0649
    }

    [Serializable]
    private sealed class PurchaseResponse
    {
        public string type = "purchaseResult";
        public string requestId;
        public bool success;
        public string message;
    }

    [Serializable]
    private sealed class ShopStateMessage
    {
        public string type = "shopState";
        public int revision;
        public ArmyShopState[] armies;
    }

    [Serializable]
    private sealed class ArmyShopState
    {
        public string army;
        public bool owned;
        public int upgradeLevel;
        public float currentDamage;
        public int nextCost;
        public float upgradeDamagePercent;
        public bool canUpgrade;
        public int coins;
        public float passiveIncomePerSecond;
    }

    private sealed class WebSocketClient
    {
        public readonly WebSocket Socket;
        public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);

        public WebSocketClient(WebSocket socket) => Socket = socket;
    }

    private sealed class PendingMessage
    {
        public string Json;
        public TaskCompletionSource<string> Response;
    }

    private void Start() => StartServer();

    private void Update()
    {
        while (backgroundErrors.TryDequeue(out string error))
            status = error;

        while (pendingMessages.TryDequeue(out PendingMessage pending))
            ProcessPurchase(pending);

        int economyRevision = economy != null ? economy.StateRevision : -1;
        if (shop != null && (shop.StateRevision != observedShopRevision || economyRevision != observedEconomyRevision))
        {
            observedShopRevision = shop.StateRevision;
            observedEconomyRevision = economyRevision;
            outgoingStateRevision++;
            Volatile.Write(ref serializedShopState, BuildShopStateJson());
            BroadcastShopState();
        }
    }

    private void StartServer()
    {
        if (shop == null)
            shop = FindAnyObjectByType<TowerShop>();
        if (economy == null)
            economy = FindAnyObjectByType<ArmyEconomy>();

        if (shop == null)
        {
            status = "Error: no se encontró Tower Shop";
            Debug.LogError(status, this);
            return;
        }

        pagePath = Path.Combine(Application.streamingAssetsPath, "remote-control.html");
        if (!File.Exists(pagePath))
        {
            status = "Error: falta Assets/StreamingAssets/remote-control.html";
            Debug.LogError(status, this);
            return;
        }

        try
        {
            string lanAddress = FindLanAddress();
            if (string.IsNullOrEmpty(lanAddress))
            {
                status = "No se detectó una dirección IPv4 LAN activa.";
                Debug.LogError(status, this);
                return;
            }

            shutdown = new CancellationTokenSource();
            observedShopRevision = shop.StateRevision;
            observedEconomyRevision = economy != null ? economy.StateRevision : -1;
            outgoingStateRevision = 0;
            serializedShopState = BuildShopStateJson();
            listener = new TcpListener(IPAddress.Parse(lanAddress), port);
            listener.Start();
            accessUrl = "http://" + lanAddress + ":" + port + "/";
            status = "Servidor activo";
            TcpListener activeListener = listener;
            CancellationToken token = shutdown.Token;
            _ = Task.Run(() => AcceptClientsAsync(activeListener, token));
            Debug.Log("Control remoto disponible en " + accessUrl, this);
        }
        catch (Exception exception)
        {
            status = "No se pudo iniciar el servidor: " + exception.Message;
            Debug.LogError(status, this);
            StopServer();
        }
    }

    private async Task AcceptClientsAsync(TcpListener activeListener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await activeListener.AcceptTcpClientAsync().ConfigureAwait(false);
                int clientId = Interlocked.Increment(ref nextClientId);
                clients.TryAdd(clientId, client);
                _ = Task.Run(() => HandleClientAsync(clientId, client, token));
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected when Unity stops the listener.
        }
        catch (SocketException) when (token.IsCancellationRequested)
        {
            // Expected when Unity stops the listener.
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                backgroundErrors.Enqueue("Servidor detenido: " + exception.Message);
        }
    }

    private async Task HandleClientAsync(int clientId, TcpClient client, CancellationToken token)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                string requestHeaders = await ReadHeadersAsync(stream, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(requestHeaders))
                    return;

                string[] lines = requestHeaders.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2 || !string.Equals(requestLine[0], "GET", StringComparison.Ordinal))
                {
                    await WriteHttpResponseAsync(stream, 405, "Method Not Allowed", "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes("Only GET is supported."), token).ConfigureAwait(false);
                    return;
                }

                string path = requestLine[1].Split('?')[0];
                Dictionary<string, string> headers = ParseHeaders(lines);
                if (path == "/ws" && IsWebSocketUpgrade(headers))
                {
                    if (!headers.TryGetValue("Sec-WebSocket-Key", out string key))
                    {
                        await WriteHttpResponseAsync(stream, 400, "Bad Request", "text/plain; charset=utf-8",
                            Encoding.UTF8.GetBytes("Missing WebSocket key."), token).ConfigureAwait(false);
                        return;
                    }

                    await AcceptWebSocketAsync(stream, key, token).ConfigureAwait(false);
                    using (WebSocket socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30)))
                    {
                        WebSocketClient webSocketClient = new WebSocketClient(socket);
                        webSocketClients.TryAdd(clientId, webSocketClient);
                        await SendShopStateAsync(webSocketClient, token).ConfigureAwait(false);
                        await HandleWebSocketAsync(clientId, webSocketClient, token).ConfigureAwait(false);
                    }
                    return;
                }

                if (path == "/" || path == "/index.html")
                {
                    byte[] page = await File.ReadAllBytesAsync(pagePath, token).ConfigureAwait(false);
                    await WriteHttpResponseAsync(stream, 200, "OK", "text/html; charset=utf-8", page, token)
                        .ConfigureAwait(false);
                    return;
                }

                await WriteHttpResponseAsync(stream, 404, "Not Found", "text/plain; charset=utf-8",
                    Encoding.UTF8.GetBytes("Not found."), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when Unity shuts down.
        }
        catch (IOException)
        {
            // A browser closing its tab or losing Wi-Fi is normal.
        }
        catch (ObjectDisposedException)
        {
            // Expected when Unity closes connected clients.
        }
        catch (SocketException)
        {
            // A dropped local network connection should not affect the match.
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                backgroundErrors.Enqueue("Conexión remota cerrada: " + exception.Message);
        }
        finally
        {
            clients.TryRemove(clientId, out _);
            webSocketClients.TryRemove(clientId, out _);
        }
    }

    private async Task HandleWebSocketAsync(int clientId, WebSocketClient client, CancellationToken token)
    {
        byte[] buffer = new byte[1024];
        WebSocket socket = client.Socket;
        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using (MemoryStream message = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", token)
                                .ConfigureAwait(false);
                        return;
                    }
                    if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > MaxMessageBytes)
                    {
                        await SendMessageAsync(client, new PurchaseResponse
                        {
                            success = false,
                            message = "Mensaje no válido o demasiado grande."
                        }, token).ConfigureAwait(false);
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (message.Length == 0)
                    continue;

                PendingMessage pending = new PendingMessage
                {
                    Json = Encoding.UTF8.GetString(message.ToArray()),
                    Response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
                };
                pendingMessages.Enqueue(pending);

                Task cancelled = Task.Delay(Timeout.Infinite, token);
                Task completed = await Task.WhenAny(pending.Response.Task, cancelled).ConfigureAwait(false);
                if (completed != pending.Response.Task)
                    return;

                await SendMessageAsync(client, await pending.Response.Task.ConfigureAwait(false), token)
                    .ConfigureAwait(false);
            }
        }
        webSocketClients.TryRemove(clientId, out _);
    }

    private void ProcessPurchase(PendingMessage pending)
    {
        PurchaseResponse response = new PurchaseResponse();
        try
        {
            PurchaseCommand command = JsonUtility.FromJson<PurchaseCommand>(pending.Json);
            response.requestId = command != null ? command.requestId : null;
            if (command == null || command.type != "buyTower" || string.IsNullOrEmpty(command.requestId) ||
                command.requestId.Length > 64)
            {
                response.message = "Solicitud inválida.";
            }
            else if (!TryParseArmy(command.army, out ArmyType army))
            {
                response.requestId = command.requestId;
                response.message = "Ejército desconocido.";
            }
            else
            {
                response.requestId = command.requestId;
                bool wasOwned = shop.HasTower(army);
                response.success = shop.TryPurchase(army, out string failureReason);
                response.message = response.success
                    ? wasOwned
                        ? "Torre de " + shop.GetTowerDisplayName(army) + " mejorada: " +
                          shop.GetUpgradeDamageLabel(army) + " daño."
                        : "Torre de " + shop.GetTowerDisplayName(army) + " comprada."
                    : failureReason;
            }
        }
        catch (Exception)
        {
            response.success = false;
            response.message = "No se pudo interpretar la solicitud.";
        }

        pending.Response.TrySetResult(JsonUtility.ToJson(response));
    }

    private static bool TryParseArmy(string value, out ArmyType army)
    {
        if (string.Equals(value, "tierra", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "green", StringComparison.OrdinalIgnoreCase))
        {
            army = ArmyType.Tierra;
            return true;
        }
        if (string.Equals(value, "aire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "red", StringComparison.OrdinalIgnoreCase))
        {
            army = ArmyType.Aire;
            return true;
        }
        if (string.Equals(value, "mar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "blue", StringComparison.OrdinalIgnoreCase))
        {
            army = ArmyType.Mar;
            return true;
        }

        army = default;
        return false;
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
    {
        List<byte> bytes = new List<byte>(512);
        byte[] singleByte = new byte[1];
        while (bytes.Count < MaxHeaderBytes)
        {
            int read = await stream.ReadAsync(singleByte, 0, 1, token).ConfigureAwait(false);
            if (read == 0)
                return null;
            bytes.Add(singleByte[0]);
            int count = bytes.Count;
            if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 &&
                bytes[count - 2] == 13 && bytes[count - 1] == 10)
                return Encoding.ASCII.GetString(bytes.ToArray());
        }
        return null;
    }

    private static Dictionary<string, string> ParseHeaders(string[] lines)
    {
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            int separator = lines[i].IndexOf(':');
            if (separator > 0)
                headers[lines[i].Substring(0, separator).Trim()] = lines[i].Substring(separator + 1).Trim();
        }
        return headers;
    }

    private static bool IsWebSocketUpgrade(Dictionary<string, string> headers)
    {
        return headers.TryGetValue("Upgrade", out string upgrade) &&
               string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase) &&
               headers.TryGetValue("Connection", out string connection) &&
               connection.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static async Task AcceptWebSocketAsync(NetworkStream stream, string key, CancellationToken token)
    {
        byte[] hash;
        using (SHA1 sha1 = SHA1.Create())
            hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key.Trim() + WebSocketGuid));
        string accept = Convert.ToBase64String(hash);
        string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                          "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
        byte[] bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task WriteHttpResponseAsync(NetworkStream stream, int code, string reason,
        string contentType, byte[] body, CancellationToken token)
    {
        string headers = "HTTP/1.1 " + code + " " + reason + "\r\n" +
                         "Content-Type: " + contentType + "\r\n" +
                         "Content-Length: " + body.Length + "\r\n" +
                         "Cache-Control: no-store\r\n" +
                         "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
        await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private void BroadcastShopState()
    {
        if (shutdown == null)
            return;

        CancellationToken token = shutdown.Token;
        foreach (KeyValuePair<int, WebSocketClient> pair in webSocketClients)
            _ = SendShopStateAsync(pair.Value, token);
    }

    private Task SendShopStateAsync(WebSocketClient client, CancellationToken token)
    {
        string state = Volatile.Read(ref serializedShopState);
        return string.IsNullOrEmpty(state) ? Task.CompletedTask : SendMessageAsync(client, state, token);
    }

    private string BuildShopStateJson()
    {
        ShopStateMessage state = new ShopStateMessage
        {
            revision = outgoingStateRevision,
            armies = new[]
            {
                CreateArmyShopState(ArmyType.Tierra, "tierra"),
                CreateArmyShopState(ArmyType.Aire, "aire"),
                CreateArmyShopState(ArmyType.Mar, "mar")
            }
        };
        return JsonUtility.ToJson(state);
    }

    private ArmyShopState CreateArmyShopState(ArmyType army, string name)
    {
        return new ArmyShopState
        {
            army = name,
            owned = shop.HasTower(army),
            upgradeLevel = shop.GetUpgradeLevel(army),
            currentDamage = shop.GetCurrentDamage(army),
            nextCost = shop.GetNextCost(army),
            upgradeDamagePercent = shop.GetUpgradeDamagePercent(army) * 100f,
            canUpgrade = shop.CanUpgrade(army),
            coins = economy != null ? economy.GetCoins(army) : 0,
            passiveIncomePerSecond = economy != null ? economy.GetPassiveIncomePerSecond(army) : 0f
        };
    }

    private static Task SendMessageAsync(WebSocketClient client, object message, CancellationToken token)
    {
        return SendMessageAsync(client, JsonUtility.ToJson(message), token);
    }

    private static async Task SendMessageAsync(WebSocketClient client, string message, CancellationToken token)
    {
        bool lockTaken = false;
        try
        {
            await client.SendLock.WaitAsync(token).ConfigureAwait(false);
            lockTaken = true;
            if (client.Socket.State != WebSocketState.Open)
                return;

            byte[] data = Encoding.UTF8.GetBytes(message);
            await client.Socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when Unity shuts the server down.
        }
        catch (WebSocketException)
        {
            // A disconnected browser must not affect the match.
        }
        catch (ObjectDisposedException)
        {
            // Expected when Unity closes a client during shutdown.
        }
        finally
        {
            if (lockTaken)
                client.SendLock.Release();
        }
    }

    private static string FindLanAddress()
    {
        try
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            Array.Sort(adapters, (left, right) => GetAdapterPriority(left).CompareTo(GetAdapterPriority(right)));
            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    IPAddress ip = address.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip))
                        continue;
                    byte[] octets = ip.GetAddressBytes();
                    if (octets[0] == 169 && octets[1] == 254)
                        continue;
                    bool isPrivateLanAddress = octets[0] == 10 ||
                        (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                        (octets[0] == 192 && octets[1] == 168);
                    if (!isPrivateLanAddress)
                        continue;
                    return ip.ToString();
                }
            }
        }
        catch (NetworkInformationException)
        {
            // No LAN address means there is no useful address to display or bind.
        }
        catch (Exception)
        {
            // No LAN address means there is no useful address to display or bind.
        }
        return null;
    }

    private static int GetAdapterPriority(NetworkInterface adapter)
    {
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            return 0;
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            return 1;
        return 2;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(18f, 350f, 700f, 52f), GUI.skin.box);
        GUILayout.Label("Control remoto: " + status);
        GUILayout.Label("Abre desde el móvil: " + accessUrl);
        GUILayout.EndArea();
    }

    private void OnApplicationQuit() => StopServer();

    private void OnDestroy() => StopServer();

    private void StopServer()
    {
        if (shutdown != null && !shutdown.IsCancellationRequested)
            shutdown.Cancel();
        if (listener != null)
        {
            listener.Stop();
            listener = null;
        }
        foreach (KeyValuePair<int, TcpClient> pair in clients)
        {
            pair.Value.Close();
            clients.TryRemove(pair.Key, out _);
        }
        if (shutdown != null)
        {
            shutdown.Dispose();
            shutdown = null;
        }
    }
}
