using UnityEngine;
using System.Collections;

/// <summary>Attack cadence and stats for an unlocked tower; hits use the shared game projectile pipeline.</summary>
public sealed class DefenseTower : MonoBehaviour
{
    public TowerConfig Config { get; private set; }
    public ArmyType Army => Config != null ? Config.Army : default;
    public float Range { get; private set; }
    public float Damage { get; private set; }
    public float Cooldown { get; private set; }
    public float SplashRadius { get; private set; }
    public Transform Muzzle => transform.Find("Muzzle");

    internal void PlayMuzzleEffect()
    {
        Transform muzzle = Muzzle;
        if (muzzle == null) return;
        Animator animator = muzzle.GetComponent<Animator>();
        SpriteRenderer renderer = muzzle.GetComponent<SpriteRenderer>();
        if (animator == null || renderer == null) return;
        if (muzzleEffectCoroutine != null) StopCoroutine(muzzleEffectCoroutine);
        renderer.enabled = true;
        animator.enabled = true;
        animator.Play("Muzzle", 0, 0f);
        float duration = 0.3f;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller != null && controller.animationClips.Length > 0)
            duration = controller.animationClips[0].length;
        muzzleEffectCoroutine = StartCoroutine(HideMuzzleEffect(animator, renderer, duration));
    }

    private IEnumerator HideMuzzleEffect(Animator animator, SpriteRenderer renderer, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (animator != null) animator.enabled = false;
        if (renderer != null) renderer.enabled = false;
        muzzleEffectCoroutine = null;
    }

    private TriunviratoGame game;
    private float attackTimer;
    private Coroutine muzzleEffectCoroutine;
    internal void Configure(TriunviratoGame owner, TowerConfig config)
    {
        game = owner;
        Config = config;
        if (config == null || config.Hero == null)
            return;

        RefreshUpgradeStats();
        SplashRadius = config.Hero.SplashRadius;
        attackTimer = 0f;
    }

    internal void RefreshUpgradeDamage() => RefreshUpgradeStats();

    internal void RefreshUpgradeStats()
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
