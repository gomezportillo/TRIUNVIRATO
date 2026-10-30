using UnityEngine;

/// <summary>Runtime state and animation driver for one Skeleton enemy instance.</summary>
public class Enemy : MonoBehaviour
{
    private int IdleState;
    private int WalkState;
    private int TakeHitState;
    private int DeathState;
    private int AttackState;

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
    private float knockbackTargetX;
    private float knockbackSpeed;
    private bool isBeingKnockedBack;
    private float animationSpeedBeforeRoot = 1f;
    private bool isRooted;
    private TimedCombatEffects combatEffects;

    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public bool RewardGranted => rewardGranted;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth { get; private set; }
    public float ExperienceReward => config != null ? config.ExperienceReward : 0f;
    public TimedCombatEffects Effects => combatEffects;
    public bool IsRooted => isRooted || Time.time < rootedUntil;

    internal void Initialize(EnemyConfig enemyConfig, TriunviratoGame owner, int wave, bool boss)
    {
        config = enemyConfig;
        game = owner;
        isBoss = boss;
        IdleState = Animator.StringToHash(config.IdleAnimationState);
        WalkState = Animator.StringToHash(config.WalkAnimationState);
        TakeHitState = Animator.StringToHash(config.TakeHitAnimationState);
        DeathState = Animator.StringToHash(config.DeathAnimationState);
        AttackState = Animator.StringToHash(config.AttackAnimationState);
        animator = GetComponent<Animator>();
        actorRenderer = GetComponentInChildren<SpriteRenderer>();
        combatEffects = GetComponent<TimedCombatEffects>();
        if (combatEffects == null) combatEffects = gameObject.AddComponent<TimedCombatEffects>();
        currentHealth = MaximumHealth = config.GetMaximumHealth(wave);
        movementSpeed = config.GetMovementSpeed(wave);
        healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null) healthBar = gameObject.AddComponent<WorldHealthBar>();
        healthBar.Initialize(actorRenderer, config.HealthBarLength, config.HealthBarHeight, config.HealthBarOffset);
        healthBar.SetHealth(currentHealth, MaximumHealth);

        float attackClipLength = GetClipLength(AttackState, config.AttackAnimationState, 1f);
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
            isBeingKnockedBack = false;
            isRooted = false;
            rootedUntil = 0f;
            combatEffects?.ClearAll();
            healthBar?.SetVisible(false);
            PlayState(DeathState, 1f);
            deathFallbackTimer = config.DeathAnimationFallbackSeconds;
            GameSoundManager.Instance?.PlayEnemyDeath(config.DeathSound);
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

    /// <summary>Moves an enemy by a bounded horizontal impulse unless zombie grasp currently owns its movement.</summary>
    internal bool TryApplyKnockback(float signedDistance, float speed, float minimumX, float maximumX)
    {
        if (isDead || IsRooted || Mathf.Abs(signedDistance) <= 0.001f || speed <= 0f) return false;
        knockbackTargetX = Mathf.Clamp(transform.position.x + signedDistance, minimumX, maximumX);
        if (Mathf.Abs(knockbackTargetX - transform.position.x) <= 0.001f) return false;
        knockbackSpeed = speed;
        isBeingKnockedBack = true;
        return true;
    }

    /// <summary>Moves toward the castle and returns true after the death animation has finished.</summary>
    internal bool Tick(float deltaTime, Vector3 castlePosition, KnightAlly knightTarget = null)
    {
        if (isDead)
        {
            deathFallbackTimer -= deltaTime;
            int resolvedDeathState = ResolveStateHash(DeathState);
            if (animator == null || !animator.HasState(0, resolvedDeathState))
                return deathFallbackTimer <= 0f;

            AnimatorStateInfo deathInfo = animator.GetCurrentAnimatorStateInfo(0);
            return (deathInfo.shortNameHash == resolvedDeathState && deathInfo.normalizedTime >= 1f) ||
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
            int resolvedHitState = ResolveStateHash(TakeHitState);
            if (animator != null && hitInfo.shortNameHash == resolvedHitState && hitInfo.normalizedTime < 1f)
                return false;

            isTakingHit = false;
            if (hasReachedCastle)
                PlayState(AttackState, attackAnimationSpeed);
        }

        if (isBeingKnockedBack)
        {
            float nextX = Mathf.MoveTowards(transform.position.x, knockbackTargetX, knockbackSpeed * deltaTime);
            transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
            PlayState(IdleState, 1f);
            if (Mathf.Abs(nextX - knockbackTargetX) <= 0.001f) isBeingKnockedBack = false;
            return false;
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
                knightTarget.ApplyDamage(config.GetCastleDamage());
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
        if (animator == null)
            return;

        int selectedState = ResolveStateHash(stateHash);
        if (!animator.HasState(0, selectedState)) return;

        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != selectedState)
            animator.Play(selectedState, 0, 0f);
        animator.speed = speed;
    }

    private int ResolveStateHash(int stateHash)
    {
        if (animator == null || animator.HasState(0, stateHash))
            return stateHash;

        string alias = stateHash == IdleState ? "BringerOfDeath_Idle" :
            stateHash == WalkState ? "BringerOfDeath_Walk" :
            stateHash == TakeHitState ? "BringerOfDeath_Hurt" :
            stateHash == DeathState ? "BringerOfDeath_Death" : null;
        return string.IsNullOrEmpty(alias) ? stateHash : Animator.StringToHash(alias);
    }

    private float GetClipLength(int stateHash, string stateName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallback;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (Animator.StringToHash(clip.name) == stateHash || clip.name == stateName || clip.name == GetBossClipName(stateHash))
                return clip.length;
        }
        return fallback;
    }

    private string GetBossClipName(int stateHash)
    {
        if (stateHash == IdleState) return "BringerOfDeath_Idle";
        if (stateHash == WalkState) return "BringerOfDeath_Walk";
        if (stateHash == TakeHitState) return "BringerOfDeath_Hurt";
        if (stateHash == DeathState) return "BringerOfDeath_Death";
        if (stateHash == AttackState) return "BringerOfDeath_Attack";
        return null;
    }

    private float GetCastleAttackImpactDelay()
    {
        float interval = config.CastleAttackInterval;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (Animator.StringToHash(clip.name) != AttackState && clip.name != config.AttackAnimationState && clip.name != GetBossClipName(AttackState) || clip.length <= 0f)
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

        game.ApplyCastleDamage(config.GetCastleDamage());
        castleAttackTimer = config.CastleAttackInterval;
    }
}
