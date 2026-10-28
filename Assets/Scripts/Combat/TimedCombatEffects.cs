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
        public bool hasTint;
        public Color tintMultiplier;
    }

    [Serializable]
    private sealed class Entry
    {
        public Snapshot value;
        [NonSerialized] public GameObject visual;
    }

    [SerializeField] private List<Entry> active = new List<Entry>();
    [NonSerialized] private Dictionary<SpriteRenderer, Color> tintBaseColors;

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
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Entry entry = active[i];
            if (entry == null) { active.RemoveAt(i); continue; }
            entry.value.remaining -= deltaTime;
            if (entry.value.remaining > 0f) continue;
            DestroyVisual(entry);
            removedTint |= entry.value.hasTint;
            active.RemoveAt(i);
        }
        if (removedTint) RefreshTint();
    }

    private void RefreshTint()
    {
        bool needsTint = active.Exists(e => e != null && e.value.hasTint);
        if (!needsTint && tintBaseColors == null) return;
        if (tintBaseColors == null) tintBaseColors = new Dictionary<SpriteRenderer, Color>();

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
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

    private void RefreshVisual(Entry entry)
    {
        DestroyVisual(entry);
        if (entry.value.visualPrefab != null)
        {
            entry.visual = Instantiate(entry.value.visualPrefab, transform);
            entry.visual.name = "Effect - " + entry.value.id;
        }
    }

    private static void DestroyVisual(Entry entry)
    {
        if (entry != null && entry.visual != null) Destroy(entry.visual);
        if (entry != null) entry.visual = null;
    }

    private static float Positive(float value) => value <= 0f ? 1f : value;
}
