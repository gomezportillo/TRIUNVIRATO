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
    [SerializeField] private TriunviratoGame game;
    [SerializeField, Min(5f)] private float reconnectGraceSeconds = 60f;

    private readonly ConcurrentQueue<PendingMessage> pendingMessages = new ConcurrentQueue<PendingMessage>();
    private readonly ConcurrentQueue<int> disconnectedClients = new ConcurrentQueue<int>();
    private readonly ConcurrentQueue<string> backgroundErrors = new ConcurrentQueue<string>();
    private readonly ConcurrentDictionary<int, TcpClient> clients = new ConcurrentDictionary<int, TcpClient>();
    private readonly ConcurrentDictionary<int, WebSocketClient> webSocketClients =
        new ConcurrentDictionary<int, WebSocketClient>();
    private readonly Dictionary<int, PlayerSession> sessionsByClient = new Dictionary<int, PlayerSession>();
    private readonly Dictionary<string, PlayerSession> sessionsByToken = new Dictionary<string, PlayerSession>();
    private readonly string[] armySeats = new string[3];
    private string developerToken;
    private TcpListener listener;
    private CancellationTokenSource shutdown;
    private string pagePath;
    private string status = "Servidor detenido";
    private string accessUrl = "IP LAN no detectada";
    private int nextClientId;
    private int observedShopRevision = -1;
    private int observedEconomyRevision = -1;
    private int observedGameRevision = -1;
    private int outgoingStateRevision;

    public bool IsDeveloperMode => developerToken != null;
    public string StatusText => status;
    public string AccessUrlText => accessUrl;
    public string RoomStatusText => game != null ? "Sala: " + game.Status + " · " + SeatSummary() : "Sala: no disponible";

    [Serializable]
    private sealed class ClientCommand
    {
#pragma warning disable 0649 // Unity's JsonUtility fills these fields from the incoming JSON.
        public string type;
        public string army;
        public string choice;
        public string clientToken;
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
        public string selectedArmy;
        public bool developerMode;
        public string status;
    }

    [Serializable]
    private sealed class ShopStateMessage
    {
        public string type = "shopState";
        public int revision;
        public string status;
        public string selectedArmy;
        public bool developerMode;
        public bool developerAvailable;
        public int currentWave;
        public int nextEarlyCallAvailableWave;
        public bool waveInProgress;
        public bool earlyWaveCallPending;
        public bool canCallNextWaveEarly;
        public bool purchasePhase;
        public float purchaseSecondsRemaining;
        public int resourceChoiceReward;
        public SeatState[] seats;
        public ArmyShopState[] armies;
    }

    [Serializable]
    private sealed class SeatState
    {
        public string army;
        public string status;
        public bool connected;
        public bool selectedByMe;
    }

    [Serializable]
    private sealed class ArmyShopState
    {
        public string army;
        public bool owned;
        public float currentDamage;
        public float towerRange;
        public float towerAttackCooldown;
        public bool canControl;
        public bool canPurchaseTower;
        public int coins;
        public float passiveIncomePerSecond;
        public string incomeSummary;
        public float heroDamage;
        public float heroBaseDamage;
        public float heroRange;
        public float heroAttackCooldown;
        public float heroSplashRadius;
        public bool purchaseChoiceMade;
        public string purchaseResult;
        public PurchaseUpgradeOption[] purchaseOptions;
    }

    private sealed class WebSocketClient
    {
        public readonly WebSocket Socket;
        public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);

        public WebSocketClient(WebSocket socket) => Socket = socket;
    }

    private sealed class PendingMessage
    {
        public int ClientId;
        public string Json;
        public TaskCompletionSource<string> Response;
    }

    private sealed class PlayerSession
    {
        public string Token;
        public int ArmyIndex = -1;
        public bool Developer;
        public bool Connected;
        public int ClientId = -1;
        public float DisconnectedAt;
    }

    private void Start() => StartServer();

    private void Update()
    {
        while (backgroundErrors.TryDequeue(out string error))
            status = error;

        while (disconnectedClients.TryDequeue(out int clientId))
            ProcessDisconnect(clientId);
        ExpireDisconnectedLobbySeats();
        while (pendingMessages.TryDequeue(out PendingMessage pending))
            ProcessMessage(pending);

        int economyRevision = economy != null ? economy.StateRevision : -1;
        int gameRevision = game != null ? game.StateRevision : -1;
        if (shop != null && (shop.StateRevision != observedShopRevision || economyRevision != observedEconomyRevision ||
                             gameRevision != observedGameRevision))
        {
            observedShopRevision = shop.StateRevision;
            observedEconomyRevision = economyRevision;
            observedGameRevision = gameRevision;
            PublishState();
        }
    }

    private void StartServer()
    {
        if (shop == null)
            shop = FindAnyObjectByType<TowerShop>();
        if (economy == null)
            economy = FindAnyObjectByType<ArmyEconomy>();
        if (game == null)
            game = FindAnyObjectByType<TriunviratoGame>();

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
            observedGameRevision = game != null ? game.StateRevision : -1;
            outgoingStateRevision = 0;
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
            disconnectedClients.Enqueue(clientId);
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
                    ClientId = clientId,
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

    private void ProcessMessage(PendingMessage pending)
    {
        PurchaseResponse response = new PurchaseResponse();
        bool stateChanged = false;
        try
        {
            ClientCommand command = JsonUtility.FromJson<ClientCommand>(pending.Json);
            if (command == null)
                throw new FormatException();
            response.requestId = command != null ? command.requestId : null;
            if (command.type == "hello")
            {
                response.type = "sessionResult";
                response.success = ProcessHello(pending.ClientId, command.clientToken, response);
                response.status = game != null ? game.Status.ToString() : "Unavailable";
                stateChanged = response.success;
            }
            else if (string.IsNullOrEmpty(command.requestId) || command.requestId.Length > 64)
            {
                response.message = "Solicitud inválida.";
            }
            else
            {
                PlayerSession session = GetSession(pending.ClientId);
                if (session == null)
                {
                    response.message = "Primero debes identificar esta sesión.";
                }
                else if (command.type == "selectArmy")
                {
                    response.type = "selectionResult";
                    response.success = SelectArmy(session, command.army, out response.message);
                    stateChanged = response.success;
                }
                else if (command.type == "joinDeveloper")
                {
                    response.type = "selectionResult";
                    response.success = JoinDeveloper(session, out response.message);
                    stateChanged = response.success;
                }
                else if (command.type == "buyTower")
                {
                    response.type = "purchaseResult";
                    if (!TryParseArmy(command.army, out ArmyType army))
                        response.message = "Ejército desconocido.";
                    else if (!CanControl(session, army))
                        response.message = "Solo puedes dar órdenes a tu ejército.";
                    else if (game == null || !game.IsPurchasePhase)
                        response.message = "Las torres solo se pueden comprar durante la fase de compras.";
                    else
                    {
                        response.success = shop.TryPurchase(army, out string failureReason);
                        response.message = response.success
                            ? "Torre de " + shop.GetTowerDisplayName(army) + " comprada."
                            : failureReason;
                        stateChanged = response.success;
                    }
                }
                else if (command.type == "buyIntermissionUpgrade")
                {
                    response.type = "purchaseResult";
                    if (!TryParseArmy(command.army, out ArmyType army))
                        response.message = "Ejército desconocido.";
                    else if (!CanControl(session, army))
                        response.message = "Solo puedes dar órdenes a tu ejército.";
                    else if (game == null)
                        response.message = "La partida no está disponible.";
                    else
                    {
                        response.success = game.TryChooseIntermissionOption(army, command.choice, out response.message);
                        stateChanged = response.success;
                    }
                }
                else if (command.type == "callNextWaveEarly")
                {
                    response.type = "waveCallResult";
                    if (!CanControl(session, ArmyType.Mar))
                        response.message = "Solo el ejército Mar puede adelantar la siguiente oleada.";
                    else if (game == null)
                        response.message = "La partida no está disponible.";
                    else
                    {
                        response.success = game.TryCallNextWaveEarly(out response.message);
                        stateChanged = response.success;
                    }
                }
                else
                {
                    response.message = "Orden desconocida.";
                }
            }
        }
        catch (Exception)
        {
            response.success = false;
            response.message = "No se pudo interpretar la solicitud.";
        }

        pending.Response.TrySetResult(JsonUtility.ToJson(response));
        if (stateChanged)
            PublishState();
    }

    private bool ProcessHello(int clientId, string token, PurchaseResponse response)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 16 || token.Length > 64)
        {
            response.message = "Identificador de sesión no válido.";
            return false;
        }

        if (sessionsByClient.TryGetValue(clientId, out PlayerSession currentSession))
        {
            if (currentSession.Token != token)
            {
                response.message = "Esta conexión ya está vinculada a otra sesión.";
                return false;
            }
            response.selectedArmy = GetArmyName(currentSession.ArmyIndex);
            response.developerMode = currentSession.Developer;
            response.message = "Sesión ya identificada.";
            return true;
        }

        PlayerSession session;
        if (sessionsByToken.TryGetValue(token, out session))
        {
            if (session.Connected && session.ClientId != clientId &&
                !webSocketClients.ContainsKey(session.ClientId))
            {
                sessionsByClient.Remove(session.ClientId);
                session.Connected = false;
                session.ClientId = -1;
            }
            if (session.Connected && session.ClientId != clientId)
            {
                response.message = "Esta sesión ya está conectada en otra ventana.";
                return false;
            }
            Debug.Log("Reconexión de sesión. Ejército: " + GetArmyName(session.ArmyIndex) +
                      (session.Developer ? " (desarrollador)" : string.Empty) + ".", this);
        }
        else
        {
            session = new PlayerSession { Token = token };
            sessionsByToken.Add(token, session);
            Debug.Log("Nueva sesión móvil conectada.", this);
        }

        if (session.ClientId >= 0)
            sessionsByClient.Remove(session.ClientId);
        session.Connected = true;
        session.ClientId = clientId;
        session.DisconnectedAt = 0f;
        sessionsByClient[clientId] = session;
        response.selectedArmy = GetArmyName(session.ArmyIndex);
        response.developerMode = session.Developer;
        response.message = "Sesión conectada.";
        return true;
    }

    private bool SelectArmy(PlayerSession session, string armyName, out string message)
    {
        message = null;
        if (game == null || game.Status != TriunviratoGame.MatchStatus.WaitingForPlayers)
        {
            message = "La sala ya no admite nuevas selecciones.";
            return false;
        }
        if (session.Developer || developerToken != null)
        {
            message = "El modo desarrollador ya está activo.";
            return false;
        }
        if (!TryParseArmy(armyName, out ArmyType army))
        {
            message = "Ejército desconocido.";
            return false;
        }

        int targetIndex = (int)army;
        string owner = armySeats[targetIndex];
        if (owner != null && owner != session.Token)
        {
            message = "Ese ejército ya está ocupado.";
            return false;
        }

        if (session.ArmyIndex >= 0 && session.ArmyIndex != targetIndex)
            armySeats[session.ArmyIndex] = null;
        armySeats[targetIndex] = session.Token;
        session.ArmyIndex = targetIndex;
        Debug.Log("Sesión asignada al ejército " + army + ".", this);

        if (AllArmiesOccupied())
        {
            if (game.TryStartMatch())
                message = "Plaza asignada. Los tres ejércitos están listos; comienza la partida.";
            else
                message = "Plaza asignada.";
        }
        else
        {
            message = "Plaza de " + army + " asignada. Esperando a los otros ejércitos.";
        }
        return true;
    }

    private bool JoinDeveloper(PlayerSession session, out string message)
    {
        message = null;
        if (game == null || game.Status != TriunviratoGame.MatchStatus.WaitingForPlayers)
        {
            message = "La partida ya ha comenzado.";
            return false;
        }
        if (developerToken != null && developerToken != session.Token)
        {
            message = "El modo desarrollador ya está ocupado.";
            return false;
        }
        for (int i = 0; i < armySeats.Length; i++)
        {
            if (armySeats[i] != null && armySeats[i] != session.Token)
            {
                message = "El modo desarrollador requiere una sala sin otros ejércitos asignados.";
                return false;
            }
        }

        if (session.ArmyIndex >= 0)
            armySeats[session.ArmyIndex] = null;
        session.ArmyIndex = -1;
        session.Developer = true;
        developerToken = session.Token;
        if (!game.TryStartMatch())
        {
            message = "No se pudo iniciar la partida.";
            return false;
        }
        Debug.Log("Modo desarrollador activado: un jugador controla los tres ejércitos.", this);
        message = "Modo desarrollador activado. La partida ha comenzado.";
        return true;
    }

    private void ProcessDisconnect(int clientId)
    {
        if (!sessionsByClient.TryGetValue(clientId, out PlayerSession session) || session.ClientId != clientId)
            return;
        sessionsByClient.Remove(clientId);
        session.Connected = false;
        session.ClientId = -1;
        session.DisconnectedAt = Time.realtimeSinceStartup;
        Debug.Log("Sesión desconectada. Ejército reservado: " + GetArmyName(session.ArmyIndex) +
                  (session.Developer ? " (desarrollador)" : string.Empty) + ".", this);
        PublishState();
    }

    private void ExpireDisconnectedLobbySeats()
    {
        if (game == null || game.Status != TriunviratoGame.MatchStatus.WaitingForPlayers)
            return;
        float now = Time.realtimeSinceStartup;
        bool changed = false;
        foreach (PlayerSession session in sessionsByToken.Values)
        {
            if (session.Connected || session.ArmyIndex < 0 || now - session.DisconnectedAt < reconnectGraceSeconds)
                continue;
            armySeats[session.ArmyIndex] = null;
            session.ArmyIndex = -1;
            Debug.Log("Venció la reserva de reconexión; plaza liberada.", this);
            changed = true;
        }
        if (changed)
            PublishState();
    }

    private PlayerSession GetSession(int clientId) =>
        sessionsByClient.TryGetValue(clientId, out PlayerSession session) && session.Connected ? session : null;

    private static bool CanControl(PlayerSession session, ArmyType army) =>
        session != null && (session.Developer || session.ArmyIndex == (int)army);

    private bool AllArmiesOccupied()
    {
        for (int i = 0; i < armySeats.Length; i++)
        {
            if (armySeats[i] == null) return false;
        }
        return true;
    }

    private static string GetArmyName(int index) => index >= 0 && index < 3
        ? ((ArmyType)index).ToString() : string.Empty;

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

    private void PublishState()
    {
        if (shutdown == null)
            return;

        observedShopRevision = shop != null ? shop.StateRevision : -1;
        observedEconomyRevision = economy != null ? economy.StateRevision : -1;
        observedGameRevision = game != null ? game.StateRevision : -1;
        outgoingStateRevision++;
        CancellationToken token = shutdown.Token;
        foreach (KeyValuePair<int, WebSocketClient> pair in webSocketClients)
        {
            PlayerSession session = GetSession(pair.Key);
            _ = SendMessageAsync(pair.Value, BuildShopStateJson(session), token);
        }
    }

    private string BuildShopStateJson(PlayerSession session)
    {
        bool canChooseDeveloper = game != null && game.Status == TriunviratoGame.MatchStatus.WaitingForPlayers &&
                                  (developerToken == null || (session != null && developerToken == session.Token));
        if (canChooseDeveloper)
        {
            for (int i = 0; i < armySeats.Length; i++)
            {
                if (armySeats[i] != null && (session == null || armySeats[i] != session.Token))
                    canChooseDeveloper = false;
            }
        }
        ShopStateMessage state = new ShopStateMessage
        {
            revision = outgoingStateRevision,
            status = game != null ? game.Status.ToString() : "Unavailable",
            selectedArmy = session != null ? GetArmyName(session.ArmyIndex).ToLowerInvariant() : string.Empty,
            developerMode = session != null && session.Developer,
            developerAvailable = canChooseDeveloper,
            currentWave = game != null ? game.CurrentWave : 0,
            nextEarlyCallAvailableWave = game != null ? game.NextEarlyCallAvailableWave : 0,
            waveInProgress = game != null && game.IsWaveInProgress,
            earlyWaveCallPending = game != null && game.IsEarlyWaveCallPending,
            canCallNextWaveEarly = game != null && game.CanCallNextWaveEarly && CanControl(session, ArmyType.Mar),
            purchasePhase = game != null && game.IsPurchasePhase,
            purchaseSecondsRemaining = game != null ? game.PurchasePhaseSecondsRemaining : 0f,
            resourceChoiceReward = TriunviratoGame.ResourceChoiceReward,
            seats = new[]
            {
                CreateSeatState(ArmyType.Tierra, session),
                CreateSeatState(ArmyType.Aire, session),
                CreateSeatState(ArmyType.Mar, session)
            },
            armies = new[]
            {
                CreateArmyShopState(ArmyType.Tierra, "tierra", session),
                CreateArmyShopState(ArmyType.Aire, "aire", session),
                CreateArmyShopState(ArmyType.Mar, "mar", session)
            }
        };
        return JsonUtility.ToJson(state);
    }

    private SeatState CreateSeatState(ArmyType army, PlayerSession session)
    {
        string owner = armySeats[(int)army];
        bool occupied = owner != null;
        PlayerSession ownerSession = occupied && sessionsByToken.TryGetValue(owner, out PlayerSession found)
            ? found : null;
        return new SeatState
        {
            army = army.ToString().ToLowerInvariant(),
            status = occupied ? "occupied" : "free",
            connected = ownerSession != null && ownerSession.Connected,
            selectedByMe = session != null && owner == session.Token
        };
    }

    private ArmyShopState CreateArmyShopState(ArmyType army, string name, PlayerSession session)
    {
        HeroConfig hero = game != null ? game.GetHeroConfig(army) : null;
        bool canControl = CanControl(session, army);
        TowerConfig towerConfig = shop.GetConfig(army);
        return new ArmyShopState
        {
            army = name,
            owned = shop.HasTower(army),
            currentDamage = shop.GetCurrentDamage(army),
            towerRange = shop.GetCurrentRange(army),
            towerAttackCooldown = shop.GetCurrentCooldown(army),
            canControl = canControl,
            canPurchaseTower = canControl && shop.CanPurchase && economy != null &&
                               economy.GetCoins(army) >= shop.GetNextCost(army),
            coins = economy != null ? economy.GetCoins(army) : 0,
            passiveIncomePerSecond = economy != null ? economy.GetPassiveIncomePerSecond(army) : 0f,
            incomeSummary = string.Empty,
            heroDamage = game != null ? game.GetHeroDamage(army) : 0f,
            heroBaseDamage = hero != null ? hero.Damage : 0f,
            heroRange = game != null ? game.GetHeroRange(army) : hero != null ? hero.Range : 0f,
            heroAttackCooldown = game != null ? game.GetHeroAttackCooldown(army) : hero != null ? hero.AttackCooldown : 0f,
            heroSplashRadius = game != null ? game.GetHeroSplashRadius(army) : hero != null ? hero.SplashRadius : 0f,
            purchaseChoiceMade = game != null && game.IsPurchaseChoiceMade(army),
            purchaseResult = game != null ? game.GetPurchaseResult(army) : string.Empty,
            purchaseOptions = CreatePurchaseOptions(army)
        };
    }

    [Serializable]
    private sealed class PurchaseUpgradeOption
    {
        public string id;
        public string category;
        public string name;
        public string description;
        public int price;
        public int[] resourceCosts;
        public int stars;
    }

    private PurchaseUpgradeOption[] CreatePurchaseOptions(ArmyType army)
    {
        if (game == null) return Array.Empty<PurchaseUpgradeOption>();
        TriunviratoGame.PurchaseOffer[] offers = game.GetPurchaseOffers(army);
        PurchaseUpgradeOption[] options = new PurchaseUpgradeOption[offers.Length];
        for (int i = 0; i < offers.Length; i++)
        {
            TriunviratoGame.PurchaseOffer offer = offers[i];
            if (offer == null) continue;
            int total = 0;
            foreach (int amount in offer.resourceCosts) total += amount;
            options[i] = new PurchaseUpgradeOption { id = offer.id, category = offer.category,
                name = offer.name, description = offer.description, price = total,
                resourceCosts = offer.resourceCosts, stars = offer.stars };
        }
        return options;
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

    private string SeatSummary()
    {
        string[] names = { "Tierra", "Aire", "Mar" };
        string summary = string.Empty;
        for (int i = 0; i < armySeats.Length; i++)
        {
            if (i > 0) summary += "  |  ";
            summary += names[i] + (armySeats[i] == null ? ": libre" : ": ocupada");
        }
        return developerToken != null ? "Modo desarrollador" : summary;
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
