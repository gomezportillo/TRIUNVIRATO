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
    internal void Configure(TriunviratoGame owner, TowerConfig config)
    {
        game = owner;
        Config = config;
        if (config == null || config.Hero == null)
            return;

        RefreshIntermissionStats();
        SplashRadius = config.Hero.SplashRadius;
        attackTimer = 0f;
    }

    internal void RefreshIntermissionDamage() => RefreshIntermissionStats();

    internal void RefreshIntermissionStats()
    {
        if (Config == null || Config.Hero == null) return;
        Range = Config.Hero.Range * (game != null ? game.GetTowerRangeMultiplier(Config.Army) : 1f) +
                (game != null ? game.GetTowerRangeFixedBonus(Config.Army) : 0f);
        float attackRate = (Config.Hero.AttackCooldown > 0f ? 1f / Config.Hero.AttackCooldown : 0f) *
                           (game != null ? game.GetTowerAttackSpeedMultiplier(Config.Army) : 1f) +
                           (game != null ? game.GetTowerAttackRateFixedBonus(Config.Army) : 0f);
        Cooldown = attackRate > 0f ? 1f / attackRate : Config.Hero.AttackCooldown;
        RefreshDamage();
    }

    private void RefreshDamage()
    {
        if (Config != null)
            Damage = game != null ? game.GetTowerDamage(Config.Army, Config.GetBaseDamage()) : Config.GetBaseDamage();
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
