using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable temporary combat modifiers. Reapplying an ID refreshes its duration without stacking it.</summary>
public sealed class TimedCombatEffects : MonoBehaviour
{
    [Serializable]
    public struct Snapshot
    {
        public string id;
        public string owner;
        public float remaining;
        public float damageMultiplier;
        public float attackSpeedMultiplier;
        public float movementSpeedMultiplier;
        public float incomingDamageMultiplier;
        public GameObject visualPrefab;
        public GameObject iconPrefab;
        public GameObject secondaryIconPrefab;
        public Sprite[] animationIconFrames;
        public Sprite[] secondaryAnimationIconFrames;
        public float iconFrameRate;
        public float iconFreezeNormalizedTime;
        public float iconSize;
        public Vector2 iconOffset;
        public float iconVerticalSpacing;
        public bool hasTint;
        public Color tintMultiplier;
    }

    [Serializable]
    private sealed class Entry
    {
        public Snapshot value;
        [NonSerialized] public GameObject visual;
        [NonSerialized] public GameObject icon;
        [NonSerialized] public GameObject secondaryIcon;
        [NonSerialized] public SpriteRenderer iconRenderer;
        [NonSerialized] public SpriteRenderer secondaryIconRenderer;
        [NonSerialized] public float iconElapsed;
        [NonSerialized] public float secondaryIconElapsed;
        [NonSerialized] public bool iconFrozen;
        [NonSerialized] public bool secondaryIconFrozen;
    }

    [SerializeField] private List<Entry> active = new List<Entry>();
    [NonSerialized] private Dictionary<SpriteRenderer, Color> tintBaseColors;
    [SerializeField, Min(0)] private int iconSortingOrder = 30;

    public float DamageMultiplier => Product(0);
    public float AttackSpeedMultiplier => Product(1);
    public float MovementSpeedMultiplier => Product(2);
    public float IncomingDamageMultiplier => Product(3);

    private float Product(int field)
    {
        float result = 1f;
        foreach (Entry entry in active)
        {
            if (entry == null) continue;
            Snapshot s = entry.value;
            result *= Mathf.Max(0f, field == 0 ? s.damageMultiplier : field == 1 ? s.attackSpeedMultiplier :
                field == 2 ? s.movementSpeedMultiplier : s.incomingDamageMultiplier);
        }
        return result;
    }

    public bool Has(string id) => active.Exists(e => e != null && e.value.id == id);

    public bool TryGetRemaining(string id, out float remaining)
    {
        Entry entry = active.Find(e => e != null && e.value.id == id);
        remaining = entry != null ? Mathf.Max(0f, entry.value.remaining) : 0f;
        return entry != null;
    }

    public void Apply(Snapshot effect)
    {
        if (string.IsNullOrEmpty(effect.id) || effect.remaining <= 0f) return;
        effect.damageMultiplier = Positive(effect.damageMultiplier);
        effect.attackSpeedMultiplier = Positive(effect.attackSpeedMultiplier);
        effect.movementSpeedMultiplier = Positive(effect.movementSpeedMultiplier);
        effect.incomingDamageMultiplier = Positive(effect.incomingDamageMultiplier);
        Entry existing = active.Find(e => e != null && e.value.id == effect.id);
        if (existing != null)
        {
            existing.value = effect;
            RefreshVisual(existing);
            RefreshTint();
            return;
        }
        Entry entry = new Entry { value = effect };
        active.Add(entry);
        RefreshVisual(entry);
        RefreshTint();
    }

    /// <summary>Copies compatible effects with their precise remaining time; it never restarts them.</summary>
    public void CopyActiveTo(TimedCombatEffects target)
    {
        if (target == null) return;
        foreach (Entry entry in active)
            if (entry != null && entry.value.remaining > 0f)
                target.Apply(entry.value);
    }

    public void ClearAll()
    {
        foreach (Entry entry in active) DestroyVisual(entry);
        active.Clear();
        RefreshTint();
        tintBaseColors?.Clear();
    }

