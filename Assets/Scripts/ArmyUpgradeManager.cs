using System;
using System.Globalization;
using UnityEngine;

public enum ArmyUpgradeKind
{
    Hero,
    Towers,
    Income
}

/// <summary>Validates and applies army upgrades while keeping purchased levels in runtime state.</summary>
public sealed class ArmyUpgradeManager : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private TowerShop towerShop;
    [SerializeField] private ArmyEconomy economy;

    private readonly int[,] levels = new int[3, 3];

    [Serializable]
    public sealed class UpgradeOption
    {
        public string id;
        public string name;
        public int level;
        public int maximumLevel;
        public int nextCost;
        public string currentEffect;
        public string nextEffect;
        public bool canPurchase;
        public string blockedReason;
    }

    public int StateRevision { get; private set; }

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<TriunviratoGame>();
        if (towerShop == null) towerShop = FindAnyObjectByType<TowerShop>();
        if (economy == null) economy = FindAnyObjectByType<ArmyEconomy>();
    }

    public int GetLevel(ArmyType army, ArmyUpgradeKind kind)
    {
        int armyIndex = (int)army;
        int kindIndex = (int)kind;
        return armyIndex >= 0 && armyIndex < 3 && kindIndex >= 0 && kindIndex < 3
            ? levels[armyIndex, kindIndex] : 0;
    }

    public UpgradeOption GetOption(ArmyType army, ArmyUpgradeKind kind)
    {
        HeroConfig hero = game != null ? game.GetHeroConfig(army) : null;
        TowerConfig tower = towerShop != null ? towerShop.GetConfig(army) : null;
        EconomyConfig economyConfig = economy != null ? economy.Config : null;
        int level = GetLevel(army, kind);
        int maximum = GetMaximumLevel(army, kind, hero, tower, economyConfig);
        int cost = GetCost(army, kind, level, hero, tower, economyConfig);
        int coins = economy != null ? economy.GetCoins(army) : 0;
        bool hasConfig = maximum > 0 && cost > 0;
        string blockedReason = null;

        if (!hasConfig) blockedReason = "Configuración de mejora no disponible.";
        else if (game == null || !game.CanCombat) blockedReason = "La partida no está en curso.";
        else if (level >= maximum) blockedReason = "Nivel máximo alcanzado.";
        else if (coins < cost) blockedReason = "Saldo insuficiente: tienes " + coins + " y necesitas " + cost + ".";

        return new UpgradeOption
        {
            id = kind.ToString().ToLowerInvariant(),
            name = GetName(kind),
            level = level,
            maximumLevel = maximum,
            nextCost = cost,
            currentEffect = GetEffect(army, kind, level, hero, tower, economyConfig),
            nextEffect = GetEffect(army, kind, level + 1, hero, tower, economyConfig),
            canPurchase = blockedReason == null,
            blockedReason = blockedReason ?? string.Empty
        };
    }

    public bool TryPurchase(ArmyType army, ArmyUpgradeKind kind, out string message)
    {
        UpgradeOption option = GetOption(army, kind);
        if (!option.canPurchase)
        {
            message = option.blockedReason;
            Debug.LogWarning("Mejora rechazada: " + army + " / " + option.name + ". " + message, this);
            return false;
        }

        if (economy == null || !economy.TrySpend(army, option.nextCost))
        {
            message = "Saldo insuficiente.";
            Debug.LogWarning("Mejora rechazada: " + army + " / " + option.name + ". " + message, this);
            return false;
        }

        int nextLevel = option.level + 1;
        levels[(int)army, (int)kind] = nextLevel;
        ApplyLevel(army, kind, nextLevel);
        StateRevision++;
        message = option.name + " mejorada al nivel " + nextLevel + ". " + option.nextEffect;
        Debug.Log("Mejora aceptada: " + army + " / " + option.name + " nivel " + nextLevel +
                  ", coste " + option.nextCost + ", saldo " + economy.GetCoins(army) + ". " + option.nextEffect, this);
        return true;
    }

    public string GetIncomeSummary(ArmyType army)
    {
        EconomyConfig config = economy != null ? economy.Config : null;
        if (config == null || economy == null) return "Economía no configurada.";

        if (army == ArmyType.Tierra)
            return "Ingreso pasivo: " + F(economy.GetPassiveIncomePerSecond(army)) + " monedas/s.";
        if (army == ArmyType.Aire)
            return "Ingreso pasivo: " + F(economy.GetPassiveIncomePerSecond(army)) +
                   "/s; bajas: " + economy.GetEnemyKillReward(army) + " moneda(s).";
        return "Oleadas: " + economy.GetWaveReward(army) + " monedas; jefes: " +
               economy.GetBossReward(army) + " adicionales.";
    }

    public void ResetRunLevels()
    {
        for (int armyIndex = 0; armyIndex < 3; armyIndex++)
        {
            ArmyType army = (ArmyType)armyIndex;
            for (int kindIndex = 0; kindIndex < 3; kindIndex++)
                levels[armyIndex, kindIndex] = 0;
            if (game != null) game.SetHeroUpgradeLevel(army, 0);
            if (towerShop != null) towerShop.SetArmyUpgradeLevel(army, 0);
            if (economy != null) economy.SetIncomeUpgradeLevel(army, 0);
        }
        StateRevision++;
    }

    private void ApplyLevel(ArmyType army, ArmyUpgradeKind kind, int level)
    {
        if (kind == ArmyUpgradeKind.Hero) game.SetHeroUpgradeLevel(army, level);
        else if (kind == ArmyUpgradeKind.Towers) towerShop.SetArmyUpgradeLevel(army, level);
        else economy.SetIncomeUpgradeLevel(army, level);
    }

    private static int GetMaximumLevel(ArmyType army, ArmyUpgradeKind kind,
        HeroConfig hero, TowerConfig tower, EconomyConfig economyConfig)
    {
        if (kind == ArmyUpgradeKind.Hero) return hero != null ? hero.HeroUpgradeMaximumLevel : 0;
        if (kind == ArmyUpgradeKind.Towers) return tower != null ? tower.ArmyUpgradeMaximumLevel : 0;
        return economyConfig != null ? economyConfig.GetIncomeUpgradeMaximumLevel(army) : 0;
    }

    private static int GetCost(ArmyType army, ArmyUpgradeKind kind, int level,
        HeroConfig hero, TowerConfig tower, EconomyConfig economyConfig)
    {
        if (kind == ArmyUpgradeKind.Hero) return hero != null ? hero.GetHeroUpgradeCost(level) : 0;
        if (kind == ArmyUpgradeKind.Towers) return tower != null ? tower.GetArmyUpgradeCost(level) : 0;
        return economyConfig != null ? economyConfig.GetIncomeUpgradeCost(army, level) : 0;
    }

    private static string GetEffect(ArmyType army, ArmyUpgradeKind kind, int level,
        HeroConfig hero, TowerConfig tower, EconomyConfig config)
    {
        if (kind == ArmyUpgradeKind.Hero)
            return "Daño del héroe: " + F(hero != null ? hero.GetDamageAtUpgradeLevel(level) : 0f);
        if (kind == ArmyUpgradeKind.Towers)
            return "Multiplicador global de daño de torres: ×" +
                   F(tower != null ? tower.GetArmyDamageMultiplier(level) : 1f);

        if (config == null) return "Economía no configurada.";
        int currentLevel = Mathf.Max(0, level);
        if (army == ArmyType.Tierra)
            return "Ingreso pasivo: " + F(config.GetPassiveIncomePerSecond(army) *
                (1f + config.GetPassiveIncomeIncreasePerLevel(army) * currentLevel)) + " monedas/s";
        if (army == ArmyType.Aire)
            return "Recompensa por baja: " +
                   (config.GetEnemyKillReward(army) + config.GetEnemyKillRewardIncreasePerLevel(army) * currentLevel) + " moneda(s)";
        return "Oleada: " + (config.GetWaveReward(army) + config.GetWaveRewardIncreasePerLevel(army) * currentLevel) +
               "; jefe: " + (config.GetBossReward(army) + config.GetBossRewardIncreasePerLevel(army) * currentLevel);
    }

    private static string GetName(ArmyUpgradeKind kind) => kind == ArmyUpgradeKind.Hero ? "Potenciar héroe" :
        kind == ArmyUpgradeKind.Towers ? "Potenciar torres" : "Mejorar ingresos";

    private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
