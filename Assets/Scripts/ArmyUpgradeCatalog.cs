using System;
using UnityEngine;

public enum ArmyUpgradeSection
{
    Hero,
    Resources
}

/// <summary>Design-time definitions for cross-army and secondary upgrades.</summary>
[CreateAssetMenu(fileName = "ArmyUpgradeCatalog", menuName = "TRIUNVIRATO/Economy/Army Upgrade Catalog")]
public sealed class ArmyUpgradeCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Definition
    {
#pragma warning disable 0649
        public string id;
        public ArmyType army;
        public ArmyUpgradeSection section;
        public string displayName;
        [TextArea] public string description;
        [Min(1)] public int maximumLevel = 5;
        [Min(1)] public int baseCost = 15;
        [Min(0f)] public float costGrowthRate = 0.1f;
        [Min(1)] public int minimumCostIncrease = 1;
        public ArmyType secondaryArmy;
        [Min(0)] public int secondaryBaseCost;
        [Min(0f)] public float effectPerLevel;
#pragma warning restore 0649
    }

    [SerializeField] private Definition[] definitions = Array.Empty<Definition>();

    public Definition[] Definitions => definitions;

    public Definition Find(string id)
    {
        if (string.IsNullOrEmpty(id) || definitions == null)
            return null;
        foreach (Definition definition in definitions)
        {
            if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal))
                return definition;
        }
        return null;
    }

    public int GetCost(Definition definition, int level) => definition == null ? 0 :
        UpgradePrice.GetCost(definition.baseCost, definition.costGrowthRate, definition.minimumCostIncrease, level);

    public int GetSecondaryCost(Definition definition, int level)
       => definition == null || definition.secondaryBaseCost <= 0 ? 0 :
        UpgradePrice.GetCost(definition.secondaryBaseCost, definition.costGrowthRate, definition.minimumCostIncrease, level);
}
