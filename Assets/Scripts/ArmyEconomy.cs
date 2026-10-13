using UnityEngine;

/// <summary>Owns the independent, continuously generated balances for each army.</summary>
public sealed class ArmyEconomy : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField, Min(0f)] private float coinsPerSecond = 1f;

    private readonly float[] balances = new float[3];

    private void Update()
    {
        if (game == null || game.CanCombat)
        {
            for (int i = 0; i < balances.Length; i++)
                balances[i] += coinsPerSecond * Time.deltaTime;
        }
    }

    public int GetWholeCoins(ArmyType army) => Mathf.FloorToInt(balances[(int)army]);

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
            balances[i] = 0f;
    }
}
