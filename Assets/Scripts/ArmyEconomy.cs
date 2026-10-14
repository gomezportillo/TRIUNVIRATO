using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private EconomyConfig config;

    private readonly int[] balances = new int[3];
    private readonly float[] generationRemainders = new float[3];
    private readonly int[] incomeUpgradeLevels = new int[3];

    public int StateRevision { get; private set; }

    public float GetPassiveIncomePerSecond(ArmyType army) =>
        IsValidArmy(army) && config != null
            ? config.GetPassiveIncomePerSecond(army) *
              (1f + config.GetPassiveIncomeIncreasePerLevel(army) * incomeUpgradeLevels[(int)army])
            : 0f;

    public int GetIncomeUpgradeLevel(ArmyType army) =>
        IsValidArmy(army) ? incomeUpgradeLevels[(int)army] : 0;

    public int GetEnemyKillReward(ArmyType army) =>
        config != null ? config.GetEnemyKillReward(army) +
                         config.GetEnemyKillRewardIncreasePerLevel(army) * GetIncomeUpgradeLevel(army) : 0;

    public int GetWaveReward(ArmyType army) =>
        config != null ? config.GetWaveReward(army) +
                         config.GetWaveRewardIncreasePerLevel(army) * GetIncomeUpgradeLevel(army) : 0;

    public int GetBossReward(ArmyType army) =>
        config != null ? config.GetBossReward(army) +
                         config.GetBossRewardIncreasePerLevel(army) * GetIncomeUpgradeLevel(army) : 0;

    public EconomyConfig Config => config;

    private void Update()
    {
        if (config == null || game == null || !game.CanCombat)
            return;

        bool balanceChanged = false;
        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            generationRemainders[i] += GetPassiveIncomePerSecond(army) * Time.deltaTime;
            int coinsEarned = Mathf.FloorToInt(generationRemainders[i]);
            if (coinsEarned > 0)
            {
                balances[i] += coinsEarned;
                generationRemainders[i] -= coinsEarned;
                balanceChanged = true;
                Debug.Log("Economía " + army + ": +" + coinsEarned + " moneda(s) por ingreso pasivo. Saldo: " + balances[i] + ".", this);
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
        if (amount < 0 || index < 0 || index >= balances.Length || balances[index] < amount ||
            (game != null && !game.CanCombat))
            return false;

        balances[index] -= amount;
        StateRevision++;
        Debug.Log("Economía " + army + ": -" + amount + " moneda(s). Saldo: " + balances[index] + ".", this);
        return true;
    }

    public void Refund(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount > 0 && index >= 0 && index < balances.Length)
        {
            balances[index] += amount;
            StateRevision++;
            Debug.Log("Economía " + army + ": reembolso de " + amount + " moneda(s). Saldo: " + balances[index] + ".", this);
        }
    }

    public void RewardEnemyDefeated(bool isBoss)
    {
        if (config == null)
            return;

        AddCoins(ArmyType.Aire, GetEnemyKillReward(ArmyType.Aire), "baja");
        if (isBoss)
            AddCoins(ArmyType.Mar, GetBossReward(ArmyType.Mar), "jefe derrotado");
    }

    public void RewardWaveCompleted()
    {
        if (config != null)
            AddCoins(ArmyType.Mar, GetWaveReward(ArmyType.Mar), "oleada completada");
    }

    internal void SetIncomeUpgradeLevel(ArmyType army, int level)
    {
        if (!IsValidArmy(army))
            return;
        incomeUpgradeLevels[(int)army] = Mathf.Max(0, level);
        StateRevision++;
    }

    private void AddCoins(ArmyType army, int amount, string reason)
    {
        int index = (int)army;
        if (amount <= 0 || index < 0 || index >= balances.Length)
            return;

        balances[index] += amount;
        StateRevision++;
        Debug.Log("Economía " + army + ": +" + amount + " moneda(s) por " + reason + ". Saldo: " + balances[index] + ".", this);
    }

    public void ResetEconomy()
    {
        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            balances[i] = config != null ? config.GetInitialBalance(army) : 0;
            generationRemainders[i] = 0f;
            incomeUpgradeLevels[i] = 0;
        }
        StateRevision++;
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
