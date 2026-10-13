using UnityEngine;

/// <summary>Validates purchases and creates tower entities at their scene-authored placement anchors.</summary>
public sealed class TowerShop : MonoBehaviour
{
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private Transform groundTower;
    [SerializeField] private Transform airTower;
    [SerializeField] private Transform seaTower;
    [SerializeField] private Sprite groundSprite;
    [SerializeField] private Sprite airSprite;
    [SerializeField] private Sprite seaSprite;
    [SerializeField, Min(0.01f)] private float groundTowerSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float airTowerSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float seaTowerSizeMultiplier = 1f;

    private readonly System.Collections.Generic.List<GameObject> purchasedTowers =
        new System.Collections.Generic.List<GameObject>();
    private static readonly int[] Costs = { 10, 15, 20 };

    public bool TryPurchase(ArmyType army) => TryPurchase(army, out _);

    public bool TryPurchase(ArmyType army, out string failureReason)
    {
        failureReason = null;
        int index = (int)army;
        if (index < 0 || index >= Costs.Length)
        {
            failureReason = "Tipo de torre desconocido.";
            return false;
        }

        if (economy == null || game == null || !game.CanCombat)
        {
            failureReason = "La partida no está disponible para comprar.";
            return false;
        }

        if (!economy.TrySpend(army, Costs[index]))
        {
            failureReason = "Saldo insuficiente.";
            return false;
        }

        Transform anchor = GetAnchor(army);
        Sprite sprite = GetSprite(army);
        if (anchor == null || sprite == null)
        {
            // Scene wiring errors must never consume a player's balance.
            economy.Refund(army, Costs[index]);
            failureReason = "La torre no está configurada en la escena.";
            return false;
        }

        GameObject tower = new GameObject("Torre - " + army);
        tower.transform.position = anchor.position + GetPlacementOffset(army);
        SpriteRenderer renderer = tower.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;
        float spriteHeight = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        tower.transform.localScale = Vector3.one * (1.2f * GetSizeMultiplier(army) / Mathf.Max(0.01f, spriteHeight));
        DefenseTower behavior = tower.AddComponent<DefenseTower>();
        game.ConfigureTower(behavior, army);
        purchasedTowers.Add(tower);
        return true;
    }

    private Transform GetAnchor(ArmyType army) => army == ArmyType.Tierra ? groundTower :
        army == ArmyType.Aire ? airTower : seaTower;

    private Sprite GetSprite(ArmyType army) => army == ArmyType.Tierra ? groundSprite :
        army == ArmyType.Aire ? airSprite : seaSprite;

    private float GetSizeMultiplier(ArmyType army) => army == ArmyType.Tierra ? groundTowerSizeMultiplier :
        army == ArmyType.Aire ? airTowerSizeMultiplier : seaTowerSizeMultiplier;

    private Vector3 GetPlacementOffset(ArmyType army)
    {
        int sameArmyCount = 0;
        foreach (GameObject tower in purchasedTowers)
        {
            DefenseTower behavior = tower != null ? tower.GetComponent<DefenseTower>() : null;
            if (behavior != null && behavior.Army == army)
                sameArmyCount++;
        }
        return new Vector3(0.65f * sameArmyCount, 0.45f * sameArmyCount, 0f);
    }

    public void ResetPurchasedTowers()
    {
        foreach (GameObject tower in purchasedTowers)
        {
            if (tower != null)
                Destroy(tower);
        }
        purchasedTowers.Clear();
    }
}
