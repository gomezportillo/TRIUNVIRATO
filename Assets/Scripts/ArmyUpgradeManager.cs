using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum ArmyUpgradeKind
{
    Hero,
    Towers,
    Income
}

/// <summary>Authoritative validation and runtime levels for all non-tower-purchase upgrades.</summary>
public sealed class ArmyUpgradeManager : MonoBehaviour
{
    private const string HeroId = "hero";
    private const string IncomeId = "income";
    private const string HeroCostDiscountId = "earth_hero_cost_discount";
    private const string MarAttackSpeedId = "earth_mar_attack_speed";
    private const string AirAreaId = "air_area";
    private const string AirPassiveDoubleId = "earth_air_passive_double";
    private const string MarPassiveUnlockId = "earth_mar_passive_unlock";
    private const string AirMarShareId = "air_mar_income_share";
    private const string MarRangeId = "mar_range";
    private const string MarCastleDamageIncomeId = "mar_castle_damage_income";
    private const string TeamHeroDamageId = "mar_team_hero_damage";

    [SerializeField] private TriunviratoGame game;
    [SerializeField] private TowerShop towerShop;
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private ArmyUpgradeCatalog catalog;

    private readonly int[] heroLevels = new int[3];
    private readonly int[] incomeLevels = new int[3];
    private readonly Dictionary<string, int> specialLevels = new Dictionary<string, int>();

    [Serializable]
    public sealed class UpgradeOption
    {
        public string id;
        public string section;
        public string name;
        public string description;
        public int level;
        public int maximumLevel;
        public int nextCost;
        public string secondaryArmy;
        public int secondaryCost;
        public string currentEffect;
        public string nextEffect;
        public bool canPurchase;
        public string status;
        public string blockedReason;
    }

