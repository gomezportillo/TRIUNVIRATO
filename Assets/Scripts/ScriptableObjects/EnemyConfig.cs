using UnityEngine;

/// <summary>Shared design values for a wave enemy. Runtime health belongs to each SkeletonEnemy.</summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "TRIUNVIRATO/Combat/Enemy Config")]
public sealed class EnemyConfig : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private SkeletonEnemy prefab;
    [SerializeField, Min(0.1f)] private float maximumHealth = 25f;
    [SerializeField, Min(0f)] private float additionalHealthPerWave = 7f;
    [SerializeField, Min(0f)] private float healthGrowthPerWave = 0.05f;
    [SerializeField, Min(0f)] private float movementSpeed = 0.95f;
    [SerializeField, Min(0f)] private float speedGrowthPerWave = 0.05f;
    [SerializeField, Min(1f), Tooltip("Límite de velocidad respecto a la velocidad base, para evitar que oleadas tardías se vuelvan incontrolables.")]
    private float maximumSpeedMultiplier = 2f;
    [SerializeField, Min(0f)] private float castleDamagePerAttack = 12f;
    [SerializeField, Min(0.1f)] private float castleAttackInterval = 2f;
    [SerializeField, Min(0f)] private float initialIdleDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float visualScale = 0.5f;
    [SerializeField, Min(0f), Tooltip("Experiencia base concedida por este enemigo a cada héroe.")]
    private float experienceReward = 10f;
    [Header("Barra de vida")]
    [SerializeField, Min(0.01f), Tooltip("Longitud de la barra en unidades de mundo.")] private float healthBarLength = 0.3f;
    [SerializeField, Min(0.001f), Tooltip("Altura de la barra en unidades de mundo.")] private float healthBarHeight = 0.03f;
    [SerializeField, Tooltip("Desplazamiento vertical respecto al sprite, en unidades de mundo. Puede ser negativo.")] private float healthBarOffset = 0.03f;
    [SerializeField, Min(0.1f)] private float deathAnimationFallbackSeconds = 1f;

    public string Id => id;
    public string DisplayName => displayName;
    public SkeletonEnemy Prefab => prefab;
    public float CastleAttackInterval => castleAttackInterval;
    public float InitialIdleDuration => initialIdleDuration;
    public float VisualScale => visualScale;
    public float ExperienceReward => experienceReward;
    public float HealthBarLength => healthBarLength;
    public float HealthBarHeight => healthBarHeight;
    public float HealthBarOffset => healthBarOffset;
    public float DeathAnimationFallbackSeconds => deathAnimationFallbackSeconds;

    public float GetMaximumHealth(int wave)
    {
        int waveIndex = Mathf.Max(0, wave - 1);
        float health = (maximumHealth + additionalHealthPerWave * waveIndex) *
                       Mathf.Pow(1f + healthGrowthPerWave, waveIndex);
        return health;
    }

    public float GetMovementSpeed(int wave)
    {
        int waveIndex = Mathf.Max(0, wave - 1);
        float speedMultiplier = Mathf.Min(Mathf.Pow(1f + speedGrowthPerWave, waveIndex), maximumSpeedMultiplier);
        return movementSpeed * speedMultiplier;
    }

    public float GetCastleDamage() => castleDamagePerAttack;
}
