
using UnityEngine;

public class FloatingAnimation : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private float amplitude = 0.08f;
    [SerializeField] private float frequency = 0.25f;
    [SerializeField] private bool randomizeStart = true;

    private Vector3 initialPosition;
    private float phase;

    private void Awake()
    {
        initialPosition = transform.localPosition;

        // Evita que todos los objetos floten sincronizados.
        phase = randomizeStart ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    private void Update()
    {
        float offsetY = Mathf.Sin(
            Time.time * frequency * Mathf.PI * 2f + phase
        ) * amplitude;

        transform.localPosition = initialPosition
            + new Vector3(0f, offsetY, 0f);
    }
}
