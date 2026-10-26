using UnityEngine;

/// <summary>Deploys the scene-authored towers and exposes their combat upgrades.</summary>
public sealed class TowerManager : MonoBehaviour
{
    [SerializeField] private TriunviratoGame game;
    [SerializeField] private Transform groundTower;
    [SerializeField] private Transform airTower;
    [SerializeField] private Transform seaTower;
    [SerializeField] private TowerConfig groundConfig;
    [SerializeField] private TowerConfig airConfig;
    [SerializeField] private TowerConfig seaConfig;

    private readonly DefenseTower[] towers = new DefenseTower[3];

    public int StateRevision { get; private set; }

    private void Awake() => ResetTowers();

    public TowerConfig GetConfig(ArmyType army) => army == ArmyType.Tierra ? groundConfig :
        army == ArmyType.Aire ? airConfig : army == ArmyType.Mar ? seaConfig : null;

    public bool HasTower(ArmyType army)
    {
        int index = (int)army;
        return index >= 0 && index < towers.Length && towers[index] != null;
    }

    public float GetCurrentDamage(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length)
            return 0f;

        return HasTower(army) ? towers[index].Damage : 0f;
    }

    public float GetCurrentRange(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return 0f;
        return HasTower(army) ? towers[index].Range : 0f;
    }

    public float GetCurrentCooldown(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return 0f;
        return HasTower(army) ? towers[index].Cooldown : 0f;
    }

    public string GetTowerDisplayName(ArmyType army)
    {
        TowerConfig config = GetConfig(army);
        return config != null ? config.DisplayName : GetArmyName(army);
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

    public bool UnlockTower(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length || HasTower(army))
            return false;
        TowerConfig config = GetConfig(army);
        Transform anchor = GetAnchor(army);
        if (game == null || config == null || config.Army != army || config.Hero == null || config.Hero.Army != army || anchor == null)
        {
            Debug.LogError("La torre de " + army + " no tiene una configuración válida.", this);
            return false;
        }
        DefenseTower behavior = anchor.GetComponent<DefenseTower>();
        if (behavior == null) behavior = anchor.gameObject.AddComponent<DefenseTower>();
        game.ConfigureTower(behavior, config);
        SetTowerVisible(army, true);
        behavior.enabled = true;
        towers[index] = behavior;
        StateRevision++;
        return true;
    }

    public void ResetTowers()
    {
        for (int i = 0; i < towers.Length; i++)
        {
            SetTowerVisible((ArmyType)i, false);
            towers[i] = null;
        }
        StateRevision++;
    }
    public void RefreshArmyUpgrade(ArmyType army)
    {
        int index = (int)army;
        if (index < 0 || index >= towers.Length) return;
        if (towers[index] != null) towers[index].RefreshUpgradeDamage();
        StateRevision++;
    }

    private static string GetArmyName(ArmyType army) => army == ArmyType.Tierra ? "Tierra" :
        army == ArmyType.Aire ? "Aire" : "Mar";
}