    public int StateRevision { get; private set; }
    public ArmyUpgradeCatalog Catalog => catalog;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<TriunviratoGame>();
        if (towerShop == null) towerShop = FindAnyObjectByType<TowerShop>();
        if (economy == null) economy = FindAnyObjectByType<ArmyEconomy>();
    }

    public int GetLevel(ArmyType army, ArmyUpgradeKind kind)
    {
        int index = (int)army;
        if (!IsValidArmy(army)) return 0;
        if (kind == ArmyUpgradeKind.Hero) return heroLevels[index];
        if (kind == ArmyUpgradeKind.Income) return incomeLevels[index];
        return 0;
    }

    public int GetLevel(ArmyType owner, string id)
    {
        if (id == HeroId) return GetLevel(owner, ArmyUpgradeKind.Hero);
        if (id == IncomeId) return GetLevel(owner, ArmyUpgradeKind.Income);
        return specialLevels.TryGetValue(id ?? string.Empty, out int level) ? level : 0;
    }

    public float GetSpecialEffectLevel(string id) =>
        FindDefinition(id) is ArmyUpgradeCatalog.Definition definition
            ? definition.effectPerLevel * GetLevel(definition.army, id) : 0f;

    public float GetTeamHeroDamageMultiplier()
    {
        return GetSpecialMultiplier(TeamHeroDamageId);
    }

    public float GetSpecialMultiplier(string id)
    {
        ArmyUpgradeCatalog.Definition definition = FindDefinition(id);
        return definition == null ? 1f : Mathf.Pow(1f + definition.effectPerLevel,
            Mathf.Clamp(GetLevel(definition.army, id), 0, definition.maximumLevel));
    }

    public UpgradeOption[] GetOptions(ArmyType army)
    {
        if (!IsValidArmy(army)) return Array.Empty<UpgradeOption>();
        List<UpgradeOption> options = new List<UpgradeOption>
        {
            GetOption(army, HeroId),
            GetOption(army, IncomeId)
        };
        if (catalog != null && catalog.Definitions != null)
        {
            foreach (ArmyUpgradeCatalog.Definition definition in catalog.Definitions)
            {
                if (definition != null && definition.army == army)
                    options.Add(GetOption(army, definition.id));
            }
        }
        return options.ToArray();
    }

    public UpgradeOption GetOption(ArmyType army, ArmyUpgradeKind kind) =>
        GetOption(army, kind == ArmyUpgradeKind.Hero ? HeroId : kind == ArmyUpgradeKind.Income ? IncomeId : "towers");

    public UpgradeOption GetOption(ArmyType army, string id)
    {
        HeroConfig hero = game != null ? game.GetHeroConfig(army) : null;
        EconomyConfig economyConfig = economy != null ? economy.Config : null;
        ArmyUpgradeCatalog.Definition definition = FindDefinition(id);
        int level = GetLevel(army, id);
        int maximum = id == HeroId ? (hero != null ? hero.HeroUpgradeMaximumLevel : 0) :
            id == IncomeId ? (economyConfig != null ? economyConfig.GetIncomeUpgradeMaximumLevel(army) : 0) :
            definition != null && definition.army == army ? definition.maximumLevel : 0;
        int cost = GetCost(army, id, level, hero, economyConfig, definition);
        int secondaryCost = definition != null ? catalog.GetSecondaryCost(definition, level) : 0;
        ArmyType secondaryArmy = definition != null ? definition.secondaryArmy : army;
        int coins = economy != null ? economy.GetCoins(army) : 0;
        int secondaryCoins = economy != null ? economy.GetCoins(secondaryArmy) : 0;

        string blockedReason = null;
        string status;
        if (maximum <= 0 || cost <= 0)
        {
            blockedReason = "Configuración de mejora no disponible.";
            status = "locked";
        }
        else if (level >= maximum)
        {
            blockedReason = "Nivel máximo alcanzado.";
            status = "max";
        }
        else if (game == null || !game.IsPurchasePhase)
        {
            blockedReason = "Disponible durante la fase de compras.";
            status = "waiting";
        }
        else if (coins < cost || secondaryCoins < secondaryCost)
        {
            blockedReason = BuildInsufficientFunds(army, cost, coins, secondaryArmy, secondaryCost, secondaryCoins);
            status = "insufficient";
        }
        else
        {
            status = "available";
        }

        string section = id == HeroId ? "hero" : id == IncomeId ? "resources" :
            definition != null && definition.section == ArmyUpgradeSection.Hero ? "hero" : "resources";
        string name = id == HeroId ? "Daño del héroe" : id == IncomeId ? "Ingresos principales" :
            definition != null ? definition.displayName : "Mejora no disponible";
        string description = id == HeroId
            ? "Aumenta el daño de ataque de tu héroe."
            : id == IncomeId ? GetIncomeDescription(army) : definition != null ? definition.description : string.Empty;

        return new UpgradeOption
        {
            id = id,
            section = section,
            name = name,
            description = description,
            level = level,
            maximumLevel = maximum,
            nextCost = cost,
            secondaryArmy = secondaryCost > 0 ? ArmyName(secondaryArmy).ToLowerInvariant() : string.Empty,
            secondaryCost = secondaryCost,
            currentEffect = GetEffect(army, id, level, hero, economyConfig, definition),
            nextEffect = GetEffect(army, id, Mathf.Min(maximum, level + 1), hero, economyConfig, definition),
            canPurchase = status == "available",
            status = status,
            blockedReason = blockedReason ?? string.Empty
        };
    }

    public bool TryPurchase(ArmyType army, ArmyUpgradeKind kind, out string message) =>
        TryPurchase(army, kind == ArmyUpgradeKind.Hero ? HeroId : kind == ArmyUpgradeKind.Income ? IncomeId : "towers", out message);

    public bool TryPurchase(ArmyType army, string id, out string message)
    {
        UpgradeOption option = GetOption(army, id);
        if (!option.canPurchase)
        {
            message = option.blockedReason;
            Debug.LogWarning("Mejora rechazada: " + army + " / " + option.name + ". " + message, this);
            return false;
        }

        bool spent = option.secondaryCost > 0
            ? economy.TrySpendBundle(army, option.nextCost, ParseArmy(option.secondaryArmy), option.secondaryCost)
            : economy != null && economy.TrySpend(army, option.nextCost);
        if (!spent)
        {
            message = "Saldo insuficiente para completar la compra.";
            Debug.LogWarning("Mejora rechazada: " + army + " / " + option.name + ". " + message, this);
            return false;
        }

        int nextLevel = option.level + 1;
        SetLevel(army, id, nextLevel);
        StateRevision++;
        message = option.name + " · nivel " + nextLevel + ". " + option.nextEffect;
        Debug.Log("Mejora aceptada: " + army + " / " + option.name + " nivel " + nextLevel +
                  ", coste " + option.nextCost + (option.secondaryCost > 0 ? " + " + option.secondaryCost : string.Empty) +
                  ", saldo " + economy.GetCoins(army) + ".", this);
        return true;
    }

    public string GetIncomeSummary(ArmyType army)
    {
        if (economy == null || economy.Config == null) return "Economía no configurada.";
        if (army == ArmyType.Tierra)
            return "Pasivo " + F(economy.GetPassiveIncomePerSecond(army)) + " 🟢/s";
        if (army == ArmyType.Aire)
            return "Pasivo " + F(economy.GetPassiveIncomePerSecond(army)) + " 🔴/s · " + economy.GetEnemyKillReward(army) + " 🔴 por baja";
        return "Pasivo " + F(economy.GetPassiveIncomePerSecond(army)) + " 🔵/s · " + economy.GetWaveReward(army) +
               " 🔵/oleada · " + economy.GetBossReward(army) + " 🔵/jefe";
    }

    public void ResetRunLevels()
    {
        Array.Clear(heroLevels, 0, heroLevels.Length);
        Array.Clear(incomeLevels, 0, incomeLevels.Length);
        specialLevels.Clear();
        if (catalog != null && catalog.Definitions != null)
        {
            foreach (ArmyUpgradeCatalog.Definition definition in catalog.Definitions)
            {
                if (definition != null && !string.IsNullOrEmpty(definition.id))
                    specialLevels[definition.id] = 0;
            }
        }
        for (int i = 0; i < 3; i++)
        {
            ArmyType army = (ArmyType)i;
            towerShop?.SetArmyUpgradeLevel(army, 0);
        }
        StateRevision++;
    }

    private void SetLevel(ArmyType army, string id, int level)
    {
        if (id == HeroId) heroLevels[(int)army] = level;
        else if (id == IncomeId) incomeLevels[(int)army] = level;
        else specialLevels[id] = level;
    }

    private int GetCost(ArmyType army, string id, int level, HeroConfig hero, EconomyConfig economyConfig,
        ArmyUpgradeCatalog.Definition definition)
    {
        if (id == HeroId && hero != null)
            return ApplyHeroCostDiscount(hero.GetHeroUpgradeCost(level));
        if (id == IncomeId && economyConfig != null)
            return economyConfig.GetIncomeUpgradeCost(army, level);
        if (definition == null || definition.army != army) return 0;
        int cost = catalog.GetCost(definition, level);
        return definition.section == ArmyUpgradeSection.Hero && definition.id != HeroCostDiscountId
            ? ApplyHeroCostDiscount(cost) : cost;
    }

    private int ApplyHeroCostDiscount(int cost)
    {
        ArmyUpgradeCatalog.Definition discount = FindDefinition(HeroCostDiscountId);
        int level = GetLevel(ArmyType.Tierra, HeroCostDiscountId);
        float reduction = discount != null ? Mathf.Clamp01(discount.effectPerLevel * level) : 0f;
        return Mathf.Max(1, Mathf.CeilToInt(cost * (1f - reduction)));
    }

    private string GetEffect(ArmyType army, string id, int level, HeroConfig hero, EconomyConfig economyConfig,
        ArmyUpgradeCatalog.Definition definition)
    {
        if (id == HeroId)
        {
            float teamMultiplier = GetTeamHeroMultiplierAtLevel(GetLevel(ArmyType.Mar, TeamHeroDamageId));
            return "Daño " + F(hero != null ? hero.GetDamageAtUpgradeLevel(level) * teamMultiplier : 0f);
        }
        if (id == IncomeId)
        {
            if (economyConfig == null) return "Economía no configurada";
            if (army == ArmyType.Tierra)
                return "Pasivo " + F(economyConfig.GetPassiveIncomePerSecond(army) *
                    (1f + economyConfig.GetPassiveIncomeIncreasePerLevel(army) * level)) + " 🟢/s";
            if (army == ArmyType.Aire)
            {
                float passiveMultiplier = GetLevel(ArmyType.Tierra, AirPassiveDoubleId) > 0 ? 2f : 1f;
                float passive = economyConfig.GetPassiveIncomePerSecond(army) *
                                (1f + economyConfig.GetPassiveIncomeIncreasePerLevel(army) * level) * passiveMultiplier;
                return "Pasivo " + F(passive) + " 🔴/s · baja " +
                       (economyConfig.GetEnemyKillReward(army) + economyConfig.GetEnemyKillRewardIncreasePerLevel(army) * level) + " 🔴";
            }
            float marPassive = economyConfig.GetPassiveIncomePerSecond(army) + GetSpecialEffectLevel(MarPassiveUnlockId);
            int waveReward = economyConfig.GetWaveReward(army) + economyConfig.GetWaveRewardIncreasePerLevel(army) * level;
            int earlyWaveReward = Mathf.CeilToInt(waveReward * 1.5f);
            return "Oleada " + waveReward + " 🔵 · anticipada " + earlyWaveReward + " 🔵 · jefe " +
                   (economyConfig.GetBossReward(army) + economyConfig.GetBossRewardIncreasePerLevel(army) * level) +
                   " 🔵 · pasivo " + F(marPassive) + " 🔵/s";
        }
        if (definition == null) return "No disponible";

        float effect = definition.effectPerLevel * level;
        switch (id)
        {
            case HeroCostDiscountId: return "Mejoras de héroe −" + F(effect * 100f) + "%";
            case MarAttackSpeedId:
                HeroConfig marHero = game != null ? game.GetHeroConfig(ArmyType.Mar) : null;
                float marCooldown = marHero != null ? marHero.AttackCooldown : 0f;
                return "Cadencia de Mar " + F(marCooldown > 0f ? (1f / marCooldown) * Mathf.Pow(1f + definition.effectPerLevel, level) : 0f) + "/s";
            case AirAreaId:
                HeroConfig airHero = game != null ? game.GetHeroConfig(ArmyType.Aire) : null;
                float radius = airHero != null ? airHero.SplashRadius : 0f;
                return "Radio de área " + F(radius * Mathf.Pow(1f + definition.effectPerLevel, level));
            case AirPassiveDoubleId:
                float airPassive = economy != null && economy.Config != null
                    ? economy.Config.GetPassiveIncomePerSecond(ArmyType.Aire) *
                      (1f + economy.Config.GetPassiveIncomeIncreasePerLevel(ArmyType.Aire) * economy.GetIncomeUpgradeLevel(ArmyType.Aire)) *
                      (level > 0 ? definition.effectPerLevel : 1f)
                    : 0f;
                return "Pasivo de Aire " + F(airPassive) + " 🔴/s";
            case MarPassiveUnlockId:
                float marPassive = economy != null && economy.Config != null
                    ? economy.Config.GetPassiveIncomePerSecond(ArmyType.Mar) + (level > 0 ? definition.effectPerLevel : 0f)
                    : (level > 0 ? definition.effectPerLevel : 0f);
                return "Pasivo de Mar " + F(marPassive) + " 🔵/s";
            case AirMarShareId: return level > 0 ? "Aire recibe 1 🔴 cada 2 🔵 de Mar" : "Sinergia todavía bloqueada";
            case MarRangeId:
                HeroConfig rangeHero = game != null ? game.GetHeroConfig(ArmyType.Mar) : null;
                float range = rangeHero != null ? rangeHero.Range : 0f;
                return "Alcance " + F(range * Mathf.Pow(1f + definition.effectPerLevel, level));
            case MarCastleDamageIncomeId: return level > 0 ? "Mar recibe 1 🔵 por punto de daño real" : "Recompensa todavía bloqueada";
            case TeamHeroDamageId: return "Daño de los tres héroes ×" + F(GetTeamHeroMultiplierAtLevel(level));
            default: return definition.description;
        }
    }

    private float GetTeamHeroMultiplierAtLevel(int level)
    {
        ArmyUpgradeCatalog.Definition definition = FindDefinition(TeamHeroDamageId);
        return definition == null ? 1f : Mathf.Pow(1f + definition.effectPerLevel, Mathf.Clamp(level, 0, definition.maximumLevel));
    }

    private ArmyUpgradeCatalog.Definition FindDefinition(string id) => catalog != null ? catalog.Find(id) : null;

    private string BuildInsufficientFunds(ArmyType primaryArmy, int primaryCost, int primaryCoins,
        ArmyType secondaryArmy, int secondaryCost, int secondaryCoins)
    {
        List<string> needs = new List<string>();
        if (primaryCoins < primaryCost)
            needs.Add("faltan " + (primaryCost - primaryCoins) + " " + CurrencyIcon(primaryArmy));
        if (secondaryCost > 0 && secondaryCoins < secondaryCost)
            needs.Add("faltan " + (secondaryCost - secondaryCoins) + " " + CurrencyIcon(secondaryArmy));
        return "Recursos insuficientes: " + string.Join(" y ", needs) + ".";
    }

    private static string GetIncomeDescription(ArmyType army) => army == ArmyType.Tierra
        ? "Aumenta el ingreso pasivo de Tierra."
        : army == ArmyType.Aire ? "Aumenta las monedas recibidas por cada baja."
        : "Aumenta las recompensas de oleada, jefe y llamada anticipada.";

    private static ArmyType ParseArmy(string army) => army == "tierra" ? ArmyType.Tierra :
        army == "aire" ? ArmyType.Aire : ArmyType.Mar;
    private static string ArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" : army == ArmyType.Aire ? "Aire" : "Mar";
    private static string CurrencyIcon(ArmyType army) => army == ArmyType.Tierra ? "🟢" : army == ArmyType.Aire ? "🔴" : "🔵";
    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
    private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
