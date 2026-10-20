using UnityEngine;

/// <summary>Activates the scene-authored tower when its initial purchase succeeds.</summary>
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

    public int StateRevision { get; private set; }
    public bool CanPurchase => game != null && game.IsPurchasePhase;

    private void Awake()
    {
        for (int i = 0; i < towers.Length; i++)
            SetTowerVisible((ArmyType)i, false);
    }

    public TowerConfig GetConfig(ArmyType army) => army == ArmyType.Tierra ? groundConfig :
        army == ArmyType.Aire ? airConfig : army == ArmyType.Mar ? seaConfig : null;

    public bool HasTower(ArmyType army)
    {
        int index = (int)army;
        return index >= 0 && index < towers.Length && towers[index] != null;
    }

    public int GetNextCost(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length)
            return 0;

        TowerConfig config = GetConfig(army);
        return !HasTower(army) && config != null ? config.PurchaseCost : 0;
    }

    public float GetCurrentDamage(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length)
            return 0f;

        TowerConfig config = GetConfig(army);
        return HasTower(army) ? towers[index].Damage :
            config != null && game != null ? game.GetTowerDamage(army, config.GetBaseDamage()) :
            config != null ? config.GetBaseDamage() : 0f;
    }

    public float GetCurrentRange(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return 0f;
        TowerConfig config = GetConfig(army);
        return HasTower(army) ? towers[index].Range :
            config != null && config.Hero != null ? config.Hero.Range * (game != null ? game.GetTowerRangeMultiplier(army) : 1f) +
                (game != null ? game.GetTowerRangeFixedBonus(army) : 0f) : 0f;
    }

    public float GetCurrentCooldown(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return 0f;
        TowerConfig config = GetConfig(army);
        return HasTower(army) ? towers[index].Cooldown :
            config != null && config.Hero != null ? GetBaseTowerCooldown(config, game, army) : 0f;
    }

    public string GetTowerDisplayName(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        return config != null ? config.DisplayName : GetArmyName(army);
    }

    public bool CanPurchaseInitialTower(ArmyType army)
    {
        return GetConfig(army) != null && !HasTower(army);
    }

    public string GetButtonLabel(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        string name = GetTowerDisplayName(army);
        bool owned = HasTower(army);
        if (owned) return "Torre de " + name + "\nDaño:  " + GetCurrentDamage(army).ToString("0.##");
        string damage = GetCurrentDamage(army).ToString("0.##");
        string action = "Desbloquear torre de ";
        return action + name +
               "\nDaño:  " + damage +
               "\nCoste: " + GetNextCost(army) + " recursos";
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

        if (economy == null || game == null || !game.IsPurchasePhase)
        {
            failureReason = "Las torres solo se pueden comprar durante la fase de compras.";
            return false;
        }

        bool isUpgrade = HasTower(army);
        TowerConfig config = GetConfig(army);
        if (config == null || config.Army != army || config.Hero == null || config.Hero.Army != army)
        {
            failureReason = "La configuración de la torre no está asignada o no coincide con su ejército.";
            return false;
        }

        if (isUpgrade)
        {
            failureReason = "Las mejoras de torre se compran en la tienda entre oleadas.";
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
            game.ConfigureTower(behavior, config);
            behavior.enabled = true;
            towers[index] = behavior;
        }
        StateRevision++;
        GameSoundManager.Instance?.PlayTowerBuilt();
        return true;
    }

    public bool CanUnlockForIntermission(ArmyType army, out string failureReason)
    {
        failureReason = null;
        if (economy == null || game == null || !game.IsPurchasePhase)
        {
            failureReason = "Las torres solo se pueden comprar durante la fase de compras.";
            return false;
        }
        TowerConfig config = GetConfig(army);
        Transform anchor = GetAnchor(army);
        SpriteRenderer renderer = anchor != null ? anchor.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (HasTower(army)) { failureReason = "La torre ya está desbloqueada."; return false; }
        if (config == null || config.Army != army || config.Hero == null || config.Hero.Army != army ||
            anchor == null || renderer == null || renderer.sprite == null)
        {
            failureReason = "La configuración o el objeto de la torre no está listo para desbloquearse.";
            return false;
        }
        return true;
    }

    public bool UnlockForIntermission(ArmyType army, out string failureReason)
    {
        if (!CanUnlockForIntermission(army, out failureReason)) return false;
        int index = (int)army;
        TowerConfig config = GetConfig(army);
        Transform anchor = GetAnchor(army);
        DefenseTower behavior = anchor.GetComponent<DefenseTower>();
        if (behavior == null) behavior = anchor.gameObject.AddComponent<DefenseTower>();
        SetTowerVisible(army, true);
        game.ConfigureTower(behavior, config);
        behavior.enabled = true;
        towers[index] = behavior;
        StateRevision++;
        GameSoundManager.Instance?.PlayTowerBuilt();
        failureReason = null;
        return true;
    }

    private static float GetBaseTowerCooldown(TowerConfig config, TriunviratoGame owner, ArmyType army)
    {
        float rate = (config.Hero.AttackCooldown > 0f ? 1f / config.Hero.AttackCooldown : 0f) *
                     (owner != null ? owner.GetTowerAttackSpeedMultiplier(army) : 1f) +
                     (owner != null ? owner.GetTowerAttackRateFixedBonus(army) : 0f);
        return rate > 0f ? 1f / rate : config.Hero.AttackCooldown;
    }

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
            ArmyType army = (ArmyType)i;
            SetTowerVisible(army, false);
        }
        StateRevision++;
    }

    public void RefreshArmyIntermissionUpgrade(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return;
        if (towers[index] != null) towers[index].RefreshIntermissionDamage();
        StateRevision++;
    }

    private static string GetArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" :
        army == ArmyType.Aire ? "Aire" : "Mar";
}
