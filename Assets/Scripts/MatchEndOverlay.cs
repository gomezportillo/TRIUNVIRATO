using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Shows the TV end screen and provides its direct restart action.</summary>
public sealed class MatchEndOverlay : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private LocalControlServer controlServer;
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject victoryOverlay;
    [SerializeField] private Text confirmedPlayersText;
    [SerializeField] private Text victoryConfirmedPlayersText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button victoryRestartButton;

    private bool isShowing;
    private bool isVictoryShowing;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartMatch);
        if (victoryRestartButton != null)
            victoryRestartButton.onClick.AddListener(RestartMatch);

        EnsureUiInputSystem();
        SetVisible(false);
        SetVictoryVisible(false);
    }

    private void Update()
    {
        bool matchFinished = game != null && game.Status == TriunviratoGame.MatchStatus.Finished;
        bool shouldShowVictory = matchFinished && game.IsVictory;
        bool shouldShowDefeat = matchFinished && !game.IsVictory;

        if (matchFinished)
        {
            int confirmed = controlServer != null ? controlServer.RestartRequestCount : 0;
            string message = "Jugadores confirmados " + confirmed + "/3";
            if (confirmedPlayersText != null) confirmedPlayersText.text = message;
            if (victoryConfirmedPlayersText != null) victoryConfirmedPlayersText.text = message;
        }

        if (shouldShowDefeat != isShowing) SetVisible(shouldShowDefeat);
        if (shouldShowVictory != isVictoryShowing) SetVictoryVisible(shouldShowVictory);
    }

    private void SetVisible(bool visible)
    {
        isShowing = visible;
        if (restartButton != null)
            restartButton.interactable = visible;
        if (overlay != null && overlay.activeSelf != visible)
            overlay.SetActive(visible);
    }

    private void SetVictoryVisible(bool visible)
    {
        isVictoryShowing = visible;
        if (victoryRestartButton != null)
            victoryRestartButton.interactable = visible;
        if (victoryOverlay != null && victoryOverlay.activeSelf != visible)
            victoryOverlay.SetActive(visible);
    }

    private void RestartMatch()
    {
        if (game != null && game.Status == TriunviratoGame.MatchStatus.Finished)
            game.RestartFinishedMatch();
    }

    private static void EnsureUiInputSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }
}
