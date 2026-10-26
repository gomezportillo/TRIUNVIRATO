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
    [SerializeField] private Text confirmedPlayersText;
    [SerializeField] private Button restartButton;

    private bool isShowing;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartMatch);

        EnsureUiInputSystem();
        SetVisible(false);
    }

    private void Update()
    {
        bool shouldShow = game != null && game.Status == TriunviratoGame.MatchStatus.Finished;

        if (shouldShow && confirmedPlayersText != null)
        {
            int confirmed = controlServer != null ? controlServer.RestartRequestCount : 0;
            confirmedPlayersText.text = "Jugadores confirmados " + confirmed + "/3";
        }

        if (shouldShow == isShowing)
            return;

        SetVisible(shouldShow);
    }

    private void SetVisible(bool visible)
    {
        isShowing = visible;
        if (restartButton != null)
            restartButton.interactable = visible;
        if (overlay != null && overlay.activeSelf != visible)
            overlay.SetActive(visible);
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
