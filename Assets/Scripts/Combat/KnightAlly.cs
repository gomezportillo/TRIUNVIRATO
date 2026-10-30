using UnityEngine;

/// <summary>Runtime ally unit using the imported Blademaster renderer and Animator Controller.</summary>
public sealed class KnightAlly : MonoBehaviour
{
    private static readonly int IdleState = Animator.StringToHash("Blademaster_Idle");
    private static readonly int RunState = Animator.StringToHash("Blademaster_Run");
    private static readonly int AttackState = Animator.StringToHash("Blademaster_Attack");
    private static readonly int HurtState = Animator.StringToHash("Blademaster_Hit");
    private static readonly int DeathState = Animator.StringToHash("Blademaster_Death");

    private TriunviratoGame game;
    private KnightSummonAbilityConfig config;
    private Animator animator;
    private SpriteRenderer actorRenderer;
    private WorldHealthBar healthBar;
    private float currentHealth;
    private float maximumHealth;
    private float damage;
    private float combatLaneY;
    private float attackTimer;
    private float deathTimer;
    private bool dead;

    public bool IsDead => dead;
    public float EnemyEngagementRange => config != null ? config.EnemyEngagementRange : 0f;

    internal void Initialize(TriunviratoGame owner, KnightSummonAbilityConfig ability, int level)
    {
        game = owner;
        config = ability;
        animator = GetComponent<Animator>();
        actorRenderer = GetComponentInChildren<SpriteRenderer>();
        float statMultiplier = Mathf.Pow(1f + ability.HealthAndDamageIncreasePerLevel, Mathf.Max(0, level));
        maximumHealth = currentHealth = ability.BaseHealth * statMultiplier;
        damage = ability.BaseDamage * statMultiplier;
        combatLaneY = transform.position.y;
        attackTimer = 0f;
        deathTimer = ability.DeathAnimationFallback;
        transform.localScale *= ability.VisualScale;

        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != this && !(behaviour is SpriteOutlineUvBounds)) behaviour.enabled = false;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;
        healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null) healthBar = gameObject.AddComponent<WorldHealthBar>();
        healthBar.Initialize(actorRenderer, ability.HealthBarLength, ability.HealthBarHeight, ability.HealthBarOffset);
        healthBar.SetHealth(currentHealth, maximumHealth);
        PlayState(IdleState);
    }

    internal bool Tick(float deltaTime, Enemy[] enemies, float advanceTargetX)
    {
        if (dead)
        {
            deathTimer -= deltaTime;
            if (animator == null || !animator.HasState(0, DeathState)) return deathTimer <= 0f;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return (state.shortNameHash == DeathState && state.normalizedTime >= 1f) || deathTimer <= 0f;
        }

        Enemy target = FindTarget(enemies);
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance <= config.AttackRange)
            {
                SetMoving(false);
                attackTimer -= deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = Mathf.Max(0.05f, config.AttackInterval);
                    PlayState(AttackState);
                    game?.ApplyKnightDamage(target, damage, this);
                }
                return false;
            }

            float targetY = Mathf.Clamp(target.transform.position.y,
                combatLaneY - config.MaximumLaneYOffset, combatLaneY + config.MaximumLaneYOffset);
            MoveToward(new Vector2(target.transform.position.x, targetY), deltaTime);
            return false;
        }

        if (transform.position.x < advanceTargetX - 0.05f)
            MoveToward(new Vector2(advanceTargetX, combatLaneY), deltaTime);
        else SetMoving(false);
        return false;
    }

    internal void ApplyDamage(float amount)
    {
        if (dead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            dead = true;
            healthBar?.SetVisible(false);
            PlayState(DeathState);
            deathTimer = config != null ? config.DeathAnimationFallback : 1f;
            return;
        }
        healthBar?.SetHealth(currentHealth, maximumHealth);
        PlayState(HurtState);
    }

    private Enemy FindTarget(Enemy[] enemies)
    {
        Enemy best = null;
        float bestDistance = float.PositiveInfinity;
        if (enemies == null) return null;
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < bestDistance) { best = enemy; bestDistance = distance; }
        }
        return best;
    }

    private void MoveToward(Vector2 destination, float deltaTime)
    {
        SetMoving(true);
        if (actorRenderer != null) actorRenderer.flipX = destination.x < transform.position.x;
        Vector2 current = new Vector2(transform.position.x, transform.position.y);
        Vector2 next = Vector2.MoveTowards(current, destination, config.MovementSpeed * deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    private void SetMoving(bool moving)
    {
        if (animator == null || animator.runtimeAnimatorController == null || dead) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == AttackState || state.shortNameHash == HurtState)
        {
            if (state.normalizedTime < 1f) return;
        }
        PlayState(moving ? RunState : IdleState);
    }

    private void PlayState(int stateHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, stateHash)) return;
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != stateHash)
            animator.Play(stateHash, 0, 0f);
    }
}
