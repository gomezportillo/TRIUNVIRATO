using UnityEngine;

/// <summary>Small world-space health bar shared by enemies and summoned combat units.</summary>
[DisallowMultipleComponent]
public sealed class WorldHealthBar : MonoBehaviour
{
    private static Sprite pixelSprite;
    private Transform barRoot;
    private SpriteRenderer actorRenderer;
    private SpriteRenderer background;
    private SpriteRenderer fill;
    private float width;
    private float height;
    private float offset;
    private float currentHealth;
    private float maximumHealth;

    public void Initialize(SpriteRenderer actor, float barWidth, float barHeight, float verticalOffset)
    {
        actorRenderer = actor;
        width = Mathf.Max(0.01f, barWidth);
        height = Mathf.Max(0.001f, barHeight);
        offset = verticalOffset;
        if (actorRenderer == null) return;
        CreateBar();
    }

    public void SetHealth(float current, float maximum)
    {
        currentHealth = Mathf.Max(0f, current);
        maximumHealth = Mathf.Max(0f, maximum);
        UpdateFill();
    }

    public void SetVisible(bool visible)
    {
        if (barRoot != null) barRoot.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (barRoot == null || actorRenderer == null) return;
        Bounds bounds = actorRenderer.bounds;
        barRoot.position = new Vector3(bounds.center.x, bounds.max.y + offset, bounds.center.z);

        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(barRoot.lossyScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(barRoot.lossyScale.y));
        background.transform.localScale = new Vector3(width / parentScaleX, height / parentScaleY, 1f);
        UpdateFill(parentScaleX, parentScaleY);
    }

    private void CreateBar()
    {
        if (pixelSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Combat Health Bar Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            pixelSprite.name = "Combat Health Bar Sprite";
        }

        GameObject root = new GameObject("Health Bar");
        barRoot = root.transform;
        barRoot.SetParent(transform, true);
        background = CreateLayer("Background", new Color(0.08f, 0.08f, 0.08f, 0.9f), 20);
        fill = CreateLayer("Fill", Color.green, 21);
        UpdateFill();
    }

    private SpriteRenderer CreateLayer(string objectName, Color color, int sortingOffset)
    {
        GameObject layer = new GameObject(objectName);
        layer.transform.SetParent(barRoot, false);
        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = pixelSprite;
        renderer.color = color;
        renderer.sortingLayerID = actorRenderer.sortingLayerID;
        renderer.sortingOrder = actorRenderer.sortingOrder + sortingOffset;
        return renderer;
    }

    private void UpdateFill()
    {
        if (barRoot == null) return;
        UpdateFill(Mathf.Max(0.0001f, Mathf.Abs(barRoot.lossyScale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(barRoot.lossyScale.y)));
    }

    private void UpdateFill(float parentScaleX, float parentScaleY)
    {
        if (fill == null) return;
        float healthRatio = maximumHealth > 0f ? Mathf.Clamp01(currentHealth / maximumHealth) : 0f;
        fill.transform.localScale = new Vector3(width * healthRatio / parentScaleX,
            height * 0.68f / parentScaleY, 1f);
        fill.transform.localPosition = new Vector3(-width * (1f - healthRatio) * 0.5f / parentScaleX,
            0f, -0.01f);
        fill.color = healthRatio > 0.55f ? new Color(0.28f, 0.92f, 0.38f) :
            healthRatio > 0.25f ? new Color(1f, 0.78f, 0.22f) : new Color(1f, 0.27f, 0.24f);
    }
}
