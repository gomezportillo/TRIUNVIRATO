using UnityEngine;
using System.Collections;

public class BlinkImage : MonoBehaviour
{
    [SerializeField] private float interval = 1f;

    private SpriteRenderer spriteRenderer;

    private IEnumerator Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        while (true)
        {
            yield return new WaitForSeconds(interval);
            spriteRenderer.enabled = !spriteRenderer.enabled;
        }
    }
}