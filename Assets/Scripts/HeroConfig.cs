using UnityEngine;

/// <summary>Immutable design data for a hero and the shared combat values its tower inherits.</summary>
[CreateAssetMenu(fileName = "HeroConfig", menuName = "TRIUNVIRATO/Combat/Hero Config")]
public sealed class HeroConfig : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private ArmyType army;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0.01f)] private float attackCooldown = 1f;
    [SerializeField, Min(0f)] private float range = 1f;
    [SerializeField, Min(0f)] private float splashRadius;
    [SerializeField] private Color projectileColor = Color.white;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 10f;
    [SerializeField, Min(0.01f)] private float projectileLifetime = 1.5f;
    [SerializeField, Min(0f)] private float projectileDelay = 0.5f;
    [Header("Mejora del héroe")]
    [SerializeField, Min(0)] private int heroUpgradeBaseCost = 15;
    [SerializeField, Min(1f)] private float heroUpgradeCostMultiplier = 2f;
    [SerializeField, Min(0f)] private float heroDamageIncreasePerLevel = 0.15f;
    [SerializeField, Min(0)] private int heroUpgradeMaximumLevel = 5;

    public string Id => id;
    public string DisplayName => displayName;
    public ArmyType Army => army;
    public float Damage => damage;
    public float AttackCooldown => attackCooldown;
    public float Range => range;
    public float SplashRadius => splashRadius;
    public Color ProjectileColor => projectileColor;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileDelay => projectileDelay;
    public int HeroUpgradeBaseCost => heroUpgradeBaseCost;
    public float HeroUpgradeCostMultiplier => heroUpgradeCostMultiplier;
    public float HeroDamageIncreasePerLevel => heroDamageIncreasePerLevel;
    public int HeroUpgradeMaximumLevel => heroUpgradeMaximumLevel;

    public float GetDamageAtUpgradeLevel(int level) =>
        damage * Mathf.Pow(1f + heroDamageIncreasePerLevel, Mathf.Clamp(level, 0, heroUpgradeMaximumLevel));

    public int GetHeroUpgradeCost(int currentLevel) =>
        Mathf.CeilToInt(heroUpgradeBaseCost * Mathf.Pow(heroUpgradeCostMultiplier, Mathf.Max(0, currentLevel)));
}
