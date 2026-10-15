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
    [Header("Mejora global del ejército")]
    [SerializeField, Min(0)] private int armyUpgradeBaseCost = 20;
    [SerializeField, Min(1f)] private float armyUpgradeCostMultiplier = 2f;
    [SerializeField, Min(0f)] private float armyDamageIncreasePerLevel = 0.1f;
    [SerializeField, Min(0)] private int armyUpgradeMaximumLevel = 5;

    public string Id => id;
    public string DisplayName => displayName;
    public ArmyType Army => army;
    public HeroConfig Hero => hero;
    public int PurchaseCost => purchaseCost;
    public float HeroDamageMultiplier => heroDamageMultiplier;
    public float UpgradeDamagePercent => upgradeDamagePercent;
    public int MaximumUpgradeLevel => maximumUpgradeLevel;
    public int ArmyUpgradeBaseCost => armyUpgradeBaseCost;
    public float ArmyUpgradeCostMultiplier => armyUpgradeCostMultiplier;
    public float ArmyDamageIncreasePerLevel => armyDamageIncreasePerLevel;
    public int ArmyUpgradeMaximumLevel => armyUpgradeMaximumLevel;

    public float GetBaseDamage() => hero != null ? hero.Damage * heroDamageMultiplier : 0f;

    public float GetDamageAtUpgradeLevel(int level)
    {
        int appliedLevels = maximumUpgradeLevel > 0 ? Mathf.Clamp(level, 0, maximumUpgradeLevel) : Mathf.Max(0, level);
        return GetBaseDamage() * Mathf.Pow(1f + upgradeDamagePercent, appliedLevels);
    }

    public int GetNextUpgradeCost(int currentCost)
    {
        return UpgradePrice.GetCost(currentCost, upgradeCostGrowthRate, minimumUpgradeCostIncrease, 1);
    }

    public bool CanUpgrade(int currentLevel) =>
        maximumUpgradeLevel <= 0 || currentLevel < maximumUpgradeLevel;

    public float GetArmyDamageMultiplier(int level) =>
        Mathf.Pow(1f + armyDamageIncreasePerLevel, Mathf.Clamp(level, 0, armyUpgradeMaximumLevel));

    public float GetDamageAtLevels(int individualLevel, int armyLevel) =>
        GetDamageAtUpgradeLevel(individualLevel) * GetArmyDamageMultiplier(armyLevel);

    public int GetArmyUpgradeCost(int currentLevel) =>
        Mathf.CeilToInt(armyUpgradeBaseCost * Mathf.Pow(armyUpgradeCostMultiplier, Mathf.Max(0, currentLevel)));
}
