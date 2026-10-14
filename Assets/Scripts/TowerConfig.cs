using UnityEngine;

/// <summary>Design data for a tower purchase and its upgrade rules.</summary>
[CreateAssetMenu(fileName = "TowerConfig", menuName = "TRIUNVIRATO/Combat/Tower Config")]
public sealed class TowerConfig : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private ArmyType army;
    [SerializeField] private HeroConfig hero;
    [SerializeField, Min(0)] private int purchaseCost = 10;
    [SerializeField, Min(0f)] private float heroDamageMultiplier = 0.2f;
    [SerializeField, Min(0f)] private float upgradeDamagePercent = 0.01f;
    [SerializeField, Min(0f)] private float upgradeCostGrowthRate = 0.01f;
    [SerializeField, Min(1)] private int minimumUpgradeCostIncrease = 1;
    [SerializeField, Min(0)] private int maximumUpgradeLevel;

    public string Id => id;
    public string DisplayName => displayName;
    public ArmyType Army => army;
    public HeroConfig Hero => hero;
    public int PurchaseCost => purchaseCost;
    public float HeroDamageMultiplier => heroDamageMultiplier;
    public float UpgradeDamagePercent => upgradeDamagePercent;
    public int MaximumUpgradeLevel => maximumUpgradeLevel;

    public float GetBaseDamage() => hero != null ? hero.Damage * heroDamageMultiplier : 0f;

    public float GetDamageAtUpgradeLevel(int level)
    {
        int appliedLevels = maximumUpgradeLevel > 0 ? Mathf.Clamp(level, 0, maximumUpgradeLevel) : Mathf.Max(0, level);
        return GetBaseDamage() * Mathf.Pow(1f + upgradeDamagePercent, appliedLevels);
    }

    public int GetNextUpgradeCost(int currentCost)
    {
        int increase = Mathf.Max(minimumUpgradeCostIncrease,
            Mathf.CeilToInt(currentCost * upgradeCostGrowthRate));
        return currentCost + increase;
    }

    public bool CanUpgrade(int currentLevel) =>
        maximumUpgradeLevel <= 0 || currentLevel < maximumUpgradeLevel;
}
