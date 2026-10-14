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
        [Header("Mejora de ingresos")]
        [Min(0)] public int incomeUpgradeBaseCost;
        [Min(1f)] public float incomeUpgradeCostMultiplier = 2f;
        [Min(0)] public int incomeUpgradeMaximumLevel = 5;
        [Min(0f)] public float passiveIncomeIncreasePerLevel;
        [Min(0)] public int enemyKillRewardIncreasePerLevel;
        [Min(0)] public int waveRewardIncreasePerLevel;
        [Min(0)] public int bossRewardIncreasePerLevel;
#pragma warning restore 0649
    }

    [SerializeField] private ArmySettings[] armies = new ArmySettings[3];

    public int GetInitialBalance(ArmyType army) => GetSettings(army)?.initialBalance ?? 0;
    public float GetPassiveIncomePerSecond(ArmyType army) => GetSettings(army)?.passiveIncomePerSecond ?? 0f;
    public int GetEnemyKillReward(ArmyType army) => GetSettings(army)?.rewardPerEnemyKill ?? 0;
    public int GetWaveReward(ArmyType army) => GetSettings(army)?.rewardPerWaveCompleted ?? 0;
    public int GetBossReward(ArmyType army) => GetSettings(army)?.additionalRewardPerBossDefeated ?? 0;
    public int GetIncomeUpgradeBaseCost(ArmyType army) => GetSettings(army)?.incomeUpgradeBaseCost ?? 0;
    public float GetIncomeUpgradeCostMultiplier(ArmyType army) => GetSettings(army)?.incomeUpgradeCostMultiplier ?? 2f;
    public int GetIncomeUpgradeMaximumLevel(ArmyType army) => GetSettings(army)?.incomeUpgradeMaximumLevel ?? 0;
    public float GetPassiveIncomeIncreasePerLevel(ArmyType army) => GetSettings(army)?.passiveIncomeIncreasePerLevel ?? 0f;
    public int GetEnemyKillRewardIncreasePerLevel(ArmyType army) => GetSettings(army)?.enemyKillRewardIncreasePerLevel ?? 0;
    public int GetWaveRewardIncreasePerLevel(ArmyType army) => GetSettings(army)?.waveRewardIncreasePerLevel ?? 0;
    public int GetBossRewardIncreasePerLevel(ArmyType army) => GetSettings(army)?.bossRewardIncreasePerLevel ?? 0;

    public int GetIncomeUpgradeCost(ArmyType army, int currentLevel) =>
        Mathf.CeilToInt(GetIncomeUpgradeBaseCost(army) *
                        Mathf.Pow(GetIncomeUpgradeCostMultiplier(army), Mathf.Max(0, currentLevel)));

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
