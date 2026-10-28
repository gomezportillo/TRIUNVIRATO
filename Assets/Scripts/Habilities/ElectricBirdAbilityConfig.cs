using UnityEngine;

[CreateAssetMenu(fileName = "ElectricBird", menuName = "TRIUNVIRATO/Abilities/Electric Bird")]
public sealed class ElectricBirdAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0f), Tooltip("Espera antes de crear el proyectil para acompasarlo con la animación del héroe.")] private float launchDelay = 1f;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
    [SerializeField, Min(0f)] private float hitRadius = 0.25f;
    [SerializeField, Min(0.1f)] private float debuffDuration = 5f;
    [SerializeField, Range(0f, 0.99f)] private float slowPercent = 30f;
    [SerializeField, Min(1f)] private float vulnerabilityMultiplier = 2f;
    [SerializeField, Min(0f)] private float durationUpgradeSeconds = 1f;
    [SerializeField, Min(0f)] private float cooldownReductionSeconds = 3f;
    [SerializeField, Min(0f)] private float minimumCooldown = 5f;
    [SerializeField] private Color debuffTint = new Color(1f, 1f, 0.7f, 1f);

    public GameObject ProjectilePrefab => projectilePrefab;
    public float LaunchDelay => launchDelay;
    public float ProjectileSpeed => projectileSpeed;
    public float HitRadius => hitRadius;
    public float DebuffDuration => debuffDuration;
    public float SlowPercent => slowPercent;
    public float VulnerabilityMultiplier => vulnerabilityMultiplier;
    public float DurationUpgradeSeconds => durationUpgradeSeconds;
    public float CooldownReductionSeconds => cooldownReductionSeconds;
    public float MinimumCooldown => minimumCooldown;
    public Color DebuffTint => debuffTint;
}
