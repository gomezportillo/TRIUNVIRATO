using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the first playable TRIUNVIRATO prototype using scene-authored objects.</summary>
public sealed class TriunviratoGame : MonoBehaviour
{
    private const string NormalAttackState = "Attack_Normal";
    public enum MatchStatus { WaitingForPlayers, InProgress, Finished }
    private const float CastleStartHealth = 100f;
    public const float PurchasePhaseDuration = 20f;
    public const int ResourceChoiceReward = 20;
    private const int NormalStars = 1;
    private const int RareStars = 2;
    [SerializeField] private Transform tierraHero;
    [SerializeField] private Transform aireHero;
    [SerializeField] private Transform marHero;
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private HeroConfig tierraConfig;
    [SerializeField] private HeroConfig aireConfig;
    [SerializeField] private HeroConfig marConfig;
    [SerializeField] private Transform enemySpawner;
    [SerializeField] private Transform castleTarget;
    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private Font damageFont;
    [SerializeField] private IntermissionUpgradeCatalog intermissionCatalog;
    private readonly List<SkeletonEnemy> enemies = new List<SkeletonEnemy>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<FloatingDamage> floatingDamage = new List<FloatingDamage>();
    private Defender[] defenders;
    private Sprite projectileSprite;
    private float castleHealth;
    private float spawnTimer;
    private float intermissionTimer;
    private int wave;
    private int lastRewardedWave;
    private int lastEarlyCallWave = -2;
    private int lastPurchaseTimerSecond = -1;
    private int remainingToSpawn;
    private float currentWaveRewardMultiplier = 1f;
    private bool waveInProgress;
    private bool earlyWaveCallPending;
    private bool paused;
    private bool finished;
    private bool victory;
    private bool purchasePhase;
    private float purchasePhaseTimer;
    private readonly bool[] purchaseChoices = new bool[3];
    private readonly string[] purchaseResults = new string[3];
    private readonly float[] economyUpgradeMultipliers = { 1f, 1f, 1f };
    private readonly float[] towerUpgradeMultipliers = { 1f, 1f, 1f };
    private readonly float[] heroUpgradeMultipliers = { 1f, 1f, 1f };
    private readonly float[] economyKillRewardMultipliers = { 1f, 1f, 1f };
    private readonly float[] economyKillRewardFixedBonus = new float[3];
    private readonly float[] economyPassiveFixedBonus = new float[3];
    private readonly float[] economyWaveRewardFixedBonus = new float[3];
    private readonly float[] economyWaveRewardMultipliers = { 1f, 1f, 1f };
    private readonly float[] towerSpeedMultipliers = { 1f, 1f, 1f };
    private readonly float[] towerAttackRateFixedBonus = new float[3];
    private readonly float[] towerRangeMultipliers = { 1f, 1f, 1f };
    private readonly float[] towerRangeFixedBonus = new float[3];
    private readonly float[] towerDamageFixedBonus = new float[3];
    private readonly float[] heroSpeedMultipliers = { 1f, 1f, 1f };
    private readonly float[] heroAttackRateFixedBonus = new float[3];
    private readonly float[] heroRangeMultipliers = { 1f, 1f, 1f };
    private readonly float[] heroRangeFixedBonus = new float[3];
    private readonly float[] heroSplashMultipliers = { 1f, 1f, 1f };
    private readonly float[] heroSplashFixedBonus = new float[3];
    private readonly float[] heroDamageFixedBonus = new float[3];
    private readonly PurchaseOffer[,] purchaseOffers = new PurchaseOffer[3, 3];
    private bool waveHadBoss;
    private bool purchaseOffersAreRare;
    private MatchStatus matchStatus = MatchStatus.WaitingForPlayers;

    [Serializable]
    public sealed class PurchaseOffer
    {
        public string id;
        public string category;
        public string name;
        public string description;
        public int stars;
        public int[] resourceCosts;
        public bool isTowerUnlock;
    }

    public MatchStatus Status => matchStatus;
    public float CastleHealth => Mathf.Max(0f, castleHealth);
    public float CastleMaximumHealth => CastleStartHealth;
    public int StateRevision { get; private set; }
    public bool IsMatchInProgress => matchStatus == MatchStatus.InProgress;
    public int CurrentWave => wave;
    public float CurrentWaveRewardMultiplier => currentWaveRewardMultiplier;
    public bool IsWaveInProgress => waveInProgress;
    public bool IsPurchasePhase => purchasePhase;
    public float PurchasePhaseSecondsRemaining => Mathf.Max(0f, purchasePhaseTimer);
    public bool IsPurchaseChoiceMade(ArmyType army) => IsValidArmy(army) && purchaseChoices[(int)army];
    public string GetPurchaseResult(ArmyType army) => IsValidArmy(army) ? purchaseResults[(int)army] : string.Empty;
    public PurchaseOffer[] GetPurchaseOffers(ArmyType army)
    {
        int index = (int)army;
        if (!IsValidArmy(army) || !purchasePhase) return Array.Empty<PurchaseOffer>();
        return new[] { purchaseOffers[index, 0], purchaseOffers[index, 1], purchaseOffers[index, 2] };
    }
    public bool CanCombatNow => CanCombat;
    public bool IsPaused => paused;
    public int EnemiesRemainingInWave => enemies.Count + remainingToSpawn;
    public bool IsEarlyWaveCallPending => earlyWaveCallPending;
    public int NextEarlyCallAvailableWave => lastEarlyCallWave + 2;
    public bool CanCallNextWaveEarly => CanCombat && !earlyWaveCallPending && wave >= lastEarlyCallWave + 2;

