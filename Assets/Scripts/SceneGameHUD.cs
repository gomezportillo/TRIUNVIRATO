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
        if (economy != null)
        {
            Set(airCoins, "🔴  " + economy.GetCoins(ArmyType.Aire) + "  ·  " + economy.GetPassiveIncomePerSecond(ArmyType.Aire).ToString("0.##") + "/s");
            Set(earthCoins, "🟢  " + economy.GetCoins(ArmyType.Tierra) + "  ·  " + economy.GetPassiveIncomePerSecond(ArmyType.Tierra).ToString("0.##") + "/s");
            Set(seaCoins, "🔵  " + economy.GetCoins(ArmyType.Mar) + "  ·  " + economy.GetPassiveIncomePerSecond(ArmyType.Mar).ToString("0.##") + "/s");
        }

        if (game != null)
        {
            Set(matchStatus, "Partida: " + (game.IsPaused ? "Pausada" : game.Status.ToString()));
            Set(wave, "🌊  Oleada " + Mathf.Max(1, game.CurrentWave) + " / ∞");
            Set(enemies, "👾  Enemigos restantes: " + game.EnemiesRemainingInWave);
            Set(castleHealth, "❤️  Castillo: " + Mathf.CeilToInt(game.CastleHealth) + " / " + Mathf.CeilToInt(game.CastleMaximumHealth));
        }

        if (controlServer != null)
        {
            Set(serverStatus, "Red: " + controlServer.StatusText);
            Set(connectionAddress, controlServer.AccessUrlText);
            Set(roomStatus, controlServer.RoomStatusText);
        }
    }

    private static void Set(Text target, string value)
    {
        if (target != null && target.text != value)
            target.text = value;
    }
}
