using UnityEngine;

[CreateAssetMenu(fileName = "HeroClone", menuName = "TRIUNVIRATO/Abilities/Hero Clone")]
public sealed class HeroCloneAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(0.1f)] private float duration = 5f;
    [SerializeField, Min(0f)] private float durationUpgradeSeconds = 1f;
    [SerializeField, Min(0f)] private float cooldownReductionSeconds = 3f;
    [SerializeField, Min(0.1f)] private float minimumCooldown = 5f;
    [SerializeField, Min(0f)] private float horizontalSeparation = 1f;
    [SerializeField, Min(0f), Tooltip("Retraso entre activar la habilidad y crear el clon, para sincronizarlo con Attack_Special.")] private float spawnDelay = 1f;
    [SerializeField, Range(0f, 1f)] private float tintAlpha = 0.65f;
    [SerializeField] private Color tint = new Color(1f, 0.32f, 0.32f, 1f);

    public float Duration => duration;
    public float DurationUpgradeSeconds => durationUpgradeSeconds;
    public float CooldownReductionSeconds => cooldownReductionSeconds;
    public float MinimumCooldown => minimumCooldown;
    public float HorizontalSeparation => horizontalSeparation;
    public float SpawnDelay => spawnDelay;
    public float TintAlpha => tintAlpha;
    public Color Tint => tint;
}
