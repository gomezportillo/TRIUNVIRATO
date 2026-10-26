using System;
using System.Globalization;
using UnityEngine;

public enum UpgradeCategory { Progression, Towers, Hero, SpecialAbilities }
public enum UpgradeValueType { Percentage, Fixed }
public enum UpgradeEffect
{
    TowerDamage = 3, TowerAttackSpeed = 4, TowerRange = 5,
    HeroDamage = 6, HeroAttackSpeed = 7, HeroRange = 8, HeroSplash = 9,
    ExperienceMultiplier = 10, Luck = 11, UnlockTower = 12, HeroBossDamage = 13
}

/// <summary>Inspector-authored progression upgrades and rarity settings.</summary>
[CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "TRIUNVIRATO/Upgrades/Upgrade Catalog")]
public sealed class UpgradeCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Definition
    {
        public string id;
        public string displayName;
        public UpgradeCategory category;
        public UpgradeEffect effect;
        public UpgradeValueType valueType;
        [Min(0f)] public float normalEffect;
        [Min(0f)] public float rareEffect;
        [Min(0f)] public float legendaryEffect;
        public ArmyType[] eligibleArmies = Array.Empty<ArmyType>();
    }

    [SerializeField, Range(0f, 1f)] private float rareOfferChance = 0.10f;
    [SerializeField, Range(0f, 1f)] private float luckRareChanceIncrement = 0.10f;
    [Header("Habilidades especiales (solo tras jefe)")]
    [SerializeField] private bool specialAbilityOffersAfterBoss = true;
    [SerializeField] private Definition[] definitions = Array.Empty<Definition>();

    public float RareOfferChance => rareOfferChance;
    public float LuckRareChanceIncrement => luckRareChanceIncrement;
    public bool SpecialAbilityOffersAfterBoss => specialAbilityOffersAfterBoss;
    public Definition[] Definitions => definitions;

    public Definition Find(string id)
    {
        if (definitions == null || string.IsNullOrEmpty(id)) return null;
        foreach (Definition definition in definitions)
            if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal)) return definition;
        return null;
    }

    public static float GetOfferEffect(Definition definition, int stars) =>
        definition == null ? 0f : stars >= 3 ? definition.legendaryEffect : stars == 2 ? definition.rareEffect : definition.normalEffect;

    public static string FormatDescription(Definition definition, int stars, float luckChanceIncrement = 0.10f)
    {
        if (definition == null) return string.Empty;
        float value = GetOfferEffect(definition, stars);
        if (definition.effect == UpgradeEffect.UnlockTower)
            return "Desbloquea la torre de " + (definition.eligibleArmies != null && definition.eligibleArmies.Length > 0
                ? definition.eligibleArmies[0].ToString() : "tu ejército");
        if (definition.effect == UpgradeEffect.Luck)
            return "Probabilidad de dos estrellas +" + (value * luckChanceIncrement * 100f).ToString("0.##", CultureInfo.GetCultureInfo("es-ES")) + " puntos porcentuales";
        string number = value.ToString("0.##", CultureInfo.GetCultureInfo("es-ES"));
        string amount = definition.valueType == UpgradeValueType.Percentage
            ? "+" + number + " %"
            : "+" + number + " " + GetUnit(definition.effect);
        return GetEffectLabel(definition.effect) + " " + amount;
    }

    private static string GetEffectLabel(UpgradeEffect effect)
    {
        switch (effect)
        {
            case UpgradeEffect.TowerDamage: return "Daño de torres";
            case UpgradeEffect.TowerAttackSpeed: return "Velocidad de ataque de torres";
            case UpgradeEffect.TowerRange: return "Alcance de torres";
            case UpgradeEffect.HeroDamage: return "Daño del héroe";
            case UpgradeEffect.HeroAttackSpeed: return "Velocidad de ataque del héroe";
            case UpgradeEffect.HeroRange: return "Alcance del héroe";
            case UpgradeEffect.HeroSplash: return "Área de impacto del héroe";
            case UpgradeEffect.ExperienceMultiplier: return "Multiplicador de experiencia";
            case UpgradeEffect.Luck: return "Suerte";
            case UpgradeEffect.UnlockTower: return "Desbloqueo de torre";
            case UpgradeEffect.HeroBossDamage: return "Daño del héroe contra jefes";
            default: return "Mejora";
        }
    }

    private static string GetUnit(UpgradeEffect effect)
    {
        switch (effect)
        {
            case UpgradeEffect.TowerDamage:
            case UpgradeEffect.HeroDamage: return "daño";
            case UpgradeEffect.TowerAttackSpeed:
            case UpgradeEffect.HeroAttackSpeed: return "ataques/s";
            case UpgradeEffect.TowerRange:
            case UpgradeEffect.HeroRange:
            case UpgradeEffect.HeroSplash: return "unidades";
            case UpgradeEffect.ExperienceMultiplier: return "puntos porcentuales";
            case UpgradeEffect.Luck: return "puntos porcentuales de probabilidad";
            case UpgradeEffect.HeroBossDamage: return "% de daño contra jefes";
            default: return "";
        }
    }
}
