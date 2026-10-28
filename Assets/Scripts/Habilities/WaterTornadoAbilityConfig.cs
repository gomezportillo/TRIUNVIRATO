using UnityEngine;

[CreateAssetMenu(fileName = "WaterTornado", menuName = "TRIUNVIRATO/Abilities/Water Tornado")]
public sealed class WaterTornadoAbilityConfig : HeroSpecialAbilityConfig
{
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField, Tooltip("Posición inicial en coordenadas del mundo.")] private Vector2 initialWorldPosition = Vector2.zero;
    [SerializeField, Min(0f)] private float damage = 20f;
    [SerializeField, Min(0.01f), Tooltip("Velocidad horizontal del tornado hacia la izquierda, en unidades por segundo.")] private float travelSpeed = 4f;
    [SerializeField, Min(0f)] private float collisionRadius = 0.4f;
    [SerializeField, Tooltip("Distancia firmada: negativo empuja a la izquierda; positivo a la derecha.")] private float pushDistance = -0.8f;
    [SerializeField, Min(0.01f)] private float pushSpeed = 3.5f;
    [SerializeField, Min(0f)] private float damageUpgradePercent = 50f;
    [SerializeField, Min(0f)] private float pushDistanceUpgrade = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("Fracción del empuje aplicado a jefes.")] private float bossPushMultiplier = 0f;

    public GameObject TornadoPrefab => tornadoPrefab;
    public Vector2 InitialWorldPosition => initialWorldPosition;
    public float Damage => damage;
    public float TravelSpeed => travelSpeed;
    public float CollisionRadius => collisionRadius;
    public float PushDistance => pushDistance;
    public float PushSpeed => pushSpeed;
    public float DamageUpgradePercent => damageUpgradePercent;
    public float PushDistanceUpgrade => pushDistanceUpgrade;
    public float BossPushMultiplier => bossPushMultiplier;
}
