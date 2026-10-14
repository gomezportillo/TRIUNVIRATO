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

        Range = config.Hero.Range;
        Damage = config.GetDamageAtUpgradeLevel(0);
        Cooldown = config.Hero.AttackCooldown;
        SplashRadius = config.Hero.SplashRadius;
        attackTimer = 0f;
    }

    internal void SetUpgradeLevel(int level)
    {
        if (Config != null)
            Damage = Config.GetDamageAtUpgradeLevel(level);
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
