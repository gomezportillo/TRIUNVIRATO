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
    private SpriteRenderer actorRenderer;
    private WorldHealthBar healthBar;
    private bool isBoss;
    private bool isDead;
    private bool isTakingHit;
    private bool hasReachedCastle;
    private bool rewardGranted;
    private float currentHealth;
    private float movementSpeed;
    private float attackAnimationSpeed = 1f;
    private float castleAttackImpactDelay;
    private float castleAttackTimer;
    private float allyAttackTimer;
    private float idleRemaining;
    private float deathFallbackTimer;
    private float rootedUntil;
    private float animationSpeedBeforeRoot = 1f;
    private bool isRooted;
    private TimedCombatEffects combatEffects;

    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public bool RewardGranted => rewardGranted;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth { get; private set; }
    public TimedCombatEffects Effects => combatEffects;

    internal void Initialize(EnemyConfig enemyConfig, TriunviratoGame owner, int wave, bool boss)
    {
        config = enemyConfig;
        game = owner;
        isBoss = boss;
        animator = GetComponent<Animator>();
        actorRenderer = GetComponentInChildren<SpriteRenderer>();
        combatEffects = GetComponent<TimedCombatEffects>();
        if (combatEffects == null) combatEffects = gameObject.AddComponent<TimedCombatEffects>();
        currentHealth = MaximumHealth = config.GetMaximumHealth(wave, isBoss);
        movementSpeed = config.GetMovementSpeed(wave, isBoss);
        healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null) healthBar = gameObject.AddComponent<WorldHealthBar>();
        healthBar.Initialize(actorRenderer, config.HealthBarLength, config.HealthBarHeight, config.HealthBarOffset);
        healthBar.SetHealth(currentHealth, MaximumHealth);

        float attackClipLength = GetClipLength(AttackState, 1f);
        attackAnimationSpeed = attackClipLength / Mathf.Max(0.1f, config.CastleAttackInterval);
        castleAttackImpactDelay = GetCastleAttackImpactDelay();
        allyAttackTimer = config.CastleAttackInterval;
        idleRemaining = config.InitialIdleDuration;
        deathFallbackTimer = config.DeathAnimationFallbackSeconds;
        PlayState(IdleState, 1f);
    }

    /// <summary>Returns only the damage that actually reduced health, capped by remaining health.</summary>
    internal float ApplyDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return 0f;

        float appliedDamage = Mathf.Min(currentHealth, amount * (combatEffects != null ? combatEffects.IncomingDamageMultiplier : 1f));
        currentHealth = Mathf.Max(0f, currentHealth - appliedDamage);
        healthBar?.SetHealth(currentHealth, MaximumHealth);
        if (currentHealth <= 0f)
        {
            isDead = true;
            isTakingHit = false;
            isRooted = false;
            rootedUntil = 0f;
            combatEffects?.ClearAll();
            healthBar?.SetVisible(false);
            PlayState(DeathState, 1f);
            deathFallbackTimer = config.DeathAnimationFallbackSeconds;
            GameSoundManager.Instance?.PlaySkeletonDeath();
            return appliedDamage;
        }

        if (!isTakingHit)
        {
            isTakingHit = true;
            PlayState(TakeHitState, 1f);
            if (isRooted && animator != null)
                animator.speed = 0f;
        }
        return appliedDamage;
    }

    internal void ApplyRooted(float duration)
    {
        if (isDead || duration <= 0f)
            return;
        rootedUntil = Mathf.Max(rootedUntil, Time.time + duration);
    }

    /// <summary>Moves toward the castle and returns true after the death animation has finished.</summary>
    internal bool Tick(float deltaTime, Vector3 castlePosition, KnightAlly knightTarget = null)
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

        if (Time.time < rootedUntil)
        {
            if (!isRooted)
            {
                isRooted = true;
                animationSpeedBeforeRoot = animator != null ? animator.speed : 1f;
                if (animator != null)
                    animator.speed = 0f;
            }
            return false;
        }
        if (isRooted)
        {
            isRooted = false;
            if (animator != null)
                animator.speed = animationSpeedBeforeRoot;
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

        if (knightTarget != null && !knightTarget.IsDead &&
            Vector2.Distance(transform.position, knightTarget.transform.position) <= knightTarget.EnemyEngagementRange)
        {
            hasReachedCastle = false;
            allyAttackTimer -= deltaTime;
            if (allyAttackTimer <= 0f)
            {
                allyAttackTimer = config.CastleAttackInterval;
                PlayState(AttackState, attackAnimationSpeed);
                knightTarget.ApplyDamage(config.GetCastleDamage(isBoss));
            }
            else if (animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != AttackState)
                PlayState(AttackState, attackAnimationSpeed);
            return false;
        }
        allyAttackTimer = config.CastleAttackInterval;

        // Castle damage is driven by a timer as well as the animation event. The timer
        // keeps attacks working if Take Hit (or another state) interrupts the Attack clip.
        if (hasReachedCastle)
        {
            castleAttackTimer -= deltaTime;
            if (castleAttackTimer <= 0f)
                ApplyCastleAttackDamage();
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

        float effectiveMovementSpeed = movementSpeed * (combatEffects != null ? combatEffects.MovementSpeedMultiplier : 1f);
        transform.position = Vector3.MoveTowards(transform.position, castlePosition, effectiveMovementSpeed * deltaTime);
        if (Vector2.Distance(transform.position, castlePosition) <= 0.2f)
        {
            hasReachedCastle = true;
            castleAttackTimer = castleAttackImpactDelay;
            PlayState(AttackState, attackAnimationSpeed);
        }
        else if (animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != WalkState)
        {
            PlayState(WalkState, 1f);
        }

        return false;
    }

    /// <summary>Animation Event on the weapon impact frame; timer is a fallback if the clip is interrupted.</summary>
    public void OnCastleAttackImpact()
    {
        if (!isDead && hasReachedCastle && game != null &&
            castleAttackTimer <= Mathf.Max(0.02f, Time.deltaTime * 1.5f))
            ApplyCastleAttackDamage();
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

    private float GetCastleAttackImpactDelay()
    {
        float interval = config.CastleAttackInterval;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (Animator.StringToHash(clip.name) != AttackState || clip.length <= 0f)
                    continue;

                foreach (AnimationEvent animationEvent in clip.events)
                {
                    if (animationEvent.functionName == nameof(OnCastleAttackImpact))
                        return Mathf.Clamp(animationEvent.time / clip.length, 0f, 1f) * interval;
                }
            }
        }

        return interval * 0.75f;
    }

    private void ApplyCastleAttackDamage()
    {
        if (isDead || !hasReachedCastle || game == null)
            return;

        game.ApplyCastleDamage(config.GetCastleDamage(isBoss));
        castleAttackTimer = config.CastleAttackInterval;
    }
}
