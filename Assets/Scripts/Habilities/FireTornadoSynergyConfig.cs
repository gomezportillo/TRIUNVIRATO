using UnityEngine;

[CreateAssetMenu(fileName = "FireTornadoSynergy", menuName = "TRIUNVIRATO/Abilities/Fire Tornado Synergy")]
public sealed class FireTornadoSynergyConfig : ScriptableObject
{
    [SerializeField] private GameObject fireTornadoPrefab;
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;

    public GameObject FireTornadoPrefab => fireTornadoPrefab;
    public float DamageMultiplier => damageMultiplier;
}
