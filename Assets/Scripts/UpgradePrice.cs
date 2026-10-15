using UnityEngine;

/// <summary>Shared integer price progression for level-based upgrades.</summary>
public static class UpgradePrice
{
    public static int GetCost(int baseCost, float growthRate, int minimumIncrease, int level)
    {
        int cost = Mathf.Max(0, baseCost);
        int floor = Mathf.Max(1, minimumIncrease);
        for (int i = 0; i < Mathf.Max(0, level); i++)
            cost += Mathf.Max(floor, Mathf.CeilToInt(cost * Mathf.Max(0f, growthRate)));
        return cost;
    }
}
