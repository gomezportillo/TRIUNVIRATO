using UnityEngine;

/// <summary>Supplies the current sprite's UV rectangle so outline sampling cannot leak across atlas frames.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteOutlineUvBounds : MonoBehaviour
{
    private static readonly int UvMinId = Shader.PropertyToID("_SpriteUvMin");
    private static readonly int UvMaxId = Shader.PropertyToID("_SpriteUvMax");
    private static readonly int UvValidId = Shader.PropertyToID("_SpriteUvValid");
    private static readonly int LocalMinId = Shader.PropertyToID("_SpriteLocalMin");
    private static readonly int LocalMaxId = Shader.PropertyToID("_SpriteLocalMax");
    private static readonly int OutlineLocalWidthId = Shader.PropertyToID("_OutlineLocalWidth");
    private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");

    private SpriteRenderer spriteRenderer;
    private Sprite cachedSprite;
    private float cachedOutlinePixels = -1f;
    private bool cachedFlipX;
    private bool cachedFlipY;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        RefreshBounds();
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            return;
        float outlinePixels = spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.HasProperty(OutlinePixelsId)
            ? spriteRenderer.sharedMaterial.GetFloat(OutlinePixelsId)
            : 0f;
        if (spriteRenderer.sprite != cachedSprite || spriteRenderer.flipX != cachedFlipX || spriteRenderer.flipY != cachedFlipY ||
            !Mathf.Approximately(outlinePixels, cachedOutlinePixels))
            RefreshBounds();
    }

    private void OnEnable()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        RefreshBounds();
    }

    private void RefreshBounds()
    {
        if (spriteRenderer == null)
            return;

        cachedSprite = spriteRenderer.sprite;
        cachedOutlinePixels = spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.HasProperty(OutlinePixelsId)
            ? spriteRenderer.sharedMaterial.GetFloat(OutlinePixelsId)
            : 0f;
        cachedFlipX = spriteRenderer.flipX;
        cachedFlipY = spriteRenderer.flipY;
        if (cachedSprite == null)
        {
            propertyBlock.SetFloat(UvValidId, 0f);
            propertyBlock.SetFloat(OutlineLocalWidthId, 0f);
            spriteRenderer.SetPropertyBlock(propertyBlock);
            return;
        }

        Vector2[] uvs = cachedSprite.uv;
        if (uvs == null || uvs.Length == 0)
        {
            propertyBlock.SetFloat(UvValidId, 0f);
            propertyBlock.SetFloat(OutlineLocalWidthId, 0f);
            spriteRenderer.SetPropertyBlock(propertyBlock);
            return;
        }

        Vector2 min = uvs[0];
        Vector2 max = uvs[0];
        for (int i = 1; i < uvs.Length; i++)
        {
            min = Vector2.Min(min, uvs[i]);
            max = Vector2.Max(max, uvs[i]);
        }

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(UvMinId, new Vector4(min.x, min.y, 0f, 0f));
        propertyBlock.SetVector(UvMaxId, new Vector4(max.x, max.y, 0f, 0f));
        Bounds bounds = cachedSprite.bounds;
        Vector2 localMin = bounds.min;
        Vector2 localMax = bounds.max;
        if (spriteRenderer.flipX)
        {
            float minX = -localMax.x;
            localMax.x = -localMin.x;
            localMin.x = minX;
        }
        if (spriteRenderer.flipY)
        {
            float minY = -localMax.y;
            localMax.y = -localMin.y;
            localMin.y = minY;
        }
        propertyBlock.SetVector(LocalMinId, new Vector4(localMin.x, localMin.y, 0f, 0f));
        propertyBlock.SetVector(LocalMaxId, new Vector4(localMax.x, localMax.y, 0f, 0f));
        float pixelsPerUnit = Mathf.Max(1f, cachedSprite.pixelsPerUnit);
        propertyBlock.SetFloat(OutlineLocalWidthId, cachedOutlinePixels / pixelsPerUnit);
        propertyBlock.SetFloat(UvValidId, 1f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
