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
    [SerializeField] private TowerConfig groundConfig;
    [SerializeField] private TowerConfig airConfig;
    [SerializeField] private TowerConfig seaConfig;
    [SerializeField, Min(0.01f)] private float groundTowerSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float airTowerSizeMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float seaTowerSizeMultiplier = 1f;

    private readonly DefenseTower[] towers = new DefenseTower[3];
    private readonly int[] upgradeLevels = new int[3];
    private readonly int[] nextCosts = new int[3];

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
        if (index < 0 || index >= towers.Length)
            return 0;

        TowerConfig config = GetConfig(army);
        return HasTower(army) ? nextCosts[index] : config != null ? config.PurchaseCost : 0;
    }

    public float GetCurrentDamage(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length)
            return 0f;

        TowerConfig config = GetConfig(army);
        return HasTower(army) ? towers[index].Damage : config != null ? config.GetBaseDamage() : 0f;
    }

    public float GetUpgradeDamagePercent(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        return config != null ? config.UpgradeDamagePercent : 0f;
    }

    public string GetUpgradeDamageLabel(ArmyType army) =>
        "+" + (GetUpgradeDamagePercent(army) * 100f).ToString("0.##") + "%";

    public string GetTowerDisplayName(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        return config != null ? config.DisplayName : GetArmyName(army);
    }

    public bool CanUpgrade(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        return config != null && (!HasTower(army) || config.CanUpgrade(GetUpgradeLevel(army)));
    }

    public string GetButtonLabel(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        string name = GetTowerDisplayName(army);
        bool owned = HasTower(army);
        if (owned && !CanUpgrade(army))
            return "Torre de " + name + "\nNivel máximo\nDaño:  " + GetCurrentDamage(army).ToString("0.##");

        string damage = GetCurrentDamage(army).ToString("0.##") +
            (owned && config != null ? " " + GetUpgradeDamageLabel(army) : "");
        string action = owned ? CanUpgrade(army) ? "Mejorar torre de " : "Torre de " : "Comprar torre de ";
        return action + name +
               "\nDaño:  " + damage +
               "\nCoste: " + GetNextCost(army) + " " + GetCoinIcon(army);
    }

    public bool TryPurchase(ArmyType army) => TryPurchase(army, out _);

    public bool TryPurchase(ArmyType army, out string failureReason)
    {
        failureReason = null;
        int index = (int)army;
        if (index < 0 || index >= towers.Length)
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
        TowerConfig config = GetConfig(army);
        if (config == null || config.Army != army || config.Hero == null || config.Hero.Army != army)
        {
            failureReason = "La configuración de la torre no está asignada o no coincide con su ejército.";
            return false;
        }

        if (isUpgrade && !config.CanUpgrade(upgradeLevels[index]))
        {
            failureReason = "La torre ya alcanzó su nivel máximo.";
            return false;
        }

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
            game.ConfigureTower(behavior, config);
            towers[index] = behavior;
            upgradeLevels[index] = 0;
            nextCosts[index] = config.GetNextUpgradeCost(cost);
        }
        else
        {
            if (!economy.TrySpend(army, cost))
            {
                failureReason = "Saldo insuficiente: necesitas " + cost.ToString("0.##") + " monedas.";
                return false;
            }

            upgradeLevels[index]++;
            towers[index].SetUpgradeLevel(upgradeLevels[index]);
            nextCosts[index] = config.GetNextUpgradeCost(cost);
        }

        StateRevision++;
        return true;
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

    private TowerConfig GetConfig(ArmyType army) => army == ArmyType.Tierra ? groundConfig :
        army == ArmyType.Aire ? airConfig : seaConfig;

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
            ArmyType army = (ArmyType)i;
            TowerConfig config = GetConfig(army);
            nextCosts[i] = config != null ? config.PurchaseCost : 0;
        }
        StateRevision++;
    }

    private static string GetArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" :
        army == ArmyType.Aire ? "Aire" : "Mar";
}
