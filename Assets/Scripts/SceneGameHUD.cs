using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates scene-authored HUD text from the live game, economy, and local server state.</summary>
public sealed class SceneGameHUD : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private LocalControlServer controlServer;
    [Header("Recursos")]
    [SerializeField] private Text airCoins;
    [SerializeField] private Text earthCoins;
    [SerializeField] private Text seaCoins;
    [Header("Partida")]
    [SerializeField] private Text matchStatus;
    [SerializeField] private Text wave;
    [SerializeField] private Text enemies;
    [SerializeField] private Text castleHealth;
    [Header("Red y sala")]
    [SerializeField] private Text serverStatus;
    [SerializeField] private Text connectionAddress;
    [SerializeField] private Text roomStatus;
    [Header("Paneles")]
    [SerializeField] private GameObject resourcesPanel;
    [Header("Lobby")]
    [SerializeField] private GameObject lobbyOverlay;
    [Header("Fase de compras")]
    [SerializeField] private GameObject purchaseOverlay;
    [SerializeField] private Text purchaseTimer;
    [SerializeField] private Text purchasePlayerStates;

    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
    private float refreshTimer;

    private void Start() => Refresh();

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
            return;
        refreshTimer = refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        bool showLobby = game == null || game.Status == TriunviratoGame.MatchStatus.WaitingForPlayers;
        if (lobbyOverlay != null && lobbyOverlay.activeSelf != showLobby)
            lobbyOverlay.SetActive(showLobby);
        if (resourcesPanel != null && resourcesPanel.activeSelf != !showLobby)
            resourcesPanel.SetActive(!showLobby);

        if (economy != null)
        {
            Set(airCoins, economy.GetCoins(ArmyType.Aire).ToString());
            Set(earthCoins, economy.GetCoins(ArmyType.Tierra).ToString());
            Set(seaCoins, economy.GetCoins(ArmyType.Mar).ToString());
        }

        if (game != null)
        {
            Set(matchStatus, "Partida: " + (game.IsPaused ? "Pausada" : game.Status.ToString()));
            Set(wave, "Oleada " + Mathf.Max(1, game.CurrentWave) + " / ∞");
            Set(enemies, "Enemigos restantes: " + game.EnemiesRemainingInWave);
            Set(castleHealth, "Castillo: " + Mathf.CeilToInt(game.CastleHealth) + " / " + Mathf.CeilToInt(game.CastleMaximumHealth));
            if (purchaseOverlay != null && purchaseOverlay.activeSelf != game.IsPurchasePhase)
                purchaseOverlay.SetActive(game.IsPurchasePhase);
            if (game.IsPurchasePhase)
            {
                Set(purchaseTimer, Mathf.CeilToInt(game.PurchasePhaseSecondsRemaining) + " s");
                Set(purchasePlayerStates, BuildPurchasePlayerStates());
            }
        }

        if (controlServer != null)
        {
            Set(serverStatus, "Red: " + controlServer.StatusText);
            Set(connectionAddress, controlServer.AccessUrlText);
            Set(roomStatus, controlServer.RoomStatusText);
        }
    }

    private string BuildPurchasePlayerStates()
    {
        string[] lines = new string[3];
        for (int i = 0; i < 3; i++)
        {
            ArmyType army = (ArmyType)i;
            string name = army == ArmyType.Tierra ? "Tierra" : army == ArmyType.Aire ? "Aire" : "Mar";
            lines[i] = name + ": " + (game.IsPurchaseChoiceMade(army) ? game.GetPurchaseResult(army) : "pendiente");
        }
        return string.Join("\n", lines);
    }

    private static void Set(Text target, string value)
    {
        if (target == null) return;
        if (target.text != value) target.text = value;
    }
}