    /// <summary>Advances this host's independent timers. The match owner calls this only while combat is unpaused.</summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        bool removedTint = false;
        foreach (Entry activeEntry in active)
        {
            TickIcon(activeEntry, false, deltaTime);
            TickIcon(activeEntry, true, deltaTime);
        }
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Entry entry = active[i];
            if (entry == null) { active.RemoveAt(i); continue; }
            entry.value.remaining -= deltaTime;
            if (entry.value.remaining > 0f || HasIconPlayback(entry)) continue;
            DestroyVisual(entry);
            removedTint |= entry.value.hasTint;
            active.RemoveAt(i);
        }
        if (removedTint) RefreshTint();
        RepositionIcons();
    }

    private static bool HasIconPlayback(Entry entry)
    {
        if (HasUnfinishedIcon(entry.icon, entry.value.animationIconFrames, entry.iconElapsed, entry.value.iconFrameRate)) return true;
        if (HasUnfinishedIcon(entry.secondaryIcon, entry.value.secondaryAnimationIconFrames, entry.secondaryIconElapsed, entry.value.iconFrameRate)) return true;
        return false;
    }

    private static bool HasUnfinishedIcon(GameObject icon, Sprite[] frames, float elapsed, float frameRate) =>
        icon != null && frames != null && frames.Length > 0 && elapsed * Mathf.Max(1f, frameRate) < frames.Length;

    private void RefreshTint()
    {
        bool needsTint = active.Exists(e => e != null && e.value.hasTint);
        if (!needsTint && tintBaseColors == null) return;
        if (tintBaseColors == null) tintBaseColors = new Dictionary<SpriteRenderer, Color>();

        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>(allRenderers.Length);
        foreach (SpriteRenderer renderer in allRenderers)
            if (renderer != null && !IsEffectIconRenderer(renderer)) renderers.Add(renderer);
        HashSet<SpriteRenderer> current = new HashSet<SpriteRenderer>(renderers);
        List<SpriteRenderer> stale = new List<SpriteRenderer>();
        foreach (KeyValuePair<SpriteRenderer, Color> pair in tintBaseColors)
        {
            if (pair.Key == null) { stale.Add(pair.Key); continue; }
            if (!current.Contains(pair.Key)) { stale.Add(pair.Key); continue; }
            Color tint = Color.white;
            foreach (Entry entry in active)
                if (entry != null && entry.value.hasTint) tint *= entry.value.tintMultiplier;
            pair.Key.color = pair.Value * tint;
        }
        foreach (SpriteRenderer renderer in stale) tintBaseColors.Remove(renderer);
        if (needsTint)
        {
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || tintBaseColors.ContainsKey(renderer)) continue;
                Color original = renderer.color;
                tintBaseColors.Add(renderer, original);
                Color tint = Color.white;
                foreach (Entry entry in active)
                    if (entry != null && entry.value.hasTint) tint *= entry.value.tintMultiplier;
                renderer.color = original * tint;
            }
        }
    }

    private bool IsEffectIconRenderer(SpriteRenderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null && current != transform)
        {
            if (current.name.StartsWith("Effect icon - ", StringComparison.Ordinal)) return true;
            current = current.parent;
        }
        return false;
    }

    private void RefreshVisual(Entry entry)
    {
        DestroyVisual(entry);
        if (entry.value.visualPrefab != null)
        {
            entry.visual = Instantiate(entry.value.visualPrefab, transform);
            entry.visual.name = "Effect - " + entry.value.id;
        }
        CreateIcon(entry, false);
        CreateIcon(entry, true);
        RepositionIcons();
    }

    private void CreateIcon(Entry entry, bool secondary)
    {
        Sprite[] frames = secondary ? entry.value.secondaryAnimationIconFrames : entry.value.animationIconFrames;
        GameObject prefab = secondary ? entry.value.secondaryIconPrefab : entry.value.iconPrefab;
        if (prefab == null) return;
        SpriteRenderer actorRenderer = GetComponentInChildren<SpriteRenderer>();
        GameObject icon = Instantiate(prefab, transform, false);
        icon.name = "Effect icon - " + entry.value.id;
        icon.transform.localPosition = Vector3.zero;
        icon.transform.localScale = Vector3.one * Mathf.Max(0.01f, entry.value.iconSize);
        SpriteRenderer renderer = icon.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null)
        {
            Destroy(icon);
            return;
        }
        if (actorRenderer != null)
        {
            renderer.sortingLayerID = actorRenderer.sortingLayerID;
            renderer.sortingOrder = actorRenderer.sortingOrder + iconSortingOrder;
        }
        else renderer.sortingOrder = iconSortingOrder;
        if (frames != null && frames.Length > 0 && frames[0] != null) renderer.sprite = frames[0];
        // These supplied prefabs include a placeholder Animator controller with no states.
        // The configured sprite sequence below drives their SpriteRenderer directly.
        Animator animator = icon.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
        if (secondary) { entry.secondaryIcon = icon; entry.secondaryIconRenderer = renderer; }
        else { entry.icon = icon; entry.iconRenderer = renderer; }
    }

    private void TickIcon(Entry entry, bool secondary, float deltaTime)
    {
        if (entry == null) return;
        Sprite[] frames = secondary ? entry.value.secondaryAnimationIconFrames : entry.value.animationIconFrames;
        GameObject icon = secondary ? entry.secondaryIcon : entry.icon;
        SpriteRenderer renderer = secondary ? entry.secondaryIconRenderer : entry.iconRenderer;
        if (frames == null || frames.Length == 0 || icon == null || renderer == null) return;
        float elapsed = secondary ? entry.secondaryIconElapsed : entry.iconElapsed;
        bool frozen = secondary ? entry.secondaryIconFrozen : entry.iconFrozen;
        float frameRate = Mathf.Max(1f, entry.value.iconFrameRate);
        int freezeFrame = Mathf.Clamp(Mathf.RoundToInt((frames.Length - 1) * Mathf.Clamp01(entry.value.iconFreezeNormalizedTime)), 0, frames.Length - 1);
        if (frozen && entry.value.remaining <= 0f) frozen = false;
        if (!frozen)
        {
            elapsed += deltaTime;
            int frame = Mathf.FloorToInt(elapsed * frameRate);
            if (entry.value.remaining > 0f && frame >= freezeFrame)
            {
                frame = freezeFrame;
                frozen = true;
                elapsed = frame / frameRate;
            }
            if (frame >= frames.Length)
            {
                Destroy(icon);
                if (secondary) { entry.secondaryIcon = null; entry.secondaryIconRenderer = null; entry.secondaryIconElapsed = elapsed; }
                else { entry.icon = null; entry.iconRenderer = null; entry.iconElapsed = elapsed; }
                return;
            }
            if (frames[frame] != null) renderer.sprite = frames[frame];
        }
        if (secondary) { entry.secondaryIconFrozen = frozen; entry.secondaryIconElapsed = elapsed; }
        else { entry.iconFrozen = frozen; entry.iconElapsed = elapsed; }
    }

    private void RepositionIcons()
    {
        List<GameObject> icons = new List<GameObject>();
        foreach (Entry entry in active)
        {
            if (entry == null) continue;
            if (entry.icon != null) icons.Add(entry.icon);
            if (entry.secondaryIcon != null) icons.Add(entry.secondaryIcon);
        }
        if (icons.Count == 0) return;
        float spacing = 0.3f;
        Vector2 offset = Vector2.zero;
        foreach (Entry entry in active)
            if (entry != null) { offset = entry.value.iconOffset; spacing = Mathf.Max(0.01f, entry.value.iconVerticalSpacing); break; }
        // Keep adjacent icons visually separate even when the configured icon size is increased.
        float hostScaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        foreach (GameObject icon in icons)
        {
            SpriteRenderer renderer = icon.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) spacing = Mathf.Max(spacing, renderer.bounds.size.y / hostScaleY * 1.05f);
        }
        for (int i = 0; i < icons.Count; i++)
        {
            float centeredIndex = i - (icons.Count - 1) * 0.5f;
            icons[i].transform.localPosition = new Vector3(offset.x, offset.y - centeredIndex * spacing, 0f);
        }
    }

    private static void DestroyVisual(Entry entry)
    {
        if (entry != null && entry.visual != null) Destroy(entry.visual);
        if (entry != null && entry.icon != null) Destroy(entry.icon);
        if (entry != null && entry.secondaryIcon != null) Destroy(entry.secondaryIcon);
        if (entry != null) entry.visual = null;
    }

    private static float Positive(float value) => value <= 0f ? 1f : value;
}
