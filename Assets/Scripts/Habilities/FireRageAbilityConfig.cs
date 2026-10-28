using UnityEngine;

[CreateAssetMenu(fileName = "FireRage", menuName = "TRIUNVIRATO/Abilities/Fire Rage")]
public sealed class FireRageAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(0f)] private float bonusDamagePercent = 100f;
    [SerializeField, Min(0.1f)] private float duration = 5f;
    [SerializeField, Min(0f)] private float damageUpgradePercent = 25f;
    [SerializeField, Min(0f)] private float durationUpgradeSeconds = 1f;
    [SerializeField] private GameObject buffVisualPrefab;
    [SerializeField, Tooltip("Tinte multiplicativo aplicado a los SpriteRenderer mientras dura Furia ígnea.")] private Color buffTint = new Color(1f, 0.78f, 0.78f, 1f);

    public float BonusDamagePercent => bonusDamagePercent;
    public float Duration => duration;
    public float DamageUpgradePercent => damageUpgradePercent;
    public float DurationUpgradeSeconds => durationUpgradeSeconds;
    public GameObject BuffVisualPrefab => buffVisualPrefab;
    public Color BuffTint => buffTint;
}
