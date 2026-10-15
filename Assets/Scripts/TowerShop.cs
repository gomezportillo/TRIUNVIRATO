using UnityEngine;

/// <summary>Activates the scene-authored tower for each army and routes later purchases into upgrades.</summary>
public sealed class TowerShop : MonoBehaviour
{
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private Transform groundTower;
    [SerializeField] private Transform airTower;
    [SerializeField] private Transform seaTower;
    [SerializeField] private TowerConfig groundConfig;
    [SerializeField] private TowerConfig airConfig;
    [SerializeField] private TowerConfig seaConfig;

    private readonly DefenseTower[] towers = new DefenseTower[3];
    private readonly int[] upgradeLevels = new int[3];
    private readonly int[] armyUpgradeLevels = new int[3];
    private readonly int[] nextCosts = new int[3];

    public int StateRevision { get; private set; }
    public bool CanPurchase => game != null && game.CanCombat;

    private void Awake()
    {
        for (int i = 0; i < towers.Length; i++)
            SetTowerVisible((ArmyType)i, false);
    }

    public TowerConfig GetConfig(ArmyType army) => army == ArmyType.Tierra ? groundConfig :
        army == ArmyType.Aire ? airConfig : army == ArmyType.Mar ? seaConfig : null;

    public int GetArmyUpgradeLevel(ArmyType army)
    {
        int index = (int)army;
        return index >= 0 && index < armyUpgradeLevels.Length ? armyUpgradeLevels[index] : 0;
    }

    public void SetArmyUpgradeLevel(ArmyType army, int level)
    {
        int index = (int)army;
        if (index < 0 || index >= armyUpgradeLevels.Length)
            return;
        armyUpgradeLevels[index] = Mathf.Max(0, level);
        if (towers[index] != null)
            towers[index].SetArmyUpgradeLevel(armyUpgradeLevels[index]);
        StateRevision++;
    }

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
        return HasTower(army)
            ? towers[index].Damage
            : config != null ? config.GetDamageAtLevels(0, GetArmyUpgradeLevel(army)) : 0f;
    }

    public float GetNextDamage(ArmyType army)
    {
        int index = (int)army;
        TowerConfig config = GetConfig(army);
        if (index < 0 || index >= towers.Length || config == null)
            return 0f;
        if (!HasTower(army) || !config.CanUpgrade(upgradeLevels[index]))
            return GetCurrentDamage(army);
        return config.GetDamageAtLevels(upgradeLevels[index] + 1, armyUpgradeLevels[index]);
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
        string action = owned ? CanUpgrade(army) ? "Mejorar torre de " : "Torre de " : "Desbloquear torre de ";
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
            SpriteRenderer renderer = anchor != null ? anchor.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (anchor == null || renderer == null || renderer.sprite == null)
            {
                failureReason = "La torre de " + GetArmyName(army) +
                    " no tiene posición o SpriteRenderer con sprite asignado en la escena.";
                return false;
            }

            // The reusable component is serialized on each scene anchor. Recover gracefully if
            // Unity dropped that component during a scene/import change so purchases still work.
            DefenseTower behavior = anchor.GetComponent<DefenseTower>();
            if (behavior == null)
            {
                behavior = anchor.gameObject.AddComponent<DefenseTower>();
                Debug.LogWarning("Faltaba DefenseTower en " + anchor.name + "; se añadió al comprar.", anchor);
            }

            if (!economy.TrySpend(army, cost))
            {
                failureReason = "Saldo insuficiente: necesitas " + cost.ToString("0.##") + " monedas.";
                return false;
            }

            SetTowerVisible(army, true);
            game.ConfigureTower(behavior, config, upgradeLevels[index], armyUpgradeLevels[index]);
            behavior.enabled = true;
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

    private void SetTowerVisible(ArmyType army, bool visible)
    {
        Transform anchor = GetAnchor(army);
        if (anchor == null)
            return;

        foreach (SpriteRenderer renderer in anchor.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.enabled = visible;
        DefenseTower behavior = anchor.GetComponent<DefenseTower>();
        if (behavior != null)
            behavior.enabled = visible;
    }

    private Transform GetAnchor(ArmyType army) => army == ArmyType.Tierra ? groundTower :
        army == ArmyType.Aire ? airTower : seaTower;

    public void ResetPurchasedTowers()
    {
        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] != null)
                towers[i].enabled = false;
            towers[i] = null;
            upgradeLevels[i] = 0;
            armyUpgradeLevels[i] = 0;
            ArmyType army = (ArmyType)i;
            SetTowerVisible(army, false);
            TowerConfig config = GetConfig(army);
            nextCosts[i] = config != null ? config.PurchaseCost : 0;
        }
        StateRevision++;
    }

    private static string GetArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" :
        army == ArmyType.Aire ? "Aire" : "Mar";
}
