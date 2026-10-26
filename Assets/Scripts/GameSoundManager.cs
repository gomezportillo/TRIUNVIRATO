using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owns the match theme and the configurable one-shot sound effects.</summary>
public sealed class GameSoundManager : MonoBehaviour
{
    public static GameSoundManager Instance { get; private set; }

    [Header("Tema principal")]
    [SerializeField] private AudioClip[] themeTracks;
    [SerializeField, Range(0f, 1f)] private float themeVolume = 0.45f;

    [Header("Volumen general de efectos")]
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;

    [Header("Volumen individual de cada efecto")]
    [SerializeField] private AudioClip airAttack;
    [SerializeField, Range(0f, 1f)] private float airAttackVolume = 0.2f;
    [SerializeField] private AudioClip seaAttack;
    [SerializeField, Range(0f, 1f)] private float seaAttackVolume = 0.2f;
    [SerializeField] private AudioClip groundAttack;
    [SerializeField, Range(0f, 1f)] private float groundAttackVolume = 0.2f;
    [SerializeField] private AudioClip skeletonDeath;
    [SerializeField, Range(0f, 1f)] private float skeletonDeathVolume = 0.3f;
    [SerializeField] private AudioClip defeat;
    [SerializeField, Range(0f, 1f)] private float defeatVolume = 0.3f;
    [SerializeField] private AudioClip matchStart;
    [SerializeField, Range(0f, 1f)] private float matchStartVolume = 0.3f;
    [SerializeField] private AudioClip towerBuilt;
    [SerializeField, Range(0f, 1f)] private float towerBuiltVolume = 0.3f;

    private AudioSource themeSource;
    private AudioSource effectsSource;
    private bool themeMuted;
    private AudioClip currentThemeClip;
    private Coroutine themeFadeCoroutine;
    private bool playNextThemeAfterFade;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Hay más de un GameSoundManager en la escena; el duplicado se desactivará.", this);
            enabled = false;
            return;
        }

        Instance = this;
        AudioSource[] sources = GetComponents<AudioSource>();
        themeSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        effectsSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        ConfigureSource(themeSource, themeVolume, true);
        ConfigureSource(effectsSource, effectsVolume, false);
    }

    private void Start()
    {
        if (themeTracks == null || themeTracks.Length == 0)
        {
            Debug.LogWarning("GameSoundManager no tiene temas asignados en Theme.", this);
            return;
        }

        List<AudioClip> availableTracks = GetAvailableTracks();

        if (availableTracks.Count == 0)
        {
            Debug.LogWarning("No hay temas de audio válidos asignados.", this);
            return;
        }

        PlayTheme(ChooseTheme(availableTracks, null));
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            ToggleThemeMute();
    }

    public void ToggleThemeMute()
    {
        themeMuted = !themeMuted;
        if (themeSource != null)
            themeSource.mute = themeMuted;
        Debug.Log(themeMuted ? "Tema principal silenciado (M)." : "Tema principal activado (M).", this);
    }

    public void FadeOutTheme(float duration)
    {
        if (themeSource == null || !themeSource.isPlaying || themeFadeCoroutine != null)
            return;
        playNextThemeAfterFade = false;
        themeFadeCoroutine = StartCoroutine(FadeTheme(Mathf.Max(0.01f, duration)));
    }

    public void PlayNextMatchTheme()
    {
        if (themeSource == null)
            return;
        if (themeFadeCoroutine != null)
        {
            playNextThemeAfterFade = true;
            return;
        }
        List<AudioClip> availableTracks = GetAvailableTracks();
        if (availableTracks.Count == 0)
            return;
        PlayTheme(ChooseTheme(availableTracks, currentThemeClip));
    }

    private IEnumerator FadeTheme(float duration)
    {
        float startingVolume = themeSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            themeSource.volume = Mathf.Lerp(startingVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        themeSource.volume = 0f;
        themeSource.Stop();
        themeSource.volume = themeVolume;
        themeFadeCoroutine = null;
        if (playNextThemeAfterFade)
        {
            playNextThemeAfterFade = false;
            List<AudioClip> tracks = GetAvailableTracks();
            if (tracks.Count > 0)
                PlayTheme(ChooseTheme(tracks, currentThemeClip));
        }
    }

    private List<AudioClip> GetAvailableTracks()
    {
        List<AudioClip> tracks = new List<AudioClip>();
        if (themeTracks != null)
            foreach (AudioClip track in themeTracks)
                if (track != null && !tracks.Contains(track)) tracks.Add(track);
        return tracks;
    }

    private static AudioClip ChooseTheme(List<AudioClip> tracks, AudioClip exclude)
    {
        if (tracks.Count > 1 && exclude != null)
            tracks.RemoveAll(track => track == exclude);
        return tracks[Random.Range(0, tracks.Count)];
    }

    private void PlayTheme(AudioClip clip)
    {
        if (clip == null || themeSource == null)
            return;
        currentThemeClip = clip;
        themeSource.clip = clip;
        themeSource.loop = true;
        themeSource.volume = themeVolume;
        themeSource.mute = themeMuted;
        themeSource.Play();
    }

    public void PlayAttack(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Aire: PlayEffect(airAttack, airAttackVolume); break;
            case ArmyType.Mar: PlayEffect(seaAttack, seaAttackVolume); break;
            case ArmyType.Tierra: PlayEffect(groundAttack, groundAttackVolume); break;
        }
    }

    public void PlaySkeletonDeath() => PlayEffect(skeletonDeath, skeletonDeathVolume);
    public void PlayDefeat() => PlayEffect(defeat, defeatVolume);
    public void PlayMatchStart() => PlayEffect(matchStart, matchStartVolume);
    public void PlayTowerBuilt() => PlayEffect(towerBuilt, towerBuiltVolume);
    public void PlayAbilityEffect(AudioClip clip, int playCount)
    {
        if (clip != null && effectsSource != null)
            StartCoroutine(PlayAbilityEffectRepeatedly(clip, Mathf.Max(1, playCount)));
    }

    private IEnumerator PlayAbilityEffectRepeatedly(AudioClip clip, int playCount)
    {
        for (int i = 0; i < playCount; i++)
        {
            PlayEffect(clip, 1f);
            if (i < playCount - 1)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, clip.length));
        }
    }

    private void PlayEffect(AudioClip clip, float volume)
    {
        if (clip != null && effectsSource != null)
            effectsSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void ConfigureSource(AudioSource source, float volume, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
