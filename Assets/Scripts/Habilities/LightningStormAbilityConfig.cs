using UnityEngine;

[CreateAssetMenu(fileName = "LightningStorm", menuName = "TRIUNVIRATO/Abilities/Lightning Storm")]
public sealed class LightningStormAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(1)] private int hitCount = 3;
    [SerializeField, Min(1)] private int hitCountUpgrade = 1;
    [SerializeField, Min(0f)] private float damagePerHit = 15f;
    [SerializeField, Min(0f)] private float damageUpgrade = 5f;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.75f;
    [SerializeField, Min(0f)] private float impactDelay = 0.2f;

    public int HitCount => hitCount;
    public int HitCountUpgrade => hitCountUpgrade;
    public float DamagePerHit => damagePerHit;
    public float DamageUpgrade => damageUpgrade;
    public float HitInterval => hitInterval;
    public float ImpactDelay => impactDelay;
}
