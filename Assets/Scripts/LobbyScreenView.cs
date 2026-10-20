using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored local lobby. Dynamic text and QR data are refreshed from the live game/server.</summary>
public sealed class LobbyScreenView : MonoBehaviour
{
    [Header("Fuentes de estado")]
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private LocalControlServer controlServer;
    [SerializeField] private CanvasGroup visibilityGroup;

    [Header("Textos editables en escena")]
    [SerializeField] private Text versionText;
    [SerializeField] private Text connectionAddressText;
    [SerializeField] private Text serverStatusText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text tierraStatusText;
    [SerializeField] private Text aireStatusText;
    [SerializeField] private Text marStatusText;
    [SerializeField] private Image qrImage;

    [Header("Contenido editable")]
    [SerializeField] private string[] armyNames = { "Tierra", "Aire", "Mar" };
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.3f;

    private float refreshTimer;
    private string displayedUrl;
    private float visibleAlpha = 1f;
    private bool qrInitialized;
    private Texture2D qrTexture;
    private Sprite qrSprite;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<TriunviratoGame>();
        if (controlServer == null) controlServer = FindAnyObjectByType<LocalControlServer>();
        if (visibilityGroup != null) visibleAlpha = visibilityGroup.alpha;
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        bool waiting = game == null || game.Status == TriunviratoGame.MatchStatus.WaitingForPlayers;
        if (visibilityGroup != null)
        {
            visibilityGroup.alpha = waiting ? visibleAlpha : 0f;
            visibilityGroup.interactable = waiting;
            visibilityGroup.blocksRaycasts = waiting;
        }
        if (!waiting) return;

        // if (versionText != null) versionText.text = "Versión " + Application.version;
        string url = controlServer != null ? controlServer.AccessUrlText : string.Empty;
        bool validUrl = !string.IsNullOrWhiteSpace(url) && url.StartsWith("http://") && url.EndsWith("/");
        if (connectionAddressText != null)
            connectionAddressText.text = validUrl ? url : "Dirección local no disponible. Comprueba que el servidor haya iniciado.";
        if (serverStatusText != null)
            serverStatusText.text = controlServer != null ? controlServer.StatusText : "Servidor local no disponible.";
        UpdateQr(validUrl ? url : null);
        UpdateSeats();
    }

    private void UpdateSeats()
    {
        int occupiedCount = 0;
        Text[] labels = { tierraStatusText, aireStatusText, marStatusText };
        for (int i = 0; i < 3; i++)
        {
            bool occupied = false;
            bool connected = false;
            if (controlServer != null)
                controlServer.TryGetLobbySeatState(i, out occupied, out connected);
            if (occupied) occupiedCount++;

            string state = !occupied ? "Libre · puedes elegirlo" : connected ? "Jugador conectado · listo" : "Desconectado · plaza reservada";
            if (labels[i] != null)
                labels[i].text = ArmyLabel(i) + "\n" + state;
        }
        if (playerCountText != null) playerCountText.text = "Ejércitos elegidos: " + occupiedCount + "/3";
    }

    private string ArmyLabel(int index) => armyNames != null && index < armyNames.Length && !string.IsNullOrEmpty(armyNames[index])
        ? armyNames[index] : new[] { "Tierra", "Aire", "Mar" }[index];

    private void UpdateQr(string url)
    {
        if ((qrInitialized && url == displayedUrl) || qrImage == null) return;
        qrInitialized = true;
        displayedUrl = url;
        if (qrSprite != null) Destroy(qrSprite);
        if (qrTexture != null) Destroy(qrTexture);
        qrSprite = null;
        qrTexture = null;
        if (string.IsNullOrEmpty(url))
        {
            qrImage.sprite = null;
            qrImage.enabled = false;
            return;
        }

        if (!LocalQrCode.TryEncode(url, out bool[,] modules))
        {
            qrImage.sprite = null;
            qrImage.enabled = false;
            if (serverStatusText != null) serverStatusText.text += " QR no disponible; escribe la dirección mostrada.";
            return;
        }

        const int scale = 8;
        const int quietZone = 4;
        int side = (modules.GetLength(0) + quietZone * 2) * scale;
        qrTexture = new Texture2D(side, side, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(0, 0, 0, 255);
        var pixels = new Color32[side * side];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
        int size = modules.GetLength(0);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (!modules[y, x]) continue;
            int px = (x + quietZone) * scale;
            int py = (size - 1 - y + quietZone) * scale;
            for (int dy = 0; dy < scale; dy++)
            for (int dx = 0; dx < scale; dx++) pixels[(py + dy) * side + px + dx] = black;
        }
        qrTexture.SetPixels32(pixels);
        qrTexture.Apply(false, false);
        qrSprite = Sprite.Create(qrTexture, new Rect(0, 0, side, side), new Vector2(0.5f, 0.5f), 100f);
        qrImage.sprite = qrSprite;
        qrImage.enabled = true;
    }

    private void OnDestroy()
    {
        if (qrSprite != null) Destroy(qrSprite);
        if (qrTexture != null) Destroy(qrTexture);
    }
}
