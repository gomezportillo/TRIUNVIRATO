using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private EconomyConfig config;

    private readonly int[] balances = new int[3];
    private readonly float[] generationRemainders = new float[3];
    private readonly float[] killRewardRemainders = new float[3];
    private float waveRewardRemainder;

    public int StateRevision { get; private set; }

    public float GetPassiveIncomePerSecond(ArmyType army)
    {
        if (!IsValidArmy(army) || config == null) return 0f;
        return config.GetPassiveIncomePerSecond(army) *
               (game != null ? game.GetEconomyIntermissionMultiplier(army) : 1f) +
               (game != null ? game.GetEconomyPassiveFixedBonus(army) : 0f);
    }

    public int GetEnemyKillReward(ArmyType army)
    {
        return Mathf.FloorToInt(GetEffectiveEnemyKillReward(army));
    }

    public float GetEffectiveEnemyKillReward(ArmyType army)
    {
        if (config == null) return 0f;
        float baseReward = army == ArmyType.Aire ? config.GetEnemyKillReward(army) : 0f;
        return baseReward * (game != null ? game.GetIntermissionKillRewardMultiplier(army) : 1f) +
               (game != null ? game.GetIntermissionKillRewardFixedBonus(army) : 0f);
    }

    public int GetWaveReward(ArmyType army) =>
        config != null ? config.GetWaveReward(army) : 0;

    /// <summary>Returns the configured next-wave payout using the same modifiers and rounding as rewards.</summary>
    public int GetEffectiveWaveReward(ArmyType army) =>
        config == null || army != ArmyType.Mar ? 0 : Mathf.CeilToInt(
            config.GetWaveReward(army) * (game != null ? game.GetIntermissionWaveRewardMultiplier(army) * game.CurrentWaveRewardMultiplier : 1f) +
            (game != null ? game.GetEconomyWaveRewardFixedBonus(army) : 0f) + waveRewardRemainder);

    public int GetBossReward(ArmyType army) =>
        config != null ? config.GetBossReward(army) : 0;

    public EconomyConfig Config => config;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<TriunviratoGame>();
    }

    private void Update()
    {
        if (config == null || game == null || !game.CanCombat)
            return;

        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            generationRemainders[i] += GetPassiveIncomePerSecond(army) * Time.deltaTime;
            int coinsEarned = Mathf.FloorToInt(generationRemainders[i]);
            if (coinsEarned > 0)
            {
                generationRemainders[i] -= coinsEarned;
                AddCoins(army, coinsEarned, "ingreso pasivo");
            }
        }
    }

    public int GetCoins(ArmyType army) => IsValidArmy(army) ? balances[(int)army] : 0;

    public int GetWholeCoins(ArmyType army) => GetCoins(army);

    public void GrantIntermissionResources(ArmyType army, int amount)
    {
        if (game == null || !game.IsPurchasePhase || !IsValidArmy(army) || amount <= 0) return;
        AddCoins(army, amount, "elección de recursos entre oleadas");
    }

    public float GetFractionalCoins(ArmyType army) =>
        IsValidArmy(army) ? generationRemainders[(int)army] : 0f;

    public bool TrySpend(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount < 0 || index < 0 || index >= balances.Length || balances[index] < amount ||
            game == null || !game.IsPurchasePhase)
            return false;

        balances[index] -= amount;
        StateRevision++;
        Debug.Log("Economía " + army + ": -" + amount + " moneda(s). Saldo: " + balances[index] + ".", this);
        return true;
    }

    public bool TrySpendBundle(ArmyType firstArmy, int firstAmount, ArmyType secondArmy, int secondAmount)
    {
        int first = (int)firstArmy;
        int second = (int)secondArmy;
        if (game == null || !game.IsPurchasePhase || first < 0 || first >= balances.Length ||
            second < 0 || second >= balances.Length || firstAmount < 0 || secondAmount < 0)
            return false;

        if (first == second)
        {
            firstAmount += secondAmount;
            secondAmount = 0;
        }
        if (balances[first] < firstAmount || balances[second] < secondAmount)
            return false;

        balances[first] -= firstAmount;
        balances[second] -= secondAmount;
        StateRevision++;
        Debug.Log("Economía " + firstArmy + ": -" + firstAmount + "; " + secondArmy + ": -" + secondAmount +
                  ". Saldos: " + balances[first] + " / " + balances[second] + ".", this);
        return true;
    }

    /// <summary>Atomically spends a planning purchase across green, red, and blue balances.</summary>
    public bool TrySpendIntermissionResources(int greenCost, int redCost, int blueCost)
    {
        if (game == null || !game.IsPurchasePhase || greenCost < 0 || redCost < 0 || blueCost < 0 ||
            balances[(int)ArmyType.Tierra] < greenCost || balances[(int)ArmyType.Aire] < redCost ||
            balances[(int)ArmyType.Mar] < blueCost)
            return false;

        balances[(int)ArmyType.Tierra] -= greenCost;
        balances[(int)ArmyType.Aire] -= redCost;
        balances[(int)ArmyType.Mar] -= blueCost;
        StateRevision++;
        Debug.Log("Compra entre oleadas: -" + greenCost + " verde, -" + redCost + " rojo, -" + blueCost +
                  " azul. Saldos: " + balances[(int)ArmyType.Tierra] + " / " + balances[(int)ArmyType.Aire] +
                  " / " + balances[(int)ArmyType.Mar] + ".", this);
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

        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            float total = GetEffectiveEnemyKillReward(army) + killRewardRemainders[i];
            int coins = Mathf.FloorToInt(total);
            killRewardRemainders[i] = total - coins;
            AddCoins(army, coins, "baja");
        }
        if (isBoss)
            AddCoins(ArmyType.Mar, GetBossReward(ArmyType.Mar), "jefe derrotado");
    }

    public void RewardWaveCompleted()
    {
        RewardWaveCompleted(1f);
    }

    public void RewardWaveCompleted(float marRewardMultiplier)
    {
        if (config != null)
        {
            float reward = GetWaveReward(ArmyType.Mar) *
                           (game != null ? game.GetIntermissionWaveRewardMultiplier(ArmyType.Mar) : 1f) * Mathf.Max(1f, marRewardMultiplier) +
                           (game != null ? game.GetEconomyWaveRewardFixedBonus(ArmyType.Mar) : 0f) + waveRewardRemainder;
            int wholeReward = Mathf.CeilToInt(reward);
            waveRewardRemainder = reward - wholeReward;
            AddCoins(ArmyType.Mar, wholeReward,
                marRewardMultiplier > 1f ? "oleada completada con llamada anticipada" : "oleada completada");
        }
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
            killRewardRemainders[i] = 0f;
        }
        waveRewardRemainder = 0f;
        StateRevision++;
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