    private sealed class Defender
    {
        public GameObject View;
        public HeroConfig Config;
        public Animator Animator;
        public Transform AttackSpawn;
        public float AttackTimer;
        public bool ShotPending;
        public float PendingShotTimer;
        public SkeletonEnemy PendingTarget;
        public Vector3 PendingTargetPosition;
    }

    private sealed class Projectile
    {
        public GameObject View;
        public Vector3 Destination;
        public SkeletonEnemy Target;
        public float Damage;
        public Color DamageColor;
        public float SplashRadius;
        public float Speed;
        public float RemainingLifetime;
    }

    private sealed class FloatingDamage
    {
        public GameObject View;
        public float RemainingLifetime;
    }

    private void Start()
    {
        if (intermissionCatalog == null)
            Debug.LogError("Asigna IntermissionUpgrades desde Assets/GameData/Upgrades en el Inspector.", this);
        if (tierraHero == null || aireHero == null || marHero == null ||
            tierraConfig == null || aireConfig == null || marConfig == null ||
            enemySpawner == null || castleTarget == null || enemyConfig == null ||
            enemyConfig.Prefab == null || damageFont == null)
        {
            Debug.LogError("Asigna los tres héroes y sus configuraciones, EnemySpawner, CastleTarget, EnemyConfig y la fuente en el Inspector.", this);
            enabled = false;
            return;
        }
        damageFont.RequestCharactersInTexture("-0123456789", 48, FontStyle.Normal);
        if (economy == null)
            economy = FindAnyObjectByType<ArmyEconomy>();
        ResetMatch(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetMatch(matchStatus != MatchStatus.WaitingForPlayers);
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame && !finished)
            {
                paused = !paused;
                StateRevision++;
            }
        }

        if (matchStatus != MatchStatus.InProgress || paused || finished)
            return;

        UpdateWaveFlow();
        if (finished || purchasePhase)
            return;
        UpdateEnemies();
        if (finished)
            return;
        UpdateDefenders();
        UpdateProjectiles();
        UpdateFloatingDamage();
        RemoveFinishedEnemyDeaths();

