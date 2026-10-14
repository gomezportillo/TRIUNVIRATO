using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private EconomyConfig config;

    private readonly int[] balances = new int[3];
    private readonly float[] generationRemainders = new float[3];

    public int StateRevision { get; private set; }

    public float GetPassiveIncomePerSecond(ArmyType army) =>
        IsValidArmy(army) && config != null ? config.GetPassiveIncomePerSecond(army) : 0f;

    private void Update()
    {
        if (config == null || (game != null && !game.CanCombat))
            return;

        bool balanceChanged = false;
        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            generationRemainders[i] += config.GetPassiveIncomePerSecond(army) * Time.deltaTime;
            int coinsEarned = Mathf.FloorToInt(generationRemainders[i]);
            if (coinsEarned > 0)
            {
                balances[i] += coinsEarned;
                generationRemainders[i] -= coinsEarned;
                balanceChanged = true;
            }
        }

        if (balanceChanged)
            StateRevision++;
    }

    public int GetCoins(ArmyType army) => IsValidArmy(army) ? balances[(int)army] : 0;

    public int GetWholeCoins(ArmyType army) => GetCoins(army);

    public float GetFractionalCoins(ArmyType army) =>
        IsValidArmy(army) ? generationRemainders[(int)army] : 0f;

    public bool TrySpend(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount < 0 || index < 0 || index >= balances.Length || balances[index] < amount)
            return false;

        balances[index] -= amount;
        StateRevision++;
        return true;
    }

    public void Refund(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount > 0 && index >= 0 && index < balances.Length)
        {
            balances[index] += amount;
            StateRevision++;
        }
    }

    public void RewardEnemyDefeated(bool isBoss)
    {
        if (config == null)
            return;

        AddCoins(ArmyType.Aire, config.GetEnemyKillReward(ArmyType.Aire));
        if (isBoss)
            AddCoins(ArmyType.Mar, config.GetBossReward(ArmyType.Mar));
    }

    public void RewardWaveCompleted()
    {
        if (config != null)
            AddCoins(ArmyType.Mar, config.GetWaveReward(ArmyType.Mar));
    }

    private void AddCoins(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount <= 0 || index < 0 || index >= balances.Length)
            return;

        balances[index] += amount;
        StateRevision++;
    }

    public void ResetEconomy()
    {
        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            balances[i] = config != null ? config.GetInitialBalance(army) : 0;
            generationRemainders[i] = 0f;
        }
        StateRevision++;
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
