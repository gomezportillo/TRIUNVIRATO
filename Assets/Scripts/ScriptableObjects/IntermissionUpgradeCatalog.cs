using System;
using System.Globalization;
using UnityEngine;

public enum IntermissionUpgradeCategory { Economy, Towers, Hero, SpecialAbilities }
public enum IntermissionUpgradeValueType { Percentage, Fixed }
public enum IntermissionUpgradeEffect
{
    PassiveIncome, KillReward, WaveReward,
    TowerDamage, TowerAttackSpeed, TowerRange,
    HeroDamage, HeroAttackSpeed, HeroRange, HeroSplash
}

/// <summary>Inspector-authored definitions and shared price rules for between-wave offers.</summary>
[CreateAssetMenu(fileName = "IntermissionUpgradeCatalog", menuName = "TRIUNVIRATO/Upgrades/Intermission Catalog")]
public sealed class IntermissionUpgradeCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Definition
    {
        public string id;
        public string displayName;
        public IntermissionUpgradeCategory category;
        public IntermissionUpgradeEffect effect;
        public IntermissionUpgradeValueType valueType;
        [Min(1)] public int baseCost = 30;
        [Min(0f)] public float normalEffect;
        [Min(0f)] public float rareEffect;
    }

    [SerializeField, Min(0f)] private float priceGrowthPerWave = 0.10f;
    [SerializeField, Min(0f)] private float rareSecondaryCostPercent = 0.50f;
    [Header("Habilidades especiales (solo tras boss)")]
    [SerializeField, Min(0)] private int specialGreenCost = 15;
    [SerializeField, Min(0)] private int specialRedCost = 15;
    [SerializeField, Min(0)] private int specialBlueCost = 15;
    [SerializeField] private bool specialAbilityOffersAfterBoss = true;
    [SerializeField, Min(1)] private int specialAbilityStars = 3;
    [SerializeField] private Definition[] definitions = Array.Empty<Definition>();

    public float PriceGrowthPerWave => priceGrowthPerWave;
    public float RareSecondaryCostPercent => rareSecondaryCostPercent;
    public int[] SpecialAbilityBaseCosts => new[] { specialGreenCost, specialRedCost, specialBlueCost };
    public bool SpecialAbilityOffersAfterBoss => specialAbilityOffersAfterBoss;
    public int SpecialAbilityStars => specialAbilityStars;
    public Definition[] Definitions => definitions;

    public Definition Find(string id)
    {
        if (definitions == null || string.IsNullOrEmpty(id)) return null;
        foreach (Definition definition in definitions)
            if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal)) return definition;
        return null;
    }

    public static float GetOfferEffect(Definition definition, int stars) =>
        definition == null ? 0f : stars >= 2 ? definition.rareEffect : definition.normalEffect;

    public static string FormatDescription(Definition definition, int stars)
    {
        if (definition == null) return string.Empty;
        float value = GetOfferEffect(definition, stars);
        string number = value.ToString("0.##", CultureInfo.GetCultureInfo("es-ES"));
        string amount = definition.valueType == IntermissionUpgradeValueType.Percentage
            ? "+" + number + " %"
            : "+" + number + " " + GetUnit(definition.effect);
        return GetEffectLabel(definition.effect) + " " + amount;
    }

    private static string GetEffectLabel(IntermissionUpgradeEffect effect)
    {
        switch (effect)
        {
            case IntermissionUpgradeEffect.PassiveIncome: return "Generación pasiva";
            case IntermissionUpgradeEffect.KillReward: return "Recompensa por kill";
            case IntermissionUpgradeEffect.WaveReward: return "Recompensa de oleada";
            case IntermissionUpgradeEffect.TowerDamage: return "Daño de torres";
            case IntermissionUpgradeEffect.TowerAttackSpeed: return "Velocidad de ataque de torres";
            case IntermissionUpgradeEffect.TowerRange: return "Alcance de torres";
            case IntermissionUpgradeEffect.HeroDamage: return "Daño del héroe";
            case IntermissionUpgradeEffect.HeroAttackSpeed: return "Velocidad de ataque del héroe";
            case IntermissionUpgradeEffect.HeroRange: return "Alcance del héroe";
            case IntermissionUpgradeEffect.HeroSplash: return "Área de impacto del héroe";
            default: return "Mejora";
        }
    }

    private static string GetUnit(IntermissionUpgradeEffect effect)
    {
        switch (effect)
        {
            case IntermissionUpgradeEffect.PassiveIncome: return "recursos/s";
            case IntermissionUpgradeEffect.KillReward:
            case IntermissionUpgradeEffect.WaveReward: return "recursos";
            case IntermissionUpgradeEffect.TowerDamage:
            case IntermissionUpgradeEffect.HeroDamage: return "daño";
            case IntermissionUpgradeEffect.TowerAttackSpeed:
            case IntermissionUpgradeEffect.HeroAttackSpeed: return "ataques/s";
            case IntermissionUpgradeEffect.TowerRange:
            case IntermissionUpgradeEffect.HeroRange:
            case IntermissionUpgradeEffect.HeroSplash: return "unidades";
            default: return "";
        }
    }
}
