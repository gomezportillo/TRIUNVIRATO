using UnityEngine;

/// <summary>Runtime state and animation driver for one Skeleton enemy instance.</summary>
public sealed class SkeletonEnemy : MonoBehaviour
{
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int WalkState = Animator.StringToHash("Walk");
    private static readonly int TakeHitState = Animator.StringToHash("Take Hit");
    private static readonly int DeathState = Animator.StringToHash("Death");
    private static readonly int AttackState = Animator.StringToHash("Attack");

    private EnemyConfig config;
    private TriunviratoGame game;
    private Animator animator;
    private bool isBoss;
    private bool isDead;
    private bool isTakingHit;
    private bool hasReachedCastle;
    private bool rewardGranted;
    private float currentHealth;
    private float movementSpeed;
    private float attackAnimationSpeed = 1f;
    private float idleRemaining;
    private float deathFallbackTimer;

    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public bool RewardGranted => rewardGranted;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth { get; private set; }

    internal void Initialize(EnemyConfig enemyConfig, TriunviratoGame owner, int wave, bool boss)
    {
        config = enemyConfig;
        game = owner;
        isBoss = boss;
        animator = GetComponent<Animator>();
        currentHealth = MaximumHealth = config.GetMaximumHealth(wave, isBoss);
        movementSpeed = config.GetMovementSpeed(wave, isBoss);

        float attackClipLength = GetClipLength(AttackState, 1f);
        attackAnimationSpeed = attackClipLength / Mathf.Max(0.1f, config.CastleAttackInterval);
        idleRemaining = config.InitialIdleDuration;
        deathFallbackTimer = config.DeathAnimationFallbackSeconds;
        PlayState(IdleState, 1f);
    }

    /// <summary>Returns true only on the first hit that kills this instance.</summary>
    internal bool ApplyDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            isDead = true;
            isTakingHit = false;
            PlayState(DeathState, 1f);
            deathFallbackTimer = config.DeathAnimationFallbackSeconds;
            return true;
        }

        if (!isTakingHit)
        {
            isTakingHit = true;
            PlayState(TakeHitState, 1f);
        }
        return false;
    }

    /// <summary>Moves toward the castle and returns true after the death animation has finished.</summary>
    internal bool Tick(float deltaTime, Vector3 castlePosition)
    {
        if (isDead)
        {
            deathFallbackTimer -= deltaTime;
            if (animator == null || !animator.HasState(0, DeathState))
                return deathFallbackTimer <= 0f;

            AnimatorStateInfo deathInfo = animator.GetCurrentAnimatorStateInfo(0);
            return (deathInfo.shortNameHash == DeathState && deathInfo.normalizedTime >= 1f) ||
                   deathFallbackTimer <= 0f;
        }

        if (isTakingHit)
        {
            AnimatorStateInfo hitInfo = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            if (animator != null && hitInfo.shortNameHash == TakeHitState && hitInfo.normalizedTime < 1f)
                return false;

            isTakingHit = false;
            if (hasReachedCastle)
                PlayState(AttackState, attackAnimationSpeed);
        }

        if (idleRemaining > 0f)
        {
            idleRemaining -= deltaTime;
            return false;
        }

        if (hasReachedCastle)
        {
            if (animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AttackState)
                PlayState(AttackState, attackAnimationSpeed);
            return false;
        }

        transform.position = Vector3.MoveTowards(transform.position, castlePosition, movementSpeed * deltaTime);
        if (Vector2.Distance(transform.position, castlePosition) <= 0.2f)
        {
            hasReachedCastle = true;
            PlayState(AttackState, attackAnimationSpeed);
        }
        else if (animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != WalkState)
        {
            PlayState(WalkState, 1f);
        }

        return false;
    }

    /// <summary>Animation Event on the weapon impact frame; ignored for dead or moving enemies.</summary>
    public void OnCastleAttackImpact()
    {
        if (!isDead && hasReachedCastle && game != null)
            game.ApplyCastleDamage(config.GetCastleDamage(isBoss));
    }

    internal void MarkRewardGranted() => rewardGranted = true;

    private void PlayState(int stateHash, float speed)
    {
        if (animator == null || !animator.HasState(0, stateHash))
            return;

        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != stateHash)
            animator.Play(stateHash, 0, 0f);
        animator.speed = speed;
    }

    private float GetClipLength(int stateHash, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallback;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (Animator.StringToHash(clip.name) == stateHash)
                return clip.length;
        }
        return fallback;
    }
}
