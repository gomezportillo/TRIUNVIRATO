using UnityEngine;

[CreateAssetMenu(fileName = "KnightSummon", menuName = "TRIUNVIRATO/Abilities/Knight Summon")]
public sealed class KnightSummonAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField] private GameObject knightPrefab;
    [SerializeField, Min(1f)] private float baseHealth = 160f;
    [SerializeField, Min(0f)] private float baseDamage = 18f;
    [SerializeField, Min(0.05f)] private float attackInterval = 1.25f;
    [SerializeField, Min(0f)] private float attackRange = 0.55f;
    [SerializeField, Min(0f)] private float movementSpeed = 1.25f;
    [SerializeField, Min(0f)] private float enemyEngagementRange = 0.65f;
    [SerializeField, Min(0f)] private float healthAndDamageIncreasePerLevel = 0.25f;
    [SerializeField, Min(0f)] private float cooldownReductionPerLevel = 3f;
    [SerializeField, Min(0f)] private float minimumCooldown = 5f;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.55f, 0f);
    [SerializeField, Min(0.01f)] private float visualScale = 0.45f;
    [SerializeField, Min(0.1f)] private float deathAnimationFallback = 1f;

    public GameObject KnightPrefab => knightPrefab;
    public float BaseHealth => baseHealth;
    public float BaseDamage => baseDamage;
    public float AttackInterval => attackInterval;
    public float AttackRange => attackRange;
    public float MovementSpeed => movementSpeed;
    public float EnemyEngagementRange => enemyEngagementRange;
    public float HealthAndDamageIncreasePerLevel => healthAndDamageIncreasePerLevel;
    public float CooldownReductionPerLevel => cooldownReductionPerLevel;
    public float MinimumCooldown => minimumCooldown;
    public Vector2 SpawnOffset => spawnOffset;
    public float VisualScale => visualScale;
    public float DeathAnimationFallback => deathAnimationFallback;
}
