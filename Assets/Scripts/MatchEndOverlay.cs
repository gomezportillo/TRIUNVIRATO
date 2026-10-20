using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Shows the scene-authored end screen and restarts the shared match.</summary>
public sealed class MatchEndOverlay : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private GameObject overlay;
    [SerializeField] private Text message;
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

        if (shouldShow == isShowing)
            return;

        // if (shouldShow && message != null)
        //    message.text = game.CastleHealth <= 0f ? "PARTIDA TERMINADA\nEl castillo ha caído" : "PARTIDA TERMINADA";

        SetVisible(shouldShow);
    }

    private void RestartMatch()
    {
        if (game != null)
            game.RestartFinishedMatch();
    }

    private void SetVisible(bool visible)
    {
        isShowing = visible;
        if (overlay != null && overlay.activeSelf != visible)
            overlay.SetActive(visible);
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
