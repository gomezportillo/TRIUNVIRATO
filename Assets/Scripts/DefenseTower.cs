using UnityEngine;

/// <summary>Attack cadence and stats for a purchased tower; hits use the shared game projectile pipeline.</summary>
public sealed class DefenseTower : MonoBehaviour
{
    public TowerConfig Config { get; private set; }
    public ArmyType Army => Config != null ? Config.Army : default;
    public float Range { get; private set; }
    public float Damage { get; private set; }
    public float Cooldown { get; private set; }
    public float SplashRadius { get; private set; }

    private TriunviratoGame game;
    private float attackTimer;
    private int individualUpgradeLevel;
    private int armyUpgradeLevel;

    internal void Configure(TriunviratoGame owner, TowerConfig config, int individualLevel, int globalArmyLevel)
    {
        game = owner;
        Config = config;
        if (config == null || config.Hero == null)
            return;

        Range = config.Hero.Range;
        individualUpgradeLevel = Mathf.Max(0, individualLevel);
        armyUpgradeLevel = Mathf.Max(0, globalArmyLevel);
        RefreshDamage();
        Cooldown = config.Hero.AttackCooldown;
        SplashRadius = config.Hero.SplashRadius;
        attackTimer = 0f;
    }

    internal void SetUpgradeLevel(int level)
    {
        individualUpgradeLevel = Mathf.Max(0, level);
        RefreshDamage();
    }

    internal void SetArmyUpgradeLevel(int level)
    {
        armyUpgradeLevel = Mathf.Max(0, level);
        RefreshDamage();
    }

    private void RefreshDamage()
    {
        if (Config != null)
            Damage = Config.GetDamageAtLevels(individualUpgradeLevel, armyUpgradeLevel);
    }

    private void Update()
    {
        if (game == null || Config == null || Config.Hero == null || !game.CanCombat)
            return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && game.TryTowerAttack(this))
            attackTimer = Cooldown;
    }
}
