using UnityEngine;

[CreateAssetMenu(fileName = "ZombieGrasp", menuName = "TRIUNVIRATO/Abilities/Zombie Grasp")]
public sealed class ZombieGraspAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField, Min(0f)] private float normalEnemyDuration = 2f;
    [SerializeField, Min(0f)] private float bossDuration = 0.5f;
    [SerializeField, Min(0f)] private float normalDurationUpgrade = 1f;
    [SerializeField, Min(0f)] private float bossDurationUpgrade = 0.5f;

    public float NormalEnemyDuration => normalEnemyDuration;
    public float BossDuration => bossDuration;
    public float NormalDurationUpgrade => normalDurationUpgrade;
    public float BossDurationUpgrade => bossDurationUpgrade;
}
