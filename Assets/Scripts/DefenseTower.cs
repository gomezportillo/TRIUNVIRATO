using UnityEngine;

/// <summary>Attack cadence and stats for a purchased tower; hits use the shared game projectile pipeline.</summary>
public sealed class DefenseTower : MonoBehaviour
{
    public ArmyType Army { get; private set; }
    public float Range { get; private set; }
    public float Damage { get; private set; }
    public float Cooldown { get; private set; }
    public float SplashRadius { get; private set; }
    public Color ProjectileColor { get; private set; }

    private TriunviratoGame game;
    private float attackTimer;

    internal void Configure(TriunviratoGame owner, ArmyType army, float range, float damage, float cooldown,
        float splashRadius, Color projectileColor)
    {
        game = owner;
        Army = army;
        Range = range;
        Damage = damage;
        Cooldown = cooldown;
        SplashRadius = splashRadius;
        ProjectileColor = projectileColor;
        attackTimer = 0f;
    }

    private void Update()
    {
        if (game == null || !game.CanCombat)
            return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && game.TryTowerAttack(this))
            attackTimer = Cooldown;
    }
}
