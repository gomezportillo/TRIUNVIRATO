using UnityEngine;

/// <summary>Creates one tower per army and routes later purchases into tower upgrades.</summary>
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

    private static readonly int[] BaseCosts = { 10, 15, 20 };
    private const float UpgradePercent = 0.01f;

    private readonly DefenseTower[] towers = new DefenseTower[3];
    private readonly int[] upgradeLevels = new int[3];
    private readonly int[] nextCosts = { 10, 15, 20 };

    public int StateRevision { get; private set; }

    public bool HasTower(ArmyType army)
    {
        int index = (int)army;
        return index >= 0 && index < towers.Length && towers[index] != null;
    }

    public int GetUpgradeLevel(ArmyType army)
    {
        int index = (int)army;
        return index >= 0 && index < upgradeLevels.Length && HasTower(army) ? upgradeLevels[index] : 0;
    }

    public int GetNextCost(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= BaseCosts.Length)
            return 0;

        return nextCosts[index];
    }

    public float GetCurrentDamage(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length || game == null)
            return 0f;

        return HasTower(army) ? towers[index].Damage : game.GetBaseTowerDamage(army);
    }

    public string GetButtonLabel(ArmyType army)
    {
        string name = GetArmyName(army);
        bool owned = HasTower(army);
        string damage = GetCurrentDamage(army).ToString("0.##") + (owned ? " +1%" : "");
        return (owned ? "Mejorar torre de " : "Comprar torre de ") + name +
               "\nDaño:  " + damage +
               "\nCoste: " + GetNextCost(army) + " " + GetCoinIcon(army);
    }

    public bool TryPurchase(ArmyType army) => TryPurchase(army, out _);

    public bool TryPurchase(ArmyType army, out string failureReason)
    {
        failureReason = null;
        int index = (int)army;
        if (index < 0 || index >= BaseCosts.Length)
        {
            failureReason = "Tipo de torre desconocido.";
            return false;
        }

        if (economy == null || game == null || !game.CanCombat)
        {
            failureReason = "La partida no está disponible para comprar.";
            return false;
        }

        bool isUpgrade = HasTower(army);
        int cost = GetNextCost(army);
        if (!isUpgrade)
        {
            Transform anchor = GetAnchor(army);
            Sprite sprite = GetSprite(army);
            if (anchor == null || sprite == null)
            {
                failureReason = "La torre no está configurada en la escena.";
                return false;
            }

            if (!economy.TrySpend(army, cost))
            {
                failureReason = "Saldo insuficiente: necesitas " + cost.ToString("0.##") + " monedas.";
                return false;
            }

            GameObject towerObject = new GameObject("Torre - " + army);
            towerObject.transform.position = anchor.position;
            SpriteRenderer renderer = towerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 3;
            float spriteHeight = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            towerObject.transform.localScale = Vector3.one *
                (1.2f * GetSizeMultiplier(army) / Mathf.Max(0.01f, spriteHeight));
            AddOutline(towerObject.transform, renderer, sprite);
            DefenseTower behavior = towerObject.AddComponent<DefenseTower>();
            game.ConfigureTower(behavior, army);
            towers[index] = behavior;
            upgradeLevels[index] = 0;
            nextCosts[index] = IncreaseCost(cost);
        }
        else
        {
            if (!economy.TrySpend(army, cost))
            {
                failureReason = "Saldo insuficiente: necesitas " + cost.ToString("0.##") + " monedas.";
                return false;
            }

            upgradeLevels[index]++;
            towers[index].SetDamageMultiplier(Mathf.Pow(1f + UpgradePercent, upgradeLevels[index]));
            nextCosts[index] = IncreaseCost(cost);
        }

        StateRevision++;
        return true;
    }

    private static int IncreaseCost(int currentCost)
    {
        int roundedIncrease = Mathf.CeilToInt(currentCost * UpgradePercent);
        return currentCost + Mathf.Max(1, roundedIncrease);
    }

    private static string GetCoinIcon(ArmyType army) => army == ArmyType.Tierra ? "🟢" :
        army == ArmyType.Aire ? "🔴" : "🔵";

    private static void AddOutline(Transform tower, SpriteRenderer towerRenderer, Sprite sprite)
    {
        Vector2[] directions =
        {
            Vector2.right,
            (Vector2.right + Vector2.up).normalized,
            Vector2.up,
            (Vector2.left + Vector2.up).normalized,
            Vector2.left,
            (Vector2.left + Vector2.down).normalized,
            Vector2.down,
            (Vector2.right + Vector2.down).normalized
        };
        float localPixelSize = Mathf.Min(
            sprite.bounds.size.x / sprite.rect.width,
            sprite.bounds.size.y / sprite.rect.height);
        float outlineOffset = localPixelSize * 1.1f;

        for (int i = 0; i < directions.Length; i++)
        {
            GameObject outline = new GameObject("Outline " + i);
            outline.transform.SetParent(tower, false);
            outline.transform.localPosition = directions[i] * outlineOffset;
            SpriteRenderer outlineRenderer = outline.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = sprite;
            outlineRenderer.color = Color.black;
            outlineRenderer.sortingLayerID = towerRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = towerRenderer.sortingOrder - 1;
            outlineRenderer.maskInteraction = towerRenderer.maskInteraction;
        }
    }

    private Transform GetAnchor(ArmyType army) => army == ArmyType.Tierra ? groundTower :
        army == ArmyType.Aire ? airTower : seaTower;

    private Sprite GetSprite(ArmyType army) => army == ArmyType.Tierra ? groundSprite :
        army == ArmyType.Aire ? airSprite : seaSprite;

    private float GetSizeMultiplier(ArmyType army) => army == ArmyType.Tierra ? groundTowerSizeMultiplier :
        army == ArmyType.Aire ? airTowerSizeMultiplier : seaTowerSizeMultiplier;

    public void ResetPurchasedTowers()
    {
        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] != null)
                Destroy(towers[i].gameObject);
            towers[i] = null;
            upgradeLevels[i] = 0;
            nextCosts[i] = BaseCosts[i];
        }
        StateRevision++;
    }

    private static string GetArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" :
        army == ArmyType.Aire ? "Aire" : "Mar";
}
