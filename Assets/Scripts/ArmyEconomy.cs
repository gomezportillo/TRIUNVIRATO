using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField, Min(0f)] private float coinsPerSecond = 1f;

    private readonly int[] balances = new int[3];
    private float generationRemainder;

    private void Update()
    {
        if (game == null || game.CanCombat)
        {
            generationRemainder += coinsPerSecond * Time.deltaTime;
            int coinsEarned = Mathf.FloorToInt(generationRemainder);
            if (coinsEarned > 0)
            {
                for (int i = 0; i < balances.Length; i++)
                    balances[i] += coinsEarned;
                generationRemainder -= coinsEarned;
            }
        }
    }

    public int GetCoins(ArmyType army) => balances[(int)army];

    public int GetWholeCoins(ArmyType army) => GetCoins(army);

    public bool TrySpend(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount < 0 || index < 0 || index >= balances.Length || balances[index] < amount)
            return false;

        balances[index] -= amount;
        return true;
    }

    public void Refund(ArmyType army, int amount)
    {
        int index = (int)army;
        if (amount > 0 && index >= 0 && index < balances.Length)
            balances[index] += amount;
    }

    public void ResetEconomy()
    {
        for (int i = 0; i < balances.Length; i++)
            balances[i] = 0;
        generationRemainder = 0f;
    }
}