        if (castleHealth <= 0f)
            EndMatch(false);
    }

    private void ResetMatch(bool resumeMatch)
    {
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        enemies.Clear();
        foreach (Projectile projectile in projectiles)
        {
            if (projectile.View != null)
                Destroy(projectile.View);
        }
        projectiles.Clear();
        foreach (FloatingDamage damage in floatingDamage)
        {
            if (damage.View != null)
                Destroy(damage.View);
        }
        floatingDamage.Clear();
        castleHealth = CastleStartHealth;
        spawnTimer = 1.5f;
        intermissionTimer = 0f;
        wave = 0;
        lastRewardedWave = 0;
        lastEarlyCallWave = -2;
        remainingToSpawn = 0;
        currentWaveRewardMultiplier = 1f;
        waveInProgress = false;
        earlyWaveCallPending = false;
        paused = false;
        finished = false;
        victory = false;
        matchStatus = resumeMatch ? MatchStatus.InProgress : MatchStatus.WaitingForPlayers;
        StateRevision++;
        if (resumeMatch)
            GameSoundManager.Instance?.PlayMatchStart();
        purchasePhase = false;
        purchasePhaseTimer = 0f;
        Array.Clear(purchaseChoices, 0, purchaseChoices.Length);
        Array.Clear(purchaseResults, 0, purchaseResults.Length);
        Array.Clear(economyKillRewardFixedBonus, 0, economyKillRewardFixedBonus.Length);
        Array.Clear(economyPassiveFixedBonus, 0, economyPassiveFixedBonus.Length);
        Array.Clear(economyWaveRewardFixedBonus, 0, economyWaveRewardFixedBonus.Length);
        Array.Clear(towerDamageFixedBonus, 0, towerDamageFixedBonus.Length);
        Array.Clear(towerAttackRateFixedBonus, 0, towerAttackRateFixedBonus.Length);
        Array.Clear(towerRangeFixedBonus, 0, towerRangeFixedBonus.Length);
        Array.Clear(heroDamageFixedBonus, 0, heroDamageFixedBonus.Length);
        Array.Clear(heroAttackRateFixedBonus, 0, heroAttackRateFixedBonus.Length);
        Array.Clear(heroRangeFixedBonus, 0, heroRangeFixedBonus.Length);
        Array.Clear(heroSplashFixedBonus, 0, heroSplashFixedBonus.Length);
        for (int i = 0; i < 3; i++)
        {
            economyUpgradeMultipliers[i] = towerUpgradeMultipliers[i] = heroUpgradeMultipliers[i] = 1f;
            economyKillRewardMultipliers[i] = 1f;
            economyWaveRewardMultipliers[i] = 1f;
            towerSpeedMultipliers[i] = towerRangeMultipliers[i] = 1f;
            heroSpeedMultipliers[i] = heroRangeMultipliers[i] = heroSplashMultipliers[i] = 1f;
            for (int category = 0; category < 3; category++) purchaseOffers[i, category] = null;
        }
        waveHadBoss = false;
        if (economy != null)
            economy.ResetEconomy();
        TowerShop towerShop = FindAnyObjectByType<TowerShop>();
        if (towerShop != null)
            towerShop.ResetPurchasedTowers();
        if (defenders == null)
        {
            defenders = new[]
            {
                CreateDefender(tierraHero, tierraConfig, ArmyType.Tierra),
                CreateDefender(aireHero, aireConfig, ArmyType.Aire),
                CreateDefender(marHero, marConfig, ArmyType.Mar)
            };
        }
        foreach (Defender defender in defenders)
        {
            if (defender != null)
            {
                defender.AttackTimer = 0f;
                defender.ShotPending = false;
                defender.PendingTarget = null;
                defender.PendingTargetPosition = Vector3.zero;
                defender.PendingShotTimer = 0f;
            }
        }
    }

    private Defender CreateDefender(Transform hero, HeroConfig config, ArmyType expectedArmy)
    {
        if (hero == null || config == null || config.Army != expectedArmy)
        {
            Debug.LogError("Falta el héroe de " + expectedArmy + " o su HeroConfig no coincide con el ejército.", this);
            return null;
        }
        Animator animator = hero.GetComponent<Animator>();

        return new Defender
        {
            View = hero.gameObject,
            Config = config,
            Animator = animator,
            AttackSpawn = hero.Find("AttackSpawn")
        };
    }

    private void UpdateWaveFlow()
    {
        if (waveInProgress)
        {
            if (remainingToSpawn > 0)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnEnemy(wave % 3 == 0 && remainingToSpawn == 1);
                    remainingToSpawn--;
                    spawnTimer = 1f;
                }
            }
            else if (enemies.Count == 0)
            {
                waveInProgress = false;
                if (wave > 0 && lastRewardedWave != wave)
                {
                    if (economy != null)
                        economy.RewardWaveCompleted(currentWaveRewardMultiplier);
                    lastRewardedWave = wave;
                }
                BeginPurchasePhase();
                currentWaveRewardMultiplier = 1f;
            }
        }
        else if (purchasePhase)
        {
            purchasePhaseTimer -= Time.deltaTime;
            int timerSecond = Mathf.CeilToInt(Mathf.Max(0f, purchasePhaseTimer));
            if (timerSecond != lastPurchaseTimerSecond)
            {
                lastPurchaseTimerSecond = timerSecond;
                StateRevision++;
            }
            if (purchasePhaseTimer <= 0f)
            {
                for (int i = 0; i < purchaseChoices.Length; i++)
                    if (!purchaseChoices[i]) GrantResourceChoice((ArmyType)i);
                FinishPurchasePhase();
            }
        }
        else if (earlyWaveCallPending)
        {
            StartNextWave();
        }
        else
        {
            intermissionTimer -= Time.deltaTime;
            if (intermissionTimer <= 0f)
                StartNextWave();
        }
    }

    private void BeginPurchasePhase()
    {
        if (purchasePhase) return;
        purchasePhase = true;
        purchasePhaseTimer = PurchasePhaseDuration;
        lastPurchaseTimerSecond = Mathf.CeilToInt(PurchasePhaseDuration);
        Array.Clear(purchaseChoices, 0, purchaseChoices.Length);
        Array.Clear(purchaseResults, 0, purchaseResults.Length);
        purchaseOffersAreRare = waveHadBoss;
        GeneratePurchaseOffers();
        StateRevision++;
    }

    private void GeneratePurchaseOffers()
    {
        if (intermissionCatalog == null) return;
        TowerShop towerShop = FindAnyObjectByType<TowerShop>();
        for (int armyIndex = 0; armyIndex < 3; armyIndex++)
        {
            ArmyType army = (ArmyType)armyIndex;
            purchaseOffers[armyIndex, 0] = GenerateOffer(army, IntermissionUpgradeCategory.Economy, towerShop);
            purchaseOffers[armyIndex, 1] = towerShop != null && !towerShop.HasTower(army)
                ? CreateTowerUnlockOffer(army, towerShop)
                : towerShop != null ? GenerateOffer(army, IntermissionUpgradeCategory.Towers, towerShop) : null;
            purchaseOffers[armyIndex, 2] = GenerateOffer(army, IntermissionUpgradeCategory.Hero, towerShop);
        }
    }

    private PurchaseOffer GenerateOffer(ArmyType army, IntermissionUpgradeCategory category, TowerShop towerShop)
    {
        List<IntermissionUpgradeCatalog.Definition> valid = new List<IntermissionUpgradeCatalog.Definition>();
        int stars = purchaseOffersAreRare ? RareStars : NormalStars;
        foreach (IntermissionUpgradeCatalog.Definition definition in intermissionCatalog.Definitions)
        {
            if (definition == null || definition.category != category || !IsOfferStillApplicable(army, definition)) continue;
            valid.Add(definition);
        }
        if (valid.Count == 0) return null;
        IntermissionUpgradeCatalog.Definition chosen = valid[UnityEngine.Random.Range(0, valid.Count)];
        int baseCost = chosen.baseCost;
        return MakeOffer(chosen.id, category, chosen.displayName,
            IntermissionUpgradeCatalog.FormatDescription(chosen, stars), baseCost, false);
    }

    private PurchaseOffer CreateTowerUnlockOffer(ArmyType army, TowerShop towerShop)
    {
        TowerConfig config = towerShop != null ? towerShop.GetConfig(army) : null;
        if (config == null || config.Army != army || config.Hero == null) return null;
        int normalTowerUpgradeCost = int.MaxValue;
        foreach (IntermissionUpgradeCatalog.Definition definition in intermissionCatalog.Definitions)
            if (definition != null && definition.category == IntermissionUpgradeCategory.Towers)
                normalTowerUpgradeCost = Mathf.Min(normalTowerUpgradeCost, definition.baseCost);
        if (config.PurchaseCost <= normalTowerUpgradeCost) return null;
        string name = "Desbloquear torre de " + config.DisplayName;
        string description = "Compra y activa esta torre para defender tu ejército.";
        return MakeOffer("tower_unlock_" + army.ToString().ToLowerInvariant(),
            IntermissionUpgradeCategory.Towers, name, description, config.PurchaseCost, true);
    }

    private PurchaseOffer MakeOffer(string id, IntermissionUpgradeCategory category, string name,
        string description, int baseCost, bool isTowerUnlock)
    {
        int primary = (int)category;
        int scaled = Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(1f + intermissionCatalog.PriceGrowthPerWave,
            Mathf.Max(0, wave - 1))));
        int[] costs = new int[3];
        costs[primary] = scaled;
        if (purchaseOffersAreRare)
        {
            int secondary = UnityEngine.Random.Range(0, 2);
            if (secondary >= primary) secondary++;
            costs[secondary] = Mathf.Max(1, Mathf.RoundToInt(scaled * intermissionCatalog.RareSecondaryCostPercent));
        }
        return new PurchaseOffer { id = id, category = category.ToString().ToLowerInvariant(), name = name,
            description = description, stars = purchaseOffersAreRare ? RareStars : NormalStars,
            resourceCosts = costs, isTowerUnlock = isTowerUnlock };
    }

    private void FinishPurchasePhase()
    {
        if (!purchasePhase) return;
        purchasePhase = false;
        purchasePhaseTimer = 0f;
        StateRevision++;
        if (earlyWaveCallPending) StartNextWave();
        else intermissionTimer = 0f;
    }

    public bool TryChooseIntermissionOption(ArmyType army, string id, out string message)
    {
        message = "La fase de compras no está activa.";
        if (!purchasePhase || purchasePhaseTimer <= 0f || !IsValidArmy(army)) return false;
        int armyIndex = (int)army;
        if (purchaseChoices[armyIndex]) { message = "Ya has elegido en esta fase."; return false; }
        if (economy == null) { message = "La economía no está disponible."; return false; }
        if (id == "resources")
        {
            GrantResourceChoice(army);
            message = purchaseResults[armyIndex];
            return true;
        }
        PurchaseOffer offer = FindGeneratedOffer(army, id);
        if (offer == null) { message = "Oferta desconocida para esta fase."; return false; }
        TowerShop towerShop = FindAnyObjectByType<TowerShop>();
        if (offer.isTowerUnlock && towerShop == null)
        {
            message = "La tienda de torres no está disponible.";
            return false;
        }
        if (offer.isTowerUnlock && towerShop.HasTower(army))
        {
            message = "La torre ya está desbloqueada.";
            return false;
        }
        if (offer.isTowerUnlock && !towerShop.CanUnlockForIntermission(army, out message))
        {
            return false;
        }
        IntermissionUpgradeCatalog.Definition definition = offer.isTowerUnlock ? null : intermissionCatalog.Find(id);
        if (!offer.isTowerUnlock && (definition == null || !IsOfferStillApplicable(army, definition)))
        {
            message = "La mejora ya no está disponible.";
            return false;
        }
        int[] costs = offer.resourceCosts;
        if (!economy.TrySpendIntermissionResources(costs[0], costs[1], costs[2]))
        {
            message = BuildInsufficientPurchaseResources(costs);
            return false;
        }
        if (offer.isTowerUnlock)
        {
            if (!towerShop.UnlockForIntermission(army, out message)) return false;
        }
        else
        {
            ApplyIntermissionEffect(army, definition, IntermissionUpgradeCatalog.GetOfferEffect(definition, offer.stars));
        }
        purchaseChoices[armyIndex] = true;
        purchaseResults[armyIndex] = offer.name + (offer.stars == RareStars ? " ⭐⭐" : " ⭐");
        StateRevision++;
        message = "Comprada: " + offer.name + ".";
        FinishIfAllChosen();
        return true;
    }

    private PurchaseOffer FindGeneratedOffer(ArmyType army, string id)
    {
        int index = (int)army;
        if (!IsValidArmy(army) || string.IsNullOrEmpty(id)) return null;
        for (int slot = 0; slot < 3; slot++)
            if (purchaseOffers[index, slot] != null && purchaseOffers[index, slot].id == id) return purchaseOffers[index, slot];
        return null;
    }

    private void ApplyIntermissionEffect(ArmyType army, IntermissionUpgradeCatalog.Definition definition, float effect)
    {
        int index = (int)army;
        bool percentage = definition.valueType == IntermissionUpgradeValueType.Percentage;
        float multiplier = percentage ? 1f + effect / 100f : 1f;
        switch (definition.effect)
        {
            case IntermissionUpgradeEffect.PassiveIncome: if (percentage) economyUpgradeMultipliers[index] *= multiplier; else economyPassiveFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.KillReward: if (percentage) economyKillRewardMultipliers[index] *= multiplier; else economyKillRewardFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.WaveReward: if (percentage) economyWaveRewardMultipliers[index] *= multiplier; else economyWaveRewardFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.TowerDamage: if (percentage) towerUpgradeMultipliers[index] *= multiplier; else towerDamageFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.TowerAttackSpeed: if (percentage) towerSpeedMultipliers[index] *= multiplier; else towerAttackRateFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.TowerRange: if (percentage) towerRangeMultipliers[index] *= multiplier; else towerRangeFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.HeroDamage: if (percentage) heroUpgradeMultipliers[index] *= multiplier; else heroDamageFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.HeroAttackSpeed: if (percentage) heroSpeedMultipliers[index] *= multiplier; else heroAttackRateFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.HeroRange: if (percentage) heroRangeMultipliers[index] *= multiplier; else heroRangeFixedBonus[index] += effect; break;
            case IntermissionUpgradeEffect.HeroSplash: if (percentage) heroSplashMultipliers[index] *= multiplier; else heroSplashFixedBonus[index] += effect; break;
        }
        if (definition.category == IntermissionUpgradeCategory.Towers)
            FindAnyObjectByType<TowerShop>()?.RefreshArmyIntermissionUpgrade(army);
    }

    private bool IsOfferStillApplicable(ArmyType army, IntermissionUpgradeCatalog.Definition definition)
    {
        if (definition.category == IntermissionUpgradeCategory.Towers)
        {
            TowerShop shop = FindAnyObjectByType<TowerShop>();
            TowerConfig tower = shop != null ? shop.GetConfig(army) : null;
            if (shop == null || !shop.HasTower(army) || tower == null || tower.Hero == null)
                return false;
            if (definition.effect == IntermissionUpgradeEffect.TowerRange) return tower.Hero.Range > 0f;
            if (definition.effect == IntermissionUpgradeEffect.TowerAttackSpeed) return tower.Hero.AttackCooldown > 0f;
            return true;
        }
        HeroConfig hero = GetHeroConfig(army);
        if (hero == null) return false;
        if (definition.effect == IntermissionUpgradeEffect.HeroSplash) return hero.SplashRadius > 0f;
        if (definition.effect == IntermissionUpgradeEffect.HeroRange) return hero.Range > 0f;
        if (definition.effect == IntermissionUpgradeEffect.HeroAttackSpeed) return hero.AttackCooldown > 0f;
        return true;
    }

    public float GetIntermissionKillRewardMultiplier(ArmyType army) => IsValidArmy(army) ? economyKillRewardMultipliers[(int)army] : 1f;
    public float GetIntermissionKillRewardFixedBonus(ArmyType army) => IsValidArmy(army) ? economyKillRewardFixedBonus[(int)army] : 0f;
    public float GetIntermissionWaveRewardMultiplier(ArmyType army) => IsValidArmy(army) ? economyWaveRewardMultipliers[(int)army] : 1f;
    public float GetTowerAttackSpeedMultiplier(ArmyType army) => IsValidArmy(army) ? towerSpeedMultipliers[(int)army] : 1f;
    public float GetTowerRangeMultiplier(ArmyType army) => IsValidArmy(army) ? towerRangeMultipliers[(int)army] : 1f;
    public float GetTowerAttackRateFixedBonus(ArmyType army) => IsValidArmy(army) ? towerAttackRateFixedBonus[(int)army] : 0f;
    public float GetTowerRangeFixedBonus(ArmyType army) => IsValidArmy(army) ? towerRangeFixedBonus[(int)army] : 0f;
    public float GetHeroAttackSpeedMultiplier(ArmyType army) => IsValidArmy(army) ? heroSpeedMultipliers[(int)army] : 1f;
    public float GetHeroRangeMultiplier(ArmyType army) => IsValidArmy(army) ? heroRangeMultipliers[(int)army] : 1f;
    public float GetHeroSplashMultiplier(ArmyType army) => IsValidArmy(army) ? heroSplashMultipliers[(int)army] : 1f;
    public float GetHeroAttackRateFixedBonus(ArmyType army) => IsValidArmy(army) ? heroAttackRateFixedBonus[(int)army] : 0f;
    public float GetHeroRangeFixedBonus(ArmyType army) => IsValidArmy(army) ? heroRangeFixedBonus[(int)army] : 0f;
    public float GetHeroSplashFixedBonus(ArmyType army) => IsValidArmy(army) ? heroSplashFixedBonus[(int)army] : 0f;
    public float GetTowerDamage(ArmyType army, float baseDamage) => baseDamage * GetTowerIntermissionMultiplier(army) + GetTowerDamageFixedBonus(army);
    public float GetTowerDamageFixedBonus(ArmyType army) => IsValidArmy(army) ? towerDamageFixedBonus[(int)army] : 0f;
    public float GetHeroDamageFixedBonus(ArmyType army) => IsValidArmy(army) ? heroDamageFixedBonus[(int)army] : 0f;
    public float GetEconomyPassiveFixedBonus(ArmyType army) => IsValidArmy(army) ? economyPassiveFixedBonus[(int)army] : 0f;
    public float GetEconomyWaveRewardFixedBonus(ArmyType army) => IsValidArmy(army) ? economyWaveRewardFixedBonus[(int)army] : 0f;

    private string BuildInsufficientPurchaseResources(int[] costs)
    {
        string[] armyNames = { "Tierra", "Aire", "Mar" };
        List<string> missing = new List<string>();
        for (int i = 0; i < costs.Length; i++)
        {
            int balance = economy != null ? economy.GetCoins((ArmyType)i) : 0;
            if (balance < costs[i]) missing.Add((costs[i] - balance) + " recursos de " + armyNames[i] + " más");
        }
        return "Recursos insuficientes: necesitas " + string.Join(" y ", missing) + ".";
    }

    private void GrantResourceChoice(ArmyType army)
    {
        if (!purchasePhase || !IsValidArmy(army) || purchaseChoices[(int)army]) return;
        economy?.GrantIntermissionResources(army, ResourceChoiceReward);
        purchaseChoices[(int)army] = true;
        purchaseResults[(int)army] = "+" + ResourceChoiceReward + " recursos";
        StateRevision++;
        FinishIfAllChosen();
    }

    private void FinishIfAllChosen()
    {
        foreach (bool chosen in purchaseChoices) if (!chosen) return;
        FinishPurchasePhase();
    }

    public float GetEconomyIntermissionMultiplier(ArmyType army) => IsValidArmy(army) ? economyUpgradeMultipliers[(int)army] : 1f;
    public float GetTowerIntermissionMultiplier(ArmyType army) => IsValidArmy(army) ? towerUpgradeMultipliers[(int)army] : 1f;
    public float GetHeroIntermissionMultiplier(ArmyType army) => IsValidArmy(army) ? heroUpgradeMultipliers[(int)army] : 1f;

    private void StartNextWave()
    {
        wave++;
        waveHadBoss = false;
        remainingToSpawn = 4 + wave * 2;
        waveInProgress = true;
        spawnTimer = 0f;
        if (earlyWaveCallPending)
        {
            currentWaveRewardMultiplier = 1.5f;
            earlyWaveCallPending = false;
            Debug.Log("Mar ha llamado antes a la oleada " + wave + ". Su recompensa de oleada aumenta un 50 %.", this);
        }
        StateRevision++;
    }

    public bool TryCallNextWaveEarly(out string message)
    {
        if (!CanCallNextWaveEarly)
        {
            message = earlyWaveCallPending
                ? "Ya hay una llamada anticipada pendiente."
                : "Mar todavía no puede adelantar otra oleada.";
            return false;
        }

        lastEarlyCallWave = wave;
        earlyWaveCallPending = true;
        StateRevision++;
        message = waveInProgress
            ? "Llamada aceptada: la siguiente oleada empezará en cuanto se despeje la actual."
            : "Llamada aceptada: empieza la siguiente oleada.";
        Debug.Log("Mar solicita adelantar la siguiente oleada desde la oleada " + wave + ".", this);
        return true;
    }

    public bool TryStartMatch()
    {
        if (matchStatus != MatchStatus.WaitingForPlayers)
            return false;
        matchStatus = MatchStatus.InProgress;
        intermissionTimer = 1.5f;
        paused = false;
        finished = false;
        StateRevision++;
        Debug.Log("Partida iniciada.", this);
        GameSoundManager.Instance?.PlayMatchStart();
        return true;
    }

    public HeroConfig GetHeroConfig(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Tierra: return tierraConfig;
            case ArmyType.Aire: return aireConfig;
            case ArmyType.Mar: return marConfig;
            default: return null;
        }
    }

    public float GetHeroDamage(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        return config != null
            ? config.Damage * GetHeroIntermissionMultiplier(army) + GetHeroDamageFixedBonus(army)
            : 0f;
    }

    public float GetHeroAttackCooldown(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        float attacksPerSecond = (config.AttackCooldown > 0f ? 1f / config.AttackCooldown : 0f) * GetHeroAttackSpeedMultiplier(army) + GetHeroAttackRateFixedBonus(army);
        return attacksPerSecond > 0f ? 1f / attacksPerSecond : config.AttackCooldown;
    }

    public float GetHeroRange(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        return config.Range * GetHeroRangeMultiplier(army) + GetHeroRangeFixedBonus(army);
    }

    public float GetHeroSplashRadius(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        return config.SplashRadius * GetHeroSplashMultiplier(army) + GetHeroSplashFixedBonus(army);
    }

    private void SpawnEnemy(bool isBoss)
    {
        if (isBoss) waveHadBoss = true;
        float randomY = UnityEngine.Random.Range(-0.25f, 0.25f);
        Vector3 spawnPosition = enemySpawner.position + Vector3.up * randomY;

        SkeletonEnemy enemy = Instantiate(
            enemyConfig.Prefab,
            spawnPosition,
            Quaternion.identity
        );

        enemy.name = isBoss ? "Skeleton Boss" : "Skeleton";
        enemy.transform.localScale = Vector3.one * (isBoss ? enemyConfig.BossScale : enemyConfig.NormalScale);
        enemy.Initialize(enemyConfig, this, wave, isBoss);
        enemies.Add(enemy);
    }

    private void UpdateEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            SkeletonEnemy enemy = enemies[i];
            if (enemy == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.Tick(Time.deltaTime, castleTarget.position))
            {
                Destroy(enemy.gameObject);
                enemies.RemoveAt(i);
            }
        }
    }

    private void UpdateDefenders()
    {
        foreach (Defender defender in defenders)
        {
            if (defender == null || defender.View == null)
                continue;

            if (defender.ShotPending)
            {
                defender.PendingShotTimer -= Time.deltaTime;
                if (defender.PendingShotTimer <= 0f)
                {
                    SkeletonEnemy pendingTarget = defender.PendingTarget;
                    Vector3 pendingTargetPosition = defender.PendingTargetPosition;
                    defender.ShotPending = false;
                    defender.PendingTarget = null;
                    defender.PendingTargetPosition = Vector3.zero;
                    if (IsValidPendingHeroShot(defender))
                    {
                        if (pendingTarget != null && !pendingTarget.IsDead)
                            FireProjectile(defender, pendingTarget);
                        else
                            FireProjectile(defender, pendingTargetPosition);
                    }
                }
                continue;
            }

            defender.AttackTimer -= Time.deltaTime;
            if (defender.AttackTimer > 0f)
                continue;

            SkeletonEnemy target = FindTarget(defender);
            if (target == null)
                continue;

            defender.AttackTimer = GetHeroAttackCooldown(defender.Config.Army);
            if (defender.Animator != null && defender.Animator.runtimeAnimatorController != null)
                defender.Animator.Play(NormalAttackState, 0, 0f);
            defender.ShotPending = true;
            defender.PendingTarget = target;
            defender.PendingTargetPosition = target.transform.position;
            defender.PendingShotTimer = defender.Config.ProjectileDelay;
        }
    }

    private bool IsValidPendingHeroShot(Defender defender)
    {
        // The target may die during the animation delay; the committed shot still fires at
        // the position captured when the attack began. Only cancel if combat itself is invalid.
        return CanCombat && defender != null && defender.View != null && defender.Config != null;
    }

    private void FireProjectile(Defender defender, SkeletonEnemy target)
    {
        if (target == null || target.IsDead)
            return;

        FireProjectile(defender.View, defender.Config.DisplayName, defender.Config,
            GetHeroDamage(defender.Config.Army), GetHeroSplashRadius(defender.Config.Army),
            target.transform.position, target, defender.Config.ProjectileAnimatorController,
            GetAttackSpawnPosition(defender));
    }

    private void FireProjectile(Defender defender, Vector3 destination)
    {
        if (defender == null || defender.View == null || defender.Config == null)
            return;

        FireProjectile(defender.View, defender.Config.DisplayName, defender.Config,
            GetHeroDamage(defender.Config.Army), GetHeroSplashRadius(defender.Config.Army), destination, null,
            defender.Config.ProjectileAnimatorController, GetAttackSpawnPosition(defender));
    }

    private static Vector3 GetAttackSpawnPosition(Defender defender)
    {
        if (defender.AttackSpawn != null)
            return defender.AttackSpawn.position;

        return defender.View.transform.position + Vector3.right * 0.2f;
    }

    private void FireProjectile(GameObject source, string sourceName, HeroConfig config, float damage,
        float splashRadius, SkeletonEnemy target, RuntimeAnimatorController animatorController = null,
        Vector3? launchPosition = null)
    {
        if (target == null || target.IsDead)
            return;

        FireProjectile(source, sourceName, config, damage, splashRadius, target.transform.position, target,
            animatorController, launchPosition);
    }

    private void FireProjectile(GameObject source, string sourceName, HeroConfig config, float damage,
        float splashRadius, Vector3 destination, SkeletonEnemy target, RuntimeAnimatorController animatorController = null,
        Vector3? launchPosition = null)
    {
        if (source == null || config == null)
            return;

        GameSoundManager.Instance?.PlayAttack(config.Army);

        Vector3 origin = launchPosition ?? source.transform.position + Vector3.right * 0.2f;
        GameObject view = new GameObject("Proyectil - " + sourceName);
        view.transform.position = origin;
        float projectileScale = animatorController != null ? config.ProjectileSize : 0.14f;
        view.transform.localScale = Vector3.one * projectileScale;
        SetProjectileVisualDirection(view.transform, destination - origin);

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 15;
        if (animatorController != null)
        {
            Animator animator = view.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
        }
        else
        {
            if (projectileSprite == null)
                projectileSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.sprite = projectileSprite;
            renderer.color = config.ProjectileColor;
        }

        projectiles.Add(new Projectile
        {
            View = view,
            Destination = destination,
            Target = target,
            Damage = damage,
            DamageColor = config.ProjectileColor,
            SplashRadius = splashRadius,
            Speed = config.ProjectileSpeed,
            RemainingLifetime = config.ProjectileLifetime
        });
    }

    private void UpdateProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (projectile.View == null)
            {
                projectiles.RemoveAt(i);
                continue;
            }

            Transform view = projectile.View.transform;
            if (projectile.Target != null)
                projectile.Destination = projectile.Target.transform.position;

            Vector3 previousPosition = view.position;
            view.position = Vector3.MoveTowards(view.position, projectile.Destination, projectile.Speed * Time.deltaTime);
            SetProjectileVisualDirection(view, view.position - previousPosition);
            projectile.RemainingLifetime -= Time.deltaTime;
            SkeletonEnemy hit = FindProjectileImpact(previousPosition, view.position);
            if (hit != null)
            {
                ApplyProjectileDamage(projectile, hit);
                Destroy(projectile.View);
                projectiles.RemoveAt(i);
            }
            else if (projectile.RemainingLifetime <= 0f || Vector3.Distance(view.position, projectile.Destination) <= 0.08f)
            {
                Destroy(projectile.View);
                projectiles.RemoveAt(i);
            }
        }
    }

    private static void SetProjectileVisualDirection(Transform visual, Vector3 direction)
    {
        if (visual == null || direction.sqrMagnitude <= 0.000001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        visual.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private SkeletonEnemy FindProjectileImpact(Vector3 start, Vector3 end)
    {
        Vector2 start2D = start;
        Vector2 segment = (Vector2)end - start2D;
        float segmentLengthSquared = segment.sqrMagnitude;
        float firstImpact = float.MaxValue;
        SkeletonEnemy hit = null;

        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            Vector2 enemyPosition = enemy.transform.position;
            float alongSegment = segmentLengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(enemyPosition - start2D, segment) / segmentLengthSquared)
                : 0f;
            Vector2 closestPoint = start2D + segment * alongSegment;
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            float enemyRadius = renderer != null
                ? Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y)
                : 0.2f;
            float hitRadius = enemyRadius + 0.08f;

            if (alongSegment < firstImpact && (enemyPosition - closestPoint).sqrMagnitude <= hitRadius * hitRadius)
            {
                firstImpact = alongSegment;
                hit = enemy;
            }
        }

        return hit;
    }

    private void ApplyProjectileDamage(Projectile projectile, SkeletonEnemy impactTarget)
    {
        if (impactTarget == null || impactTarget.IsDead)
            return;

        if (projectile.SplashRadius > 0f)
        {
            Vector3 impactPosition = impactTarget.transform.position;
            foreach (SkeletonEnemy enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && Vector2.Distance(impactPosition, enemy.transform.position) <= projectile.SplashRadius)
                    ApplyDamage(enemy, projectile.Damage, projectile.DamageColor);
            }
        }
        else
        {
            ApplyDamage(impactTarget, projectile.Damage, projectile.DamageColor);
        }
    }

    private void ApplyDamage(SkeletonEnemy enemy, float amount, Color damageColor)
    {
        if (enemy == null || enemy.IsDead)
            return;

        ShowDamage(enemy, amount, damageColor);
        if (enemy.ApplyDamage(amount) && !enemy.RewardGranted)
        {
            enemy.MarkRewardGranted();
            if (economy != null)
                economy.RewardEnemyDefeated(enemy.IsBoss);
        }
    }

    private void ShowDamage(SkeletonEnemy enemy, float amount, Color damageColor)
    {
        if (enemy == null)
            return;

        GameObject view = new GameObject("Daño - " + Mathf.RoundToInt(amount));
        view.transform.position = enemy.transform.position + Vector3.up * (enemy.IsBoss ? 0.75f : 0.55f);
        TextMesh text = view.AddComponent<TextMesh>();
        text.font = damageFont;
        text.fontSize = 30;
        text.text = "-" + Mathf.RoundToInt(amount);
        text.characterSize = 0.1f;
        text.alignment = TextAlignment.Center;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = damageColor;
        MeshRenderer renderer = view.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = damageFont.material;
        renderer.localBounds = new Bounds(Vector3.zero, new Vector3(4f, 1.5f, 1f));
        renderer.sortingOrder = 20;
        floatingDamage.Add(new FloatingDamage { View = view, RemainingLifetime = 0.85f });
    }

    private void UpdateFloatingDamage()
    {
        for (int i = floatingDamage.Count - 1; i >= 0; i--)
        {
            FloatingDamage damage = floatingDamage[i];
            if (damage.View == null)
            {
                floatingDamage.RemoveAt(i);
                continue;
            }

            damage.View.transform.position += Vector3.up * (0.65f * Time.deltaTime);
            damage.RemainingLifetime -= Time.deltaTime;
            if (damage.RemainingLifetime <= 0f)
            {
                Destroy(damage.View);
                floatingDamage.RemoveAt(i);
            }
        }
    }

    private SkeletonEnemy FindTarget(Defender defender)
    {
        return defender == null || defender.View == null || defender.Config == null
            ? null : FindTarget(defender.View.transform.position, GetHeroRange(defender.Config.Army));
    }

    private SkeletonEnemy FindTarget(Vector3 origin, float range)
    {
        SkeletonEnemy best = null;
        float bestDistance = range;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            float distance = Vector2.Distance(origin, enemy.transform.position);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    internal bool CanCombat => matchStatus == MatchStatus.InProgress && waveInProgress && !paused && !finished && !purchasePhase;

    internal void ApplyCastleDamage(float amount)
    {
        if (!CanCombat || amount <= 0f)
            return;
        float actualDamage = Mathf.Min(castleHealth, amount);
        castleHealth = Mathf.Max(0f, castleHealth - actualDamage);
        StateRevision++;
        if (castleHealth <= 0f)
            EndMatch(false);
    }

    public void RestartFinishedMatch()
    {
        if (matchStatus != MatchStatus.Finished)
            return;

        ResetMatch(true);
    }

    internal void ConfigureTower(DefenseTower tower, TowerConfig config)
    {
        if (tower == null || config == null || config.Hero == null)
            return;

        tower.Configure(this, config);
    }

    internal bool TryTowerAttack(DefenseTower tower)
    {
        if (!CanCombat || tower == null)
            return false;

        SkeletonEnemy target = FindTarget(tower.transform.position, tower.Range);
        if (target == null)
            return false;

        FireProjectile(tower.gameObject, tower.Config.DisplayName, tower.Config.Hero,
            tower.Damage, tower.SplashRadius, target);
        return true;
    }

    private void RemoveFinishedEnemyDeaths()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null)
            {
                enemies.RemoveAt(i);
            }
        }
    }

    private void EndMatch(bool didWin)
    {
        if (finished)
            return;

        finished = true;
        victory = didWin;
        matchStatus = MatchStatus.Finished;
        StateRevision++;
        Debug.Log(didWin ? "Partida finalizada: victoria." : "Partida finalizada: derrota.", this);
        if (!didWin)
            GameSoundManager.Instance?.PlayDefeat();
    }

    private void OnDestroy()
    {
        if (projectileSprite != null)
            Destroy(projectileSprite);
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
