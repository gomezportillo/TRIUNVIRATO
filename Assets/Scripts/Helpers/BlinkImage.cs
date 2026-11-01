using UnityEngine;
using System.Collections;

public class BlinkImage : MonoBehaviour
{
    [SerializeField] private float interval = 1f;

    private SpriteRenderer spriteRenderer;
    private Coroutine blinkCoroutine;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        blinkCoroutine = StartCoroutine(Blink());
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    private IEnumerator Blink()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            if (spriteRenderer != null) spriteRenderer.enabled = !spriteRenderer.enabled;
        }
    }
}
