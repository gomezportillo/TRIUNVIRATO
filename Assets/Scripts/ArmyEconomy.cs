using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private EconomyConfig config;
    [SerializeField] private ArmyUpgradeManager upgrades;

    private readonly int[] balances = new int[3];
    private readonly float[] generationRemainders = new float[3];
    private float airMarShareRemainder;
    private float castleDamageRewardRemainder;

    public int StateRevision { get; private set; }

    public float GetPassiveIncomePerSecond(ArmyType army)
    {
        if (!IsValidArmy(army) || config == null) return 0f;
        float rate = config.GetPassiveIncomePerSecond(army) *
                     (1f + config.GetPassiveIncomeIncreasePerLevel(army) * GetIncomeUpgradeLevel(army));
        if (army == ArmyType.Aire && upgrades != null && upgrades.GetLevel(ArmyType.Tierra, "earth_air_passive_double") > 0)
            rate *= 2f;
        if (army == ArmyType.Mar && upgrades != null)
            rate += upgrades.GetSpecialEffectLevel("earth_mar_passive_unlock");
        return rate;
    }

    public int GetIncomeUpgradeLevel(ArmyType army) =>
        IsValidArmy(army) && upgrades != null ? upgrades.GetLevel(army, ArmyUpgradeKind.Income) : 0;

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

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<TriunviratoGame>();
        if (upgrades == null) upgrades = FindAnyObjectByType<ArmyUpgradeManager>();
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

    public bool TrySpendBundle(ArmyType firstArmy, int firstAmount, ArmyType secondArmy, int secondAmount)
    {
        int first = (int)firstArmy;
        int second = (int)secondArmy;
        if (game == null || !game.CanCombat || first < 0 || first >= balances.Length ||
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
        RewardWaveCompleted(1f);
    }

    public void RewardWaveCompleted(float marRewardMultiplier)
    {
        if (config != null)
            AddCoins(ArmyType.Mar, Mathf.CeilToInt(GetWaveReward(ArmyType.Mar) * Mathf.Max(1f, marRewardMultiplier)),
                marRewardMultiplier > 1f ? "oleada completada con llamada anticipada" : "oleada completada");
    }

    public void RewardCastleDamaged(float actualDamage)
    {
        float coinsPerDamage = upgrades != null ? upgrades.GetSpecialEffectLevel("mar_castle_damage_income") : 0f;
        if (actualDamage <= 0f || coinsPerDamage <= 0f)
            return;

        castleDamageRewardRemainder += actualDamage * coinsPerDamage;
        int coins = Mathf.FloorToInt(castleDamageRewardRemainder);
        if (coins <= 0) return;
        castleDamageRewardRemainder -= coins;
        AddCoins(ArmyType.Mar, coins, "daño real recibido por el castillo");
    }

    private void AddCoins(ArmyType army, int amount, string reason)
    {
        int index = (int)army;
        if (amount <= 0 || index < 0 || index >= balances.Length)
            return;

        balances[index] += amount;
        StateRevision++;
        Debug.Log("Economía " + army + ": +" + amount + " moneda(s) por " + reason + ". Saldo: " + balances[index] + ".", this);

        if (army == ArmyType.Mar && upgrades != null)
        {
            float redCoinsPerBlue = upgrades.GetSpecialEffectLevel("air_mar_income_share");
            if (redCoinsPerBlue > 0f)
            {
                airMarShareRemainder += amount * redCoinsPerBlue;
                int redCoins = Mathf.FloorToInt(airMarShareRemainder);
                if (redCoins > 0)
                {
                    airMarShareRemainder -= redCoins;
                    balances[(int)ArmyType.Aire] += redCoins;
                    StateRevision++;
                    Debug.Log("Economía Aire: +" + redCoins + " moneda(s) por sinergia con Mar. Saldo: " +
                              balances[(int)ArmyType.Aire] + ".", this);
                }
            }
        }
    }

    public void ResetEconomy()
    {
        for (int i = 0; i < balances.Length; i++)
        {
            ArmyType army = (ArmyType)i;
            balances[i] = config != null ? config.GetInitialBalance(army) : 0;
            generationRemainders[i] = 0f;
        }
        airMarShareRemainder = 0f;
        castleDamageRewardRemainder = 0f;
        StateRevision++;
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
