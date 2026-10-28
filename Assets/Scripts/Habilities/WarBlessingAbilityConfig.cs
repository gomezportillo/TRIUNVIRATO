using UnityEngine;

[CreateAssetMenu(fileName = "WarBlessing", menuName = "TRIUNVIRATO/Abilities/War Blessing")]
public sealed class WarBlessingAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(0.1f)] private float duration = 5f;
    [SerializeField, Min(0f)] private float bonusAttackSpeedPercent = 100f;
    [SerializeField, Min(0f)] private float powerUpgradePercent = 50f;
    [SerializeField, Min(0f)] private float cooldownReductionPerLevel = 3f;
    [SerializeField, Min(0f)] private float minimumCooldown = 5f;
    [SerializeField] private Color buffTint = new Color(0.85f, 0.95f, 1f, 1f);

    public float Duration => duration;
    public float BonusAttackSpeedPercent => bonusAttackSpeedPercent;
    public float PowerUpgradePercent => powerUpgradePercent;
    public float CooldownReductionPerLevel => cooldownReductionPerLevel;
    public float MinimumCooldown => minimumCooldown;
    public Color BuffTint => buffTint;
}
