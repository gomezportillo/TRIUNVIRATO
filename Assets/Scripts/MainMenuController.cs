using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Wires the scene-authored main menu controls to their actions.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Pantallas")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Navegación")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("Opciones")]
    [SerializeField] private Button vSyncButton;
    [SerializeField] private Text vSyncLabel;
    [SerializeField] private Button crtButton;
    [SerializeField] private Text crtLabel;
    [SerializeField] private Button musicButton;
    [SerializeField] private Text musicLabel;
    [SerializeField] private GameObject crtScanlineOverlay;

    private bool crtEnabled = true;

    private void Awake()
    {
        QualitySettings.vSyncCount = 1;
        DontDestroyOnLoad(crtScanlineOverlay.transform.root.gameObject);
        crtScanlineOverlay.SetActive(crtEnabled);
        startButton.onClick.AddListener(StartGame);
        optionsButton.onClick.AddListener(() => Show(optionsPanel));
        creditsButton.onClick.AddListener(() => Show(creditsPanel));
        exitButton.onClick.AddListener(Quit);
        optionsBackButton.onClick.AddListener(() => Show(mainPanel));
        creditsBackButton.onClick.AddListener(() => Show(mainPanel));
        vSyncButton.onClick.AddListener(ToggleVSync);
        crtButton.onClick.AddListener(ToggleCrt);
        if (musicButton != null)
            musicButton.onClick.AddListener(ToggleMusic);
        RefreshOptionLabels();
        Show(mainPanel);
    }

    private void Update() => RefreshMusicLabel();

    private void StartGame() => SceneManager.LoadScene("MainGame");

    private void Show(GameObject selected)
    {
        mainPanel.SetActive(selected == mainPanel);
        optionsPanel.SetActive(selected == optionsPanel);
        creditsPanel.SetActive(selected == creditsPanel);
    }

    private void ToggleVSync()
    {
        QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
        RefreshOptionLabels();
    }

    private void ToggleCrt()
    {
        crtEnabled = !crtEnabled;
        crtScanlineOverlay.SetActive(crtEnabled);
        RefreshOptionLabels();
    }

    private void ToggleMusic()
    {
        if (GameSoundManager.Instance == null)
            return;

        GameSoundManager.Instance.ToggleThemeMute();
        RefreshMusicLabel();
    }

    private void RefreshOptionLabels()
    {
        vSyncLabel.text = QualitySettings.vSyncCount == 0 ? "DESACTIVADO" : "ACTIVADO";
        crtLabel.text = crtEnabled ? "ACTIVADO" : "DESACTIVADO";
        RefreshMusicLabel();
    }

    private void RefreshMusicLabel()
    {
        if (musicLabel == null)
            return;

        GameSoundManager soundManager = GameSoundManager.Instance;
        musicLabel.text = soundManager == null || !soundManager.IsThemeMuted ? "ACTIVADA" : "SILENCIADA";
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
