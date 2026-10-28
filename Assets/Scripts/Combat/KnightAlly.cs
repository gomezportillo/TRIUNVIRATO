using UnityEngine;

/// <summary>Runtime ally unit using the imported Blademaster renderer and Animator Controller.</summary>
public sealed class KnightAlly : MonoBehaviour
{
    private static readonly int AnimState = Animator.StringToHash("AnimState");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int HurtTrigger = Animator.StringToHash("Hurt");
    private static readonly int DeathTrigger = Animator.StringToHash("Death");

    private TriunviratoGame game;
    private KnightSummonAbilityConfig config;
    private Animator animator;
    private SpriteRenderer actorRenderer;
    private float currentHealth;
    private float maximumHealth;
    private float damage;
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
        attackTimer = 0f;
        deathTimer = ability.DeathAnimationFallback;
        transform.localScale = Vector3.one * ability.VisualScale;

        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != this) behaviour.enabled = false;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;
        SetMoving(false);
    }

    internal bool Tick(float deltaTime, SkeletonEnemy[] enemies, float advanceTargetX)
    {
        if (dead)
        {
            deathTimer -= deltaTime;
            if (animator == null || !animator.HasState(0, Animator.StringToHash("Death"))) return deathTimer <= 0f;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return (state.shortNameHash == Animator.StringToHash("Death") && state.normalizedTime >= 1f) || deathTimer <= 0f;
        }

        SkeletonEnemy target = FindTarget(enemies);
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
                    if (animator != null) animator.SetTrigger(AttackTrigger);
                    game?.ApplyKnightDamage(target, damage, this);
                }
                return false;
            }

            MoveToward(target.transform.position.x, deltaTime);
            return false;
        }

        if (transform.position.x < advanceTargetX - 0.05f)
            MoveToward(advanceTargetX, deltaTime);
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
            if (animator != null) animator.SetTrigger(DeathTrigger);
            deathTimer = config != null ? config.DeathAnimationFallback : 1f;
            return;
        }
        if (animator != null) animator.SetTrigger(HurtTrigger);
    }

    private SkeletonEnemy FindTarget(SkeletonEnemy[] enemies)
    {
        SkeletonEnemy best = null;
        float bestDistance = float.PositiveInfinity;
        if (enemies == null) return null;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < bestDistance) { best = enemy; bestDistance = distance; }
        }
        return best;
    }

    private void MoveToward(float x, float deltaTime)
    {
        SetMoving(true);
        if (actorRenderer != null) actorRenderer.flipX = x < transform.position.x;
        Vector3 destination = new Vector3(x, transform.position.y, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, destination, config.MovementSpeed * deltaTime);
    }

    private void SetMoving(bool moving)
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.parameters.Length > 0)
            animator.SetFloat(AnimState, moving ? 1f : 0f);
    }
}
