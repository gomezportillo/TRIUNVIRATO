using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates scene-authored HUD text from the live game and local server state.</summary>
public sealed class SceneGameHUD : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private LocalControlServer controlServer;
    [Header("Partida")]
    [SerializeField] private Text matchStatus;
    [SerializeField] private Text wave;
    [SerializeField] private Text enemies;
    [SerializeField] private Text castleHealth;
    [Header("Red y sala")]
    [SerializeField] private Text serverStatus;
    [SerializeField] private Text connectionAddress;
    [SerializeField] private Text roomStatus;
    [Header("Lobby")]
    [SerializeField] private GameObject lobbyOverlay;
    [Header("Experiencia individual")]
    [SerializeField] private GameObject experienceRoot;
    [SerializeField] private Image[] experienceBarFills = new Image[3];
    [SerializeField] private Text[] experienceLabels = new Text[3];
    [SerializeField] private Text[] experienceArmyLabels = new Text[3];

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
        bool showExperience = game != null && game.Status != TriunviratoGame.MatchStatus.WaitingForPlayers;
        if (experienceRoot != null && experienceRoot.activeSelf != showExperience)
            experienceRoot.SetActive(showExperience);
        if (game != null)
        {
            Set(matchStatus, "Partida: " + (game.IsPaused ? "Pausada" : game.Status.ToString()));
            Set(wave, "Oleada " + Mathf.Max(1, game.CurrentWave) + " / ∞");
            Set(enemies, "Enemigos restantes: " + game.EnemiesRemainingInWave);
            Set(castleHealth, "Castillo: " + Mathf.CeilToInt(game.CastleHealth) + " / " + Mathf.CeilToInt(game.CastleMaximumHealth));
            for (int i = 0; i < 3; i++)
            {
                ArmyType army = (ArmyType)i;
                if (experienceBarFills != null && i < experienceBarFills.Length && experienceBarFills[i] != null)
                    experienceBarFills[i].fillAmount = game.GetExperienceFraction(army);
                if (experienceLabels != null && i < experienceLabels.Length)
                    Set(experienceLabels[i], "Nv. " + game.GetLevel(army) + " · " + Mathf.FloorToInt(game.GetExperience(army)) + " / " + Mathf.CeilToInt(game.GetExperienceNeeded(army)));
                if (experienceArmyLabels != null && i < experienceArmyLabels.Length)
                    Set(experienceArmyLabels[i], army.ToString());
            }
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
        if (target == null) return;
        if (target.text != value) target.text = value;
    }
}
