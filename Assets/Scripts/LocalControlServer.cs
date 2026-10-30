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
    public const int MaximumPlayerNameLength = 24;

    [SerializeField, Min(1)] private int port = 2468;
    [SerializeField] private TowerManager towerManager;
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
    private readonly bool[] restartRequestedByArmy = new bool[3];
    private bool restartTriggered;
    private string developerToken;
    private TcpListener listener;
    private CancellationTokenSource shutdown;
    private string pagePath;
    private string iconsDirectory;
    private string status = "Servidor detenido";
    private string accessUrl = "IP LAN no detectada";
    private int nextClientId;
    private int observedTowerRevision = -1;
    private int observedGameRevision = -1;
    private int outgoingStateRevision;
    private float nextSpecialCooldownPublishTime;

    public bool IsDeveloperMode => developerToken != null;
    public string StatusText => status;
    public string AccessUrlText => accessUrl;
    public int RestartRequestCount => CountRestartRequests();
    public string RoomStatusText => game != null ? "Sala: " + game.Status + " · " + SeatSummary() : "Sala: no disponible";

    /// <summary>Reads a live lobby seat directly from the authoritative session table.</summary>
    public bool TryGetLobbySeatState(int seatIndex, out bool occupied, out bool connected)
    {
        occupied = false;
        connected = false;
        if (seatIndex < 0 || seatIndex >= armySeats.Length)
            return false;

        string owner = armySeats[seatIndex];
        occupied = owner != null;
        if (occupied && sessionsByToken.TryGetValue(owner, out PlayerSession ownerSession))
            connected = ownerSession.Connected;
        return true;
    }

    [Serializable]
    private sealed class ClientCommand
    {
#pragma warning disable 0649 // Unity's JsonUtility fills these fields from the incoming JSON.
        public string type;
        public string army;
        public string abilityId;
        public string targetArmy;
        public string playerName;
        public string choice;
        public string offerToken;
        public string clientToken;
        public string requestId;
#pragma warning restore 0649
    }

    [Serializable]
    private sealed class ServerResponse
    {
        public string type = "upgradeResult";
        public string requestId;
        public bool success;
        public string message;
        public string selectedArmy;
        public bool developerMode;
        public string status;
    }

    [Serializable]
    private sealed class MatchStateMessage
    {
        public string type = "matchState";
        public int revision;
        public string status;
        public string selectedArmy;
        public bool developerMode;
        public bool developerAvailable;
        public int currentWave;
        public bool waveInProgress;
        public int restartRequestCount;
        public SeatState[] seats;
        public ArmyState[] armies;
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
    private sealed class ArmyState
    {
        public string army;
        public bool towerUnlocked;
        public float currentDamage;
        public float towerRange;
        public float towerAttackCooldown;
        public bool canControl;
        public int level;
        public float experience;
        public float experienceNeeded;
        public float experienceMultiplier;
        public float experienceIncrease;
        public float luckChance;
        public float heroBossDamageBonusPercent;
        public float damageByHero;
        public float damageByTower;
        public float damageBySpecialAbility;
        public float damageByClone;
        public bool restartRequested;
        public bool hasUpgradeOffer;
        public bool hasBossOffer;
        public float heroDamage;
        public float heroBaseDamage;
        public float heroRange;
        public float heroAttackCooldown;
        public float heroSplashRadius;
        public float heroBaseCooldown;
        public float heroBaseRange;
        public float heroDamageMultiplier;
        public float heroAttackSpeedMultiplier;
        public float heroRangeMultiplier;
        public float heroSplashMultiplier;
        public float towerBaseDamage;
        public float towerDamageMultiplier;
        public float towerDamageFixedBonus;
        public float towerSplashRadius;
        public float towerBaseCooldown;
        public float towerBaseRange;
        public float towerAttackSpeedMultiplier;
        public float towerRangeMultiplier;
        public float heroDamageFixedBonus;
        public float heroRangeFixedBonus;
        public float heroSplashFixedBonus;
        public float heroAttackRateFixedBonus;
        public float towerRangeFixedBonus;
        public float towerAttackRateFixedBonus;
        public string heroName;
        public string playerName;
        public string towerName;
        public string specialAbilityName;
        public string specialAbilityDescription;
        public float specialCooldownRemaining;
        public float specialCooldownDuration;
        public bool specialCanActivate;
        public bool legacySpecialUnlocked;
        public int specialEffectCount;
        public float specialDamagePerEffect;
        public float specialNormalDuration;
        public float specialBossDuration;
        public string fireRageAbilityName;
        public string fireRageAbilityDescription;
        public float fireRageDamageBonusPercent;
        public float fireRageDuration;
        public float fireRageCooldownRemaining;
        public float fireRageCooldownDuration;
        public bool fireRageCanActivate;
        public bool fireRageCanActivateAire;
        public bool fireRageCanActivateTierra;
        public bool fireRageCanActivateMar;
        public bool fireRageUnlocked;
        public string cloneAbilityName;
        public string cloneAbilityDescription;
        public float cloneDuration;
        public float cloneCooldownRemaining;
        public float cloneCooldownDuration;
        public bool cloneCanActivate;
        public bool cloneUnlocked;
        public string knightSummonAbilityName;
        public string knightSummonAbilityDescription;
        public float knightSummonCooldownRemaining;
        public float knightSummonCooldownDuration;
        public bool knightSummonCanActivate;
        public bool knightSummonUnlocked;
        public string warBlessingAbilityName;
        public string warBlessingAbilityDescription;
        public float warBlessingBonusPercent;
        public float warBlessingDuration;
        public float warBlessingCooldownRemaining;
        public float warBlessingCooldownDuration;
        public bool warBlessingCanActivateAire;
        public bool warBlessingCanActivateTierra;
        public bool warBlessingCanActivateMar;
        public bool warBlessingUnlocked;
        public string waterTornadoAbilityName;
        public string waterTornadoAbilityDescription;
        public float waterTornadoDamage;
        public float waterTornadoPushDistance;
        public float waterTornadoCooldownRemaining;
        public float waterTornadoCooldownDuration;
        public bool waterTornadoCanActivate;
        public bool waterTornadoUnlocked;
        public string electricBirdAbilityName;
        public string electricBirdAbilityDescription;
        public float electricBirdDuration;
        public float electricBirdCooldownRemaining;
        public float electricBirdCooldownDuration;
        public bool electricBirdCanActivate;
        public bool electricBirdUnlocked;
        public UpgradeOption[] upgradeOptions;
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
        public string PlayerName;
        public bool Developer;
        public bool Connected;
        public int ClientId = -1;
        public float DisconnectedAt;
        public readonly Dictionary<string, string> SpecialResponses = new Dictionary<string, string>();
        public readonly Queue<string> SpecialResponseOrder = new Queue<string>();

        public bool TryGetSpecialResponse(string requestId, out string response) =>
            SpecialResponses.TryGetValue(requestId, out response);

        public void CacheSpecialResponse(string requestId, string response)
        {
            if (string.IsNullOrEmpty(requestId) || SpecialResponses.ContainsKey(requestId))
                return;
            SpecialResponses.Add(requestId, response);
            SpecialResponseOrder.Enqueue(requestId);
            while (SpecialResponseOrder.Count > 64)
                SpecialResponses.Remove(SpecialResponseOrder.Dequeue());
        }
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

        // The TV overlay can restart directly. Clear votes carried from the finished
        // match when the shared game state returns to combat.
        if (game != null && game.Status == TriunviratoGame.MatchStatus.InProgress && CountRestartRequests() > 0)
        {
            Array.Clear(restartRequestedByArmy, 0, restartRequestedByArmy.Length);
            restartTriggered = false;
        }

        if (game != null && game.HasSpecialCooldownsRunning && Time.unscaledTime >= nextSpecialCooldownPublishTime)
        {
            nextSpecialCooldownPublishTime = Time.unscaledTime + 0.25f;
            PublishState();
        }

        int gameRevision = game != null ? game.StateRevision : -1;
        if (towerManager != null && (towerManager.StateRevision != observedTowerRevision || gameRevision != observedGameRevision))
        {
            observedTowerRevision = towerManager.StateRevision;
            observedGameRevision = gameRevision;
            PublishState();
        }
    }

    private void StartServer()
    {
        if (towerManager == null)
            towerManager = FindAnyObjectByType<TowerManager>();
        if (game == null)
            game = FindAnyObjectByType<TriunviratoGame>();

        if (towerManager == null)
        {
            status = "Error: no se encontró Tower Manager";
            Debug.LogError(status, this);
            return;
        }

        pagePath = Path.Combine(Application.streamingAssetsPath, "remote-control.html");
        iconsDirectory = Path.Combine(Application.streamingAssetsPath, "icons");
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
            observedTowerRevision = towerManager.StateRevision;
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

                string iconFileName = path == "/icons/ground_icon.png" ? "ground_icon.png" :
                    path == "/icons/air_icon.png" ? "air_icon.png" :
                    path == "/icons/sea_icon.png" ? "sea_icon.png" : null;
                if (iconFileName != null)
                {
                    string iconPath = Path.Combine(iconsDirectory, iconFileName);
                    if (File.Exists(iconPath))
                    {
                        byte[] icon = await File.ReadAllBytesAsync(iconPath, token).ConfigureAwait(false);
                        await WriteHttpResponseAsync(stream, 200, "OK", "image/png", icon, token)
                            .ConfigureAwait(false);
                        return;
                    }
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
                        await SendMessageAsync(client, new ServerResponse
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
        ServerResponse response = new ServerResponse();
        PlayerSession specialResponseSession = null;
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
                else if (command.type == "activateSpecial" && session.TryGetSpecialResponse(command.requestId, out string cachedSpecialResponse))
                {
                    pending.Response.TrySetResult(cachedSpecialResponse);
                    return;
                }
                else if (command.type == "selectArmy")
                {
                    response.type = "selectionResult";
                    response.success = SelectArmy(session, command.army, command.playerName, out response.message);
                    stateChanged = response.success;
                }
                else if (command.type == "joinDeveloper")
                {
                    response.type = "selectionResult";
                    response.success = JoinDeveloper(session, command.playerName, out response.message);
                    stateChanged = response.success;
                }
                else if (command.type == "selectUpgrade")
                {
                    response.type = "upgradeResult";
                    if (!TryParseArmy(command.army, out ArmyType army))
                        response.message = "Ejército desconocido.";
                    else if (!CanControl(session, army))
                        response.message = "Solo puedes dar órdenes a tu ejército.";
                    else if (game == null)
                        response.message = "La partida no está disponible.";
                    else
                    {
                        response.success = game.TrySelectUpgrade(army, command.choice, command.offerToken, out response.message);
                        stateChanged = response.success;
                    }
                }
                else if (command.type == "requestRestart")
                {
                    response.type = "restartVoteResult";
                    if (game == null || game.Status != TriunviratoGame.MatchStatus.Finished)
                        response.message = "El reinicio solo se puede solicitar al terminar la partida.";
                    else if (session.Developer || session.ArmyIndex < 0 || session.ArmyIndex >= restartRequestedByArmy.Length ||
                             !CanControl(session, (ArmyType)session.ArmyIndex))
                        response.message = "Esta sesión no está vinculada a un ejército jugador.";
                    else if (restartTriggered)
                        response.message = "El reinicio ya está en curso.";
                    else
                    {
                        int armyIndex = session.ArmyIndex;
                        if (!restartRequestedByArmy[armyIndex])
                        {
                            restartRequestedByArmy[armyIndex] = true;
                            stateChanged = true;
                        }
                        response.success = true;
                        response.message = "Confirmación de reinicio registrada.";
                        int votes = 0;
                        foreach (bool requested in restartRequestedByArmy)
                            if (requested) votes++;
                        if (votes == restartRequestedByArmy.Length)
                        {
                            restartTriggered = true;
                            game.RestartFinishedMatch();
                            Array.Clear(restartRequestedByArmy, 0, restartRequestedByArmy.Length);
                            restartTriggered = false;
                            response.message = "Los tres jugadores confirmaron. Nueva partida iniciada.";
                            stateChanged = true;
                        }
                    }
                }
                else if (command.type == "activateSpecial")
                {
                    response.type = "specialAbilityResult";
                    specialResponseSession = session;
                    if (!TryParseArmy(command.army, out ArmyType army))
                        response.message = "Ejército desconocido.";
                    else if (!CanControl(session, army))
                        response.message = "Solo puedes activar la habilidad de tu ejército.";
                    else if (game == null)
                        response.message = "La partida no está disponible.";
                    else
                    {
                        response.success = string.IsNullOrEmpty(command.abilityId)
                            ? game.TryActivateSpecial(army, out response.message, session.Developer)
                            : game.TryActivateSpecial(army, command.abilityId, command.targetArmy, out response.message, session.Developer);
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

        string responseJson = JsonUtility.ToJson(response);
        if (specialResponseSession != null && !string.IsNullOrEmpty(response.requestId))
            specialResponseSession.CacheSpecialResponse(response.requestId, responseJson);
        pending.Response.TrySetResult(responseJson);
        if (stateChanged)
            PublishState();
    }

    private bool ProcessHello(int clientId, string token, ServerResponse response)
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

    private bool SelectArmy(PlayerSession session, string armyName, string playerName, out string message)
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

        session.PlayerName = CleanPlayerName(playerName) ?? army.ToString();

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

    private static string CleanPlayerName(string value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        StringBuilder clean = new StringBuilder(Mathf.Min(trimmed.Length, MaximumPlayerNameLength));
        foreach (char character in trimmed)
        {
            if (char.IsControl(character)) continue;
            clean.Append(character);
            if (clean.Length >= MaximumPlayerNameLength) break;
        }
        string result = clean.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public string GetPlayerName(ArmyType army)
    {
        if (developerToken != null && sessionsByToken.TryGetValue(developerToken, out PlayerSession developer) &&
            !string.IsNullOrWhiteSpace(developer.PlayerName)) return developer.PlayerName;
        if ((int)army < 0 || (int)army >= armySeats.Length) return army.ToString();
        string owner = armySeats[(int)army];
        if (owner != null && sessionsByToken.TryGetValue(owner, out PlayerSession session) &&
            !string.IsNullOrWhiteSpace(session.PlayerName)) return session.PlayerName;
        return army.ToString();
    }

    private bool JoinDeveloper(PlayerSession session, string playerName, out string message)
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
        session.PlayerName = CleanPlayerName(playerName);
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

    private static string GetLegacyAbilityId(ArmyType army) => army == ArmyType.Tierra ? "zombie_grasp" :
        army == ArmyType.Aire ? "meteor_shower" : army == ArmyType.Mar ? "lightning_storm" : string.Empty;

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

        observedTowerRevision = towerManager != null ? towerManager.StateRevision : -1;
        observedGameRevision = game != null ? game.StateRevision : -1;
        outgoingStateRevision++;
        CancellationToken token = shutdown.Token;
        foreach (KeyValuePair<int, WebSocketClient> pair in webSocketClients)
        {
            PlayerSession session = GetSession(pair.Key);
            _ = SendMessageAsync(pair.Value, BuildMatchStateJson(session), token);
        }
    }

    private string BuildMatchStateJson(PlayerSession session)
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
        MatchStateMessage state = new MatchStateMessage
        {
            revision = outgoingStateRevision,
            status = game != null ? game.Status.ToString() : "Unavailable",
            selectedArmy = session != null ? GetArmyName(session.ArmyIndex).ToLowerInvariant() : string.Empty,
            developerMode = session != null && session.Developer,
            developerAvailable = canChooseDeveloper,
            currentWave = game != null ? game.CurrentWave : 0,
            waveInProgress = game != null && game.IsWaveInProgress,
            restartRequestCount = CountRestartRequests(),
            seats = new[]
            {
                CreateSeatState(ArmyType.Tierra, session),
                CreateSeatState(ArmyType.Aire, session),
                CreateSeatState(ArmyType.Mar, session)
            },
            armies = new[]
            {
                CreateArmyState(ArmyType.Tierra, "tierra", session),
                CreateArmyState(ArmyType.Aire, "aire", session),
                CreateArmyState(ArmyType.Mar, "mar", session)
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

    private ArmyState CreateArmyState(ArmyType army, string name, PlayerSession session)
    {
        HeroConfig hero = game != null ? game.GetHeroConfig(army) : null;
        bool canControl = CanControl(session, army);
        TowerConfig towerConfig = towerManager.GetConfig(army);
        return new ArmyState
        {
            army = name,
            towerUnlocked = towerManager.HasTower(army),
            currentDamage = towerManager.GetCurrentDamage(army),
            towerRange = towerManager.GetCurrentRange(army),
            towerAttackCooldown = towerManager.GetCurrentCooldown(army),
            canControl = canControl,
            heroDamage = game != null ? game.GetHeroDamage(army) : 0f,
            heroBaseDamage = hero != null ? hero.Damage : 0f,
            heroRange = game != null ? game.GetHeroRange(army) : hero != null ? hero.Range : 0f,
            heroAttackCooldown = game != null ? game.GetHeroAttackCooldown(army) : hero != null ? hero.AttackCooldown : 0f,
            heroSplashRadius = game != null ? game.GetHeroSplashRadius(army) : hero != null ? hero.SplashRadius : 0f,
            heroBaseCooldown = hero != null ? hero.AttackCooldown : 0f,
            heroBaseRange = hero != null ? hero.Range : 0f,
            heroDamageMultiplier = game != null ? game.GetHeroUpgradeMultiplier(army) : 1f,
            heroAttackSpeedMultiplier = game != null ? game.GetHeroAttackSpeedMultiplier(army) : 1f,
            heroRangeMultiplier = game != null ? game.GetHeroRangeMultiplier(army) : 1f,
            heroSplashMultiplier = game != null ? game.GetHeroSplashMultiplier(army) : 1f,
            heroDamageFixedBonus = game != null ? game.GetHeroDamageFixedBonus(army) : 0f,
            heroRangeFixedBonus = game != null ? game.GetHeroRangeFixedBonus(army) : 0f,
            heroSplashFixedBonus = game != null ? game.GetHeroSplashFixedBonus(army) : 0f,
            heroAttackRateFixedBonus = game != null ? game.GetHeroAttackRateFixedBonus(army) : 0f,
            towerRangeFixedBonus = game != null ? game.GetTowerRangeFixedBonus(army) : 0f,
            towerAttackRateFixedBonus = game != null ? game.GetTowerAttackRateFixedBonus(army) : 0f,
            heroName = hero != null ? hero.DisplayName : "Héroe",
            playerName = GetPlayerName(army),
            towerBaseDamage = towerConfig != null ? towerConfig.GetBaseDamage() : 0f,
            towerDamageMultiplier = game != null ? game.GetTowerUpgradeMultiplier(army) : 1f,
            towerDamageFixedBonus = game != null ? game.GetTowerDamageFixedBonus(army) : 0f,
            towerSplashRadius = towerConfig != null && towerConfig.Hero != null ? towerConfig.Hero.SplashRadius : 0f,
            towerBaseCooldown = towerConfig != null && towerConfig.Hero != null ? towerConfig.Hero.AttackCooldown : 0f,
            towerBaseRange = towerConfig != null && towerConfig.Hero != null ? towerConfig.Hero.Range : 0f,
            towerAttackSpeedMultiplier = game != null ? game.GetTowerAttackSpeedMultiplier(army) : 1f,
            towerRangeMultiplier = game != null ? game.GetTowerRangeMultiplier(army) : 1f,
            towerName = towerManager.GetTowerDisplayName(army),
            specialAbilityName = game != null ? game.GetSpecialAbilityName(army) : "Habilidad especial",
            specialAbilityDescription = game != null ? game.GetSpecialAbilityDescription(army) : string.Empty,
            specialCooldownRemaining = game != null ? game.GetSpecialAbilityCooldownRemaining(army) : 0f,
            specialCooldownDuration = game != null ? game.GetSpecialAbilityCooldown(army) : 0f,
            specialCanActivate = canControl && game != null && game.CanActivateSpecial(army, session != null && session.Developer),
            legacySpecialUnlocked = game != null && game.IsSpecialAbilityUnlocked(army, GetLegacyAbilityId(army)),
            specialEffectCount = game != null ? game.GetSpecialAbilityEffectCount(army) : 0,
            specialDamagePerEffect = game != null ? game.GetSpecialAbilityDamagePerEffect(army) : 0f,
            specialNormalDuration = game != null ? game.GetSpecialAbilityNormalDuration(army) : 0f,
            specialBossDuration = game != null ? game.GetSpecialAbilityBossDuration(army) : 0f,
            fireRageAbilityName = game != null ? game.GetFireRageAbilityName() : "Furia ígnea",
            fireRageAbilityDescription = game != null ? game.GetFireRageAbilityDescription() : string.Empty,
            fireRageDamageBonusPercent = game != null ? game.GetFireRageDamageBonusPercent() : 0f,
            fireRageDuration = game != null ? game.GetFireRageDuration() : 0f,
            fireRageCooldownRemaining = game != null ? game.GetFireRageCooldownRemaining() : 0f,
            fireRageCooldownDuration = game != null ? game.GetFireRageCooldown() : 0f,
            fireRageCanActivate = canControl && game != null && game.CanActivateFireRage(session != null && session.Developer),
            fireRageCanActivateAire = canControl && game != null && game.CanActivateFireRage(ArmyType.Aire, session != null && session.Developer),
            fireRageCanActivateTierra = canControl && game != null && game.CanActivateFireRage(ArmyType.Tierra, session != null && session.Developer),
            fireRageCanActivateMar = canControl && game != null && game.CanActivateFireRage(ArmyType.Mar, session != null && session.Developer),
            fireRageUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Aire, "fire_rage"),
            cloneAbilityName = game != null ? game.GetCloneAbilityName() : "Clon",
            cloneAbilityDescription = game != null ? game.GetCloneAbilityDescription() : string.Empty,
            cloneDuration = game != null ? game.GetCloneDuration() : 0f,
            cloneCooldownRemaining = game != null ? game.GetCloneCooldownRemaining() : 0f,
            cloneCooldownDuration = game != null ? game.GetCloneCooldown() : 0f,
            cloneCanActivate = canControl && game != null && game.CanActivateClone(session != null && session.Developer),
            cloneUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Aire, "hero_clone"),
            knightSummonAbilityName = game != null ? game.GetKnightSummonName() : "Invocar caballero",
            knightSummonAbilityDescription = game != null ? game.GetKnightSummonDescription() : string.Empty,
            knightSummonCooldownRemaining = game != null ? game.GetKnightSummonCooldownRemaining() : 0f,
            knightSummonCooldownDuration = game != null ? game.GetKnightSummonCooldown() : 0f,
            knightSummonCanActivate = canControl && game != null && game.CanActivateKnightSummon(session != null && session.Developer),
            knightSummonUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Tierra, "knight_summon"),
            warBlessingAbilityName = game != null ? game.GetWarBlessingName() : "Acelerar aliado",
            warBlessingAbilityDescription = game != null ? game.GetWarBlessingDescription() : string.Empty,
            warBlessingBonusPercent = game != null ? game.GetWarBlessingBonusPercent() : 0f,
            warBlessingDuration = game != null ? game.GetWarBlessingDuration() : 0f,
            warBlessingCooldownRemaining = game != null ? game.GetWarBlessingCooldownRemaining() : 0f,
            warBlessingCooldownDuration = game != null ? game.GetWarBlessingCooldown() : 0f,
            warBlessingCanActivateAire = canControl && game != null && game.CanActivateWarBlessing(ArmyType.Aire, session != null && session.Developer),
            warBlessingCanActivateTierra = canControl && game != null && game.CanActivateWarBlessing(ArmyType.Tierra, session != null && session.Developer),
            warBlessingCanActivateMar = canControl && game != null && game.CanActivateWarBlessing(ArmyType.Mar, session != null && session.Developer),
            warBlessingUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Tierra, "war_blessing"),
            waterTornadoAbilityName = game != null ? game.GetWaterTornadoName() : "Huracán imparable",
            waterTornadoAbilityDescription = game != null ? game.GetWaterTornadoDescription() : string.Empty,
            waterTornadoDamage = game != null ? game.GetWaterTornadoDamage() : 0f,
            waterTornadoPushDistance = game != null ? game.GetWaterTornadoPushDistance() : 0f,
            waterTornadoCooldownRemaining = game != null ? game.GetWaterTornadoCooldownRemaining() : 0f,
            waterTornadoCooldownDuration = game != null ? game.GetWaterTornadoCooldown() : 0f,
            waterTornadoCanActivate = canControl && game != null && game.CanActivateWaterTornado(session != null && session.Developer),
            waterTornadoUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Mar, "water_tornado"),
            electricBirdAbilityName = game != null ? game.GetElectricBirdName() : "Ave eléctrica",
            electricBirdAbilityDescription = game != null ? game.GetElectricBirdDescription() : string.Empty,
            electricBirdDuration = game != null ? game.GetElectricBirdDuration() : 0f,
            electricBirdCooldownRemaining = game != null ? game.GetElectricBirdCooldownRemaining() : 0f,
            electricBirdCooldownDuration = game != null ? game.GetElectricBirdCooldown() : 0f,
            electricBirdCanActivate = canControl && game != null && game.CanActivateElectricBird(session != null && session.Developer),
            electricBirdUnlocked = game != null && game.IsSpecialAbilityUnlocked(ArmyType.Mar, "electric_bird"),
            level = game != null ? game.GetLevel(army) : 1,
            experience = game != null ? game.GetExperience(army) : 0f,
            experienceNeeded = game != null ? game.GetExperienceNeeded(army) : 0f,
            experienceMultiplier = game != null ? game.GetExperienceMultiplier(army) : 1f,
            experienceIncrease = game != null ? game.GetExperienceIncrease(army) : 0f,
            luckChance = game != null ? game.GetLuckChance(army) : 0f,
            heroBossDamageBonusPercent = game != null ? game.GetHeroBossDamageBonusPercent(army) : 0f,
            damageByHero = game != null ? game.GetDamageByHero(army) : 0f,
            damageByTower = game != null ? game.GetDamageByTower(army) : 0f,
            damageBySpecialAbility = game != null ? game.GetDamageBySpecialAbility(army) : 0f,
            damageByClone = game != null ? game.GetDamageByClone(army) : 0f,
            restartRequested = restartRequestedByArmy[(int)army],
            hasUpgradeOffer = game != null && game.GetUpgradeOffers(army).Length > 0,
            hasBossOffer = game != null && game.GetUpgradeOffers(army).Length > 0 && game.GetUpgradeOffers(army)[0].stars >= 3,
            upgradeOptions = CreateUpgradeOptions(army)
        };
    }

    [Serializable]
    private sealed class UpgradeOption
    {
        public string id;
        public string offerToken;
        public string category;
        public string name;
        public string description;
        public int stars;
    }

    private int CountRestartRequests()
    {
        int count = 0;
        foreach (bool requested in restartRequestedByArmy)
            if (requested) count++;
        return count;
    }

    private UpgradeOption[] CreateUpgradeOptions(ArmyType army)
    {
        if (game == null) return Array.Empty<UpgradeOption>();
        TriunviratoGame.UpgradeOffer[] offers = game.GetUpgradeOffers(army);
        UpgradeOption[] options = new UpgradeOption[offers.Length];
        for (int i = 0; i < offers.Length; i++)
        {
            TriunviratoGame.UpgradeOffer offer = offers[i];
            if (offer == null) continue;
            options[i] = new UpgradeOption { id = offer.id, offerToken = offer.offerToken, category = offer.category,
                name = offer.name, description = offer.description, stars = offer.stars };
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
