using UnityEngine;

[CreateAssetMenu(fileName = "MeteorShower", menuName = "TRIUNVIRATO/Abilities/Meteor Shower")]
public sealed class MeteorShowerAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(1)] private int meteorCount = 8;
    [SerializeField, Min(1)] private int meteorCountUpgrade = 1;
    [SerializeField, Min(0f)] private float damage = 6f;
    [SerializeField, Min(0f)] private float damageUpgrade = 5f;
    [SerializeField, Min(0f)] private float radius = 1.25f;
    [SerializeField, Min(0.01f)] private float interval = 0.35f;
    [SerializeField, Min(0f)] private float impactDelay = 0.7f;
    [SerializeField, Min(0f)] private float horizontalPadding = 0.4f;
    [SerializeField] private float centerY;
    [SerializeField, Min(0.1f)] private float verticalHalfRange = 0.5f;
    [SerializeField] private float maximumX = 5f;
    [SerializeField, Min(0f)] private float minimumSeparation = 1.1f;

    public int MeteorCount => meteorCount;
    public int MeteorCountUpgrade => meteorCountUpgrade;
    public float Damage => damage;
    public float DamageUpgrade => damageUpgrade;
    public float Radius => radius;
    public float Interval => interval;
    public float ImpactDelay => impactDelay;
    public float HorizontalPadding => horizontalPadding;
    public float CenterY => centerY;
    public float VerticalHalfRange => verticalHalfRange;
    public float MaximumX => maximumX;
    public float MinimumSeparation => minimumSeparation;
}
