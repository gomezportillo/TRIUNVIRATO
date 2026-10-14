using System;
using UnityEngine;

/// <summary>Design-time income and reward values; runtime balances stay in ArmyEconomy.</summary>
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "TRIUNVIRATO/Economy/Economy Config")]
public sealed class EconomyConfig : ScriptableObject
{
    [Serializable]
    private sealed class ArmySettings
    {
#pragma warning disable 0649 // Unity deserializes the nested settings from this asset.
        public ArmyType army;
        [Min(0)] public int initialBalance;
        [Min(0f)] public float passiveIncomePerSecond;
        [Min(0)] public int rewardPerEnemyKill;
        [Min(0)] public int rewardPerWaveCompleted;
        [Min(0)] public int additionalRewardPerBossDefeated;
        [Min(0f)] public float futurePassiveIncomeUpgradeAmount;
        [Min(0)] public int futurePassiveIncomeUpgradeCost;
#pragma warning restore 0649
    }

    [SerializeField] private ArmySettings[] armies = new ArmySettings[3];

    public int GetInitialBalance(ArmyType army) => GetSettings(army)?.initialBalance ?? 0;
    public float GetPassiveIncomePerSecond(ArmyType army) => GetSettings(army)?.passiveIncomePerSecond ?? 0f;
    public int GetEnemyKillReward(ArmyType army) => GetSettings(army)?.rewardPerEnemyKill ?? 0;
    public int GetWaveReward(ArmyType army) => GetSettings(army)?.rewardPerWaveCompleted ?? 0;
    public int GetBossReward(ArmyType army) => GetSettings(army)?.additionalRewardPerBossDefeated ?? 0;
    public float GetFuturePassiveIncomeUpgradeAmount(ArmyType army) =>
        GetSettings(army)?.futurePassiveIncomeUpgradeAmount ?? 0f;
    public int GetFuturePassiveIncomeUpgradeCost(ArmyType army) =>
        GetSettings(army)?.futurePassiveIncomeUpgradeCost ?? 0;

    private ArmySettings GetSettings(ArmyType army)
    {
        if (armies == null)
            return null;

        for (int i = 0; i < armies.Length; i++)
        {
            if (armies[i] != null && armies[i].army == army)
                return armies[i];
        }

        return null;
    }
}
