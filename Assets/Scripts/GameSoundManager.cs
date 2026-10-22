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

        List<AudioClip> availableTracks = new List<AudioClip>();
        foreach (AudioClip track in themeTracks)
        {
            if (track != null)
                availableTracks.Add(track);
        }

        if (availableTracks.Count == 0)
        {
            Debug.LogWarning("No hay temas de audio válidos asignados.", this);
            return;
        }

        themeSource.clip = availableTracks[Random.Range(0, availableTracks.Count)];
        themeSource.loop = true;
        themeSource.Play();
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
    public void PlayAbilityEffect(AudioClip clip) => PlayEffect(clip, 1f);

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
