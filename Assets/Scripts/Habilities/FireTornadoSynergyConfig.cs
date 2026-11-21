using UnityEngine;

[CreateAssetMenu(fileName = "FireTornadoSynergy", menuName = "TRIUNVIRATO/Abilities/Fire Tornado Synergy")]
public sealed class FireTornadoSynergyConfig : ScriptableObject
{
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;

    public float DamageMultiplier => damageMultiplier;
}
