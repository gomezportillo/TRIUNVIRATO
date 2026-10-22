using UnityEngine;

/// <summary>Common editable presentation and cooldown settings for a hero special ability.</summary>
public abstract class HeroSpecialAbilityConfig : ScriptableObject
{
    [SerializeField] private string abilityName = "Special Ability";
    [SerializeField, TextArea(2, 4)] private string description = string.Empty;
    [SerializeField, Min(0.1f)] private float cooldown = 20f;
    [SerializeField] private RuntimeAnimatorController effectController;
    [SerializeField, Min(0.01f)] private float effectScale = 1f;
    [SerializeField] private AudioClip activationSound;
    [SerializeField, Min(1)] private int activationSoundPlayCount = 1;

    public string AbilityName => abilityName;
    public string Description => description;
    public float Cooldown => cooldown;
    public RuntimeAnimatorController EffectController => effectController;
    public float EffectScale => effectScale;
    public AudioClip ActivationSound => activationSound;
    public int ActivationSoundPlayCount => activationSoundPlayCount;
}
