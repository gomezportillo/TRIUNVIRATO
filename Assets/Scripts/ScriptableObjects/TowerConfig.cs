using UnityEngine;

/// <summary>Design data for a tower and its independent combat base damage.</summary>
[CreateAssetMenu(fileName = "TowerConfig", menuName = "TRIUNVIRATO/Combat/Tower Config")]
public sealed class TowerConfig : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private ArmyType army;
    [SerializeField] private HeroConfig hero;
    [SerializeField, Min(0f)] private float baseDamage = 1f;

    public string Id => id;
    public string DisplayName => displayName;
    public ArmyType Army => army;
    public HeroConfig Hero => hero;
    public float BaseDamage => baseDamage;

    public float GetBaseDamage() => baseDamage;

}
