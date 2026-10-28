using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the first playable TRIUNVIRATO prototype using scene-authored objects.</summary>
public sealed class TriunviratoGame : MonoBehaviour
{
    private const string NormalAttackState = "Attack_Normal";
    public enum MatchStatus { WaitingForPlayers, InProgress, Finished }
    private enum DamageCategory { Hero = 0, Tower = 1, SpecialAbility = 2, Clone = 3 }
    private const float CastleStartHealth = 100f;
    private const int SpecialAbilityStars = 3;
    private const string FireRageAbilityId = "fire_rage";
    private const string HeroCloneAbilityId = "hero_clone";
    private const string KnightSummonAbilityId = "knight_summon";
    private const string WarBlessingAbilityId = "war_blessing";
    private const string WaterTornadoAbilityId = "water_tornado";
    private const string ElectricBirdAbilityId = "electric_bird";
    [SerializeField] private Transform tierraHero;
    [SerializeField] private Transform aireHero;
    [SerializeField] private Transform marHero;
    [SerializeField] private HeroConfig tierraConfig;
    [SerializeField] private HeroConfig aireConfig;
    [SerializeField] private HeroConfig marConfig;
    [SerializeField] private Transform enemySpawner;
    [SerializeField] private Transform castleTarget;
    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private Font damageFont;
    [SerializeField] private UpgradeCatalog upgradeCatalog;
    [Header("Hero Special Abilities")]
    [SerializeField] private ZombieGraspAbilityConfig zombieGraspAbility;
    [SerializeField] private MeteorShowerAbilityConfig meteorShowerAbility;
    [SerializeField] private LightningStormAbilityConfig lightningStormAbility;
    [SerializeField] private FireRageAbilityConfig fireRageAbility;
    [SerializeField] private HeroCloneAbilityConfig heroCloneAbility;
    [SerializeField] private KnightSummonAbilityConfig knightSummonAbility;
    [SerializeField] private WarBlessingAbilityConfig warBlessingAbility;
    [SerializeField] private WaterTornadoAbilityConfig waterTornadoAbility;
    [SerializeField] private ElectricBirdAbilityConfig electricBirdAbility;
    [SerializeField, Tooltip("Si se asigna, los límites del SpriteRenderer delimitan dónde caen los meteoritos.")]
    private SpriteRenderer meteorShowerArea;
    private readonly List<SkeletonEnemy> enemies = new List<SkeletonEnemy>();
    private readonly List<KnightAlly> knightAllies = new List<KnightAlly>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<TornadoProjectile> tornadoProjectiles = new List<TornadoProjectile>();
    private readonly List<ElectricBirdProjectile> electricBirdProjectiles = new List<ElectricBirdProjectile>();
    private readonly List<FloatingDamage> floatingDamage = new List<FloatingDamage>();
    private readonly List<GameObject> specialEffects = new List<GameObject>();
    private Defender[] defenders;
    private Sprite projectileSprite;
    private Sprite towerProjectileSprite;
    private Texture2D towerProjectileTexture;
    private float castleHealth;
    private float spawnTimer;
    private int wave;
    private int remainingToSpawn;
    private bool waveInProgress;
    private bool paused;
    private bool finished;
    private bool victory;
    private readonly float[] towerUpgradeMultipliers = { 1f, 1f, 1f };
    private readonly float[] heroUpgradeMultipliers = { 1f, 1f, 1f };
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
    private readonly float[,] damageDealt = new float[3, 4];
    private readonly float[,] finalDamageDealt = new float[3, 4];
    private float marBossDamageBonusPercent;
    [Header("Progresión individual")]
    [SerializeField, Min(1f)] private float tierraExperienceToLevel = 100f;
    [SerializeField, Min(1f)] private float aireExperienceToLevel = 90f;
    [SerializeField, Min(1f)] private float marExperienceToLevel = 110f;
    [SerializeField, Min(0f)] private float initialExperienceMultiplier = 1f;
    [SerializeField, Min(0f)] private float experienceGrowthPerLevel = 0.18f;
    [SerializeField, Min(0f)] private float bossExperienceMultiplier = 5f;
    [SerializeField, Min(0f)] private float tierraEnemyExperience = 10f;
    [SerializeField, Min(0f)] private float aireEnemyExperience = 10f;
    [SerializeField, Min(0f)] private float marEnemyExperience = 10f;
    private readonly float[] experience = new float[3];
    private readonly int[] levels = { 1, 1, 1 };
    private readonly float[] experienceMultipliers = new float[3];
    private readonly float[] experienceRemainders = new float[3];
    private readonly int[] luckStacks = new int[3];
    private readonly List<UpgradeOffer[]>[] queuedUpgradeOffers = { new List<UpgradeOffer[]>(), new List<UpgradeOffer[]>(), new List<UpgradeOffer[]>() };
    private readonly List<UpgradeOffer[]>[] queuedBossOffers = { new List<UpgradeOffer[]>(), new List<UpgradeOffer[]>(), new List<UpgradeOffer[]>() };
    private readonly float[] graspNormalDurationBonus = new float[3];
    private readonly float[] graspBossDurationBonus = new float[3];
    private readonly int[] meteorCountBonus = new int[3];
    private readonly float[] meteorDamageBonus = new float[3];
    private readonly int[] lightningHitCountBonus = new int[3];
    private readonly float[] lightningDamageBonus = new float[3];
    private float fireRageDamageBonusPercent;
    private float fireRageDurationBonus;
    private float cloneDurationBonus;
    private float cloneCooldownReduction;
    private int knightUpgradeLevel;
    private int knightCooldownReductionLevel;
    private int warBlessingPowerLevel;
    private int warBlessingCooldownReductionLevel;
    private int tornadoDamageLevel;
    private int tornadoPushLevel;
    private int electricBirdDurationLevel;
    private int electricBirdCooldownLevel;
    private readonly float[] specialCooldownEndsAt = new float[3];
    private readonly Dictionary<string, float> additionalCooldownEndsAt = new Dictionary<string, float>();
    private bool specialCooldownsWereRunning;
    private float nextCooldownStateRefreshAt;
    private bool warnedMissingZombieGraspController;
    private bool warnedMissingMeteorController;
    private bool warnedMissingLightningController;
    private MatchStatus matchStatus = MatchStatus.WaitingForPlayers;
    private const float TargetAspectRatio = 16f / 9f;
    private Camera gameCamera;
    private float baseOrthographicSize;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private CloneRuntime activeClone;
    private bool cloneSpawnPending;
    private float cloneSpawnRemaining;

    [Serializable]
    public sealed class UpgradeOffer
    {
        public string id;
        public string category;
        public string name;
        public string description;
        public int stars;
        public bool isSpecialAbility;
        public string offerToken;
    }

    public MatchStatus Status => matchStatus;
    public float CastleHealth => Mathf.Max(0f, castleHealth);
    public float CastleMaximumHealth => CastleStartHealth;
    public int StateRevision { get; private set; }
    public bool IsMatchInProgress => matchStatus == MatchStatus.InProgress;
    public int CurrentWave => wave;
    public bool IsWaveInProgress => waveInProgress;
    public int GetLevel(ArmyType army) => IsValidArmy(army) ? levels[(int)army] : 1;
    public float GetExperience(ArmyType army) => IsValidArmy(army) ? experience[(int)army] : 0f;
    public float GetExperienceNeeded(ArmyType army) => IsValidArmy(army) ? ExperienceNeededFor(army, levels[(int)army]) : 100f;
    public float GetExperienceMultiplier(ArmyType army) => IsValidArmy(army) ? experienceMultipliers[(int)army] : 1f;
    public float GetExperienceIncrease(ArmyType army) => Mathf.Max(0f, GetExperienceMultiplier(army) - initialExperienceMultiplier);
    public float GetLuckChance(ArmyType army) => !IsValidArmy(army) || upgradeCatalog == null ? 0f :
        Mathf.Clamp01(upgradeCatalog.RareOfferChance + luckStacks[(int)army] * upgradeCatalog.LuckRareChanceIncrement);
    public float GetHeroBossDamageBonusPercent(ArmyType army) => army == ArmyType.Mar ? marBossDamageBonusPercent : 0f;
    public float GetDamageByHero(ArmyType army) => GetDamageForSummary(army, DamageCategory.Hero);
    public float GetDamageByTower(ArmyType army) => GetDamageForSummary(army, DamageCategory.Tower);
    public float GetDamageBySpecialAbility(ArmyType army) => GetDamageForSummary(army, DamageCategory.SpecialAbility);
    public float GetDamageByClone(ArmyType army) => GetDamageForSummary(army, DamageCategory.Clone);
    public float GetExperienceFraction(ArmyType army) => Mathf.Clamp01(GetExperience(army) / Mathf.Max(1f, GetExperienceNeeded(army)));
    private float ExperienceNeededFor(ArmyType army, int level) => Mathf.Max(1f, GetBaseExperienceToLevel(army) * Mathf.Pow(1f + experienceGrowthPerLevel, Mathf.Max(0, level - 1)));
    private float GetBaseExperienceToLevel(ArmyType army) => army == ArmyType.Tierra ? tierraExperienceToLevel : army == ArmyType.Aire ? aireExperienceToLevel : marExperienceToLevel;
    public UpgradeOffer[] GetUpgradeOffers(ArmyType army)
    {
        if (!IsValidArmy(army)) return Array.Empty<UpgradeOffer>();
        int index = (int)army;
        List<UpgradeOffer[]> queue = queuedBossOffers[index].Count > 0 ? queuedBossOffers[index] : queuedUpgradeOffers[index];
        return queue.Count == 0 ? Array.Empty<UpgradeOffer>() : queue[0];
    }
    public bool CanCombatNow => CanCombat;
    public bool IsPaused => paused;
    public int EnemiesRemainingInWave => enemies.Count + remainingToSpawn;
    public bool HasSpecialCooldownsRunning => HasActiveSpecialCooldowns();

    private sealed class Defender
    {
        public GameObject View;
        public HeroConfig Config;
        public Animator Animator;
        public Transform AttackSpawn;
        public TimedCombatEffects Effects;
        public float AttackTimer;
        public bool ShotPending;
        public bool SpecialAnimationActive;
        public bool SpecialAnimationWarningLogged;
        public float SpecialAnimationStartedAt;
        public float PendingShotTimer;
    }

    private sealed class CloneRuntime
    {
        public GameObject View;
        public Animator Animator;
        public TimedCombatEffects Effects;
        public Transform AttackSpawn;
        public float Remaining;
        public float AttackTimer;
        public float PendingShotTimer;
        public float PendingDamage;
        public bool ShotPending;
    }

    private sealed class Projectile
    {
        public GameObject View;
        public Vector3 Destination;
        public SkeletonEnemy Target;
        public float Damage;
        public Color DamageColor;
        public float SplashRadius;
        public ArmyType Army;
        public DamageCategory Category;
        public float Speed;
        public float RemainingLifetime;
    }

    private sealed class TornadoProjectile
    {
        public GameObject View;
        public HashSet<SkeletonEnemy> HitEnemies = new HashSet<SkeletonEnemy>();
    }

    private sealed class ElectricBirdProjectile
    {
        public GameObject View;
        public SkeletonEnemy Target;
        public float RemainingLifetime;
        public float ImpactRemaining;
        public float LaunchDelayRemaining;
        public bool Impacted;
        public bool Launched;
    }

    private sealed class FloatingDamage
    {
        public GameObject View;
        public float RemainingLifetime;
    }

    private void Start()
    {
        gameCamera = Camera.main;
        if (gameCamera != null && gameCamera.orthographic)
            baseOrthographicSize = gameCamera.orthographicSize;
        UpdateGameCameraViewport();
        if (upgradeCatalog == null)
            Debug.LogError("Asigna UpgradeCatalog desde Assets/GameData/Upgrades en el Inspector.", this);
        if (fireRageAbility == null || heroCloneAbility == null || knightSummonAbility == null || warBlessingAbility == null ||
            waterTornadoAbility == null || electricBirdAbility == null)
            Debug.LogError("Asigna las habilidades de Aire, Tierra y Agua desde Assets/GameData/SpecialAbilities en el Inspector.", this);
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
        ResetMatch(false);
    }

    private void Update()
    {
        UpdateGameCameraViewport();
        UpdateSpecialCooldownState();
        UpdateSpecialAnimations();
        CleanupSpecialEffects();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.oKey.wasPressedThisFrame)
                ForceStartNextWaveForDebug();

            if (keyboard.pKey.wasPressedThisFrame && !finished)
            {
                paused = !paused;
                StateRevision++;
            }
        }

        if (matchStatus != MatchStatus.InProgress || paused || finished)
            return;

        UpdateWaveFlow();
        if (finished)
            return;
        UpdateEnemies();
        if (finished)
            return;
        UpdateDefenders();
        UpdateKnightAllies();
        UpdateProjectiles();
        UpdateWaterProjectiles(Time.deltaTime);
        UpdatePendingCloneSpawn();
        UpdateClone();
        UpdateFloatingDamage();
        RemoveFinishedEnemyDeaths();
        TickCombatEffects(Time.deltaTime);

        if (castleHealth <= 0f)
            EndMatch(false);
    }

    private void ForceStartNextWaveForDebug()
    {
        if (matchStatus != MatchStatus.InProgress || finished)
            return;

        foreach (SkeletonEnemy enemy in enemies)
            if (enemy != null) Destroy(enemy.gameObject);
        enemies.Clear();
        foreach (Projectile projectile in projectiles)
            if (projectile.View != null) Destroy(projectile.View);
        projectiles.Clear();
        foreach (FloatingDamage damage in floatingDamage)
            if (damage.View != null) Destroy(damage.View);
        floatingDamage.Clear();

        waveInProgress = false;
        remainingToSpawn = 0;
        StartNextWave();
        Debug.Log("Depuración: inicio forzado de la oleada " + wave + ".", this);
    }

    private void UpdateGameCameraViewport()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null || Screen.width <= 0 || Screen.height <= 0 || !gameCamera.orthographic)
            return;
        if (baseOrthographicSize <= 0f) baseOrthographicSize = gameCamera.orthographicSize;
        if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height) return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        float aspect = (float)Screen.width / Screen.height;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = Color.black;
        if (aspect > TargetAspectRatio)
        {
            float viewportWidth = TargetAspectRatio / aspect;
            gameCamera.rect = new Rect((1f - viewportWidth) * 0.5f, 0f, viewportWidth, 1f);
            gameCamera.orthographicSize = baseOrthographicSize;
        }
        else
        {
            gameCamera.rect = new Rect(0f, 0f, 1f, 1f);
            gameCamera.orthographicSize = baseOrthographicSize * TargetAspectRatio / aspect;
        }
    }

    private void ResetMatch(bool resumeMatch)
    {
        StopAllCoroutines();
        cloneSpawnPending = false;
        cloneSpawnRemaining = 0f;
        RemoveActiveClone();
        ClearKnightAllies();
        ClearWaterProjectiles();
        foreach (GameObject effect in specialEffects)
        {
            if (effect != null)
                Destroy(effect);
        }
        specialEffects.Clear();
        Array.Clear(specialCooldownEndsAt, 0, specialCooldownEndsAt.Length);
        additionalCooldownEndsAt.Clear();
        specialCooldownsWereRunning = false;
        nextCooldownStateRefreshAt = 0f;
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
        wave = 0;
        remainingToSpawn = 0;
        waveInProgress = false;
        paused = false;
        finished = false;
        victory = false;
        matchStatus = resumeMatch ? MatchStatus.InProgress : MatchStatus.WaitingForPlayers;
        StateRevision++;
        if (resumeMatch)
        {
            GameSoundManager.Instance?.PlayNextMatchTheme();
            GameSoundManager.Instance?.PlayMatchStart();
        }
        Array.Clear(towerDamageFixedBonus, 0, towerDamageFixedBonus.Length);
        Array.Clear(towerAttackRateFixedBonus, 0, towerAttackRateFixedBonus.Length);
        Array.Clear(towerRangeFixedBonus, 0, towerRangeFixedBonus.Length);
        Array.Clear(heroDamageFixedBonus, 0, heroDamageFixedBonus.Length);
        Array.Clear(damageDealt, 0, damageDealt.Length);
        Array.Clear(finalDamageDealt, 0, finalDamageDealt.Length);
        marBossDamageBonusPercent = 0f;
        Array.Clear(heroAttackRateFixedBonus, 0, heroAttackRateFixedBonus.Length);
        Array.Clear(heroRangeFixedBonus, 0, heroRangeFixedBonus.Length);
        Array.Clear(heroSplashFixedBonus, 0, heroSplashFixedBonus.Length);
        Array.Clear(graspNormalDurationBonus, 0, graspNormalDurationBonus.Length);
        Array.Clear(graspBossDurationBonus, 0, graspBossDurationBonus.Length);
        Array.Clear(meteorCountBonus, 0, meteorCountBonus.Length);
        Array.Clear(meteorDamageBonus, 0, meteorDamageBonus.Length);
        Array.Clear(lightningHitCountBonus, 0, lightningHitCountBonus.Length);
        Array.Clear(lightningDamageBonus, 0, lightningDamageBonus.Length);
        fireRageDamageBonusPercent = 0f;
        fireRageDurationBonus = 0f;
        cloneDurationBonus = 0f;
        cloneCooldownReduction = 0f;
        knightUpgradeLevel = 0;
        knightCooldownReductionLevel = 0;
        warBlessingPowerLevel = 0;
        warBlessingCooldownReductionLevel = 0;
        tornadoDamageLevel = tornadoPushLevel = electricBirdDurationLevel = electricBirdCooldownLevel = 0;
        Array.Clear(experience, 0, experience.Length);
        Array.Clear(experienceRemainders, 0, experienceRemainders.Length);
        Array.Clear(luckStacks, 0, luckStacks.Length);
        for (int i = 0; i < 3; i++) { queuedUpgradeOffers[i].Clear(); queuedBossOffers[i].Clear(); }
        for (int i = 0; i < 3; i++)
        {
            levels[i] = 1;
            experienceMultipliers[i] = initialExperienceMultiplier;
            towerUpgradeMultipliers[i] = heroUpgradeMultipliers[i] = 1f;
            towerSpeedMultipliers[i] = towerRangeMultipliers[i] = 1f;
            heroSpeedMultipliers[i] = heroRangeMultipliers[i] = heroSplashMultipliers[i] = 1f;
        }
        TowerManager towerManager = FindAnyObjectByType<TowerManager>();
        if (towerManager != null)
            towerManager.ResetTowers();
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
                defender.PendingShotTimer = 0f;
                defender.SpecialAnimationActive = false;
                if (defender.Effects != null) defender.Effects.ClearAll();
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
        TimedCombatEffects effects = hero.GetComponent<TimedCombatEffects>();
        if (effects == null) effects = hero.gameObject.AddComponent<TimedCombatEffects>();

        return new Defender
        {
            View = hero.gameObject,
            Config = config,
            Animator = animator,
            AttackSpawn = hero.Find("AttackSpawn"),
            Effects = effects
        };
    }

    private void UpdateWaveFlow()
    {
        if (!waveInProgress)
        {
            StartNextWave();
            return;
        }

        if (remainingToSpawn > 0)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnEnemy(wave % 3 == 0 && remainingToSpawn == 1);
                remainingToSpawn--;
                spawnTimer = GetEnemySpawnInterval();
            }
        }
        else if (enemies.Count == 0)
        {
            waveInProgress = false;
            StartNextWave();
        }
    }

    private float GetEnemySpawnInterval()
    {
        return Mathf.Max(0.4f, 1f / (1f + wave * 0.04f));
    }

    private UpgradeOffer GenerateSpecialAbilityOffer(ArmyType army)
    {
        HeroSpecialAbilityConfig ability = GetSpecialAbilityConfig(army);
        if (upgradeCatalog == null || ability == null) return null;
        string id;
        string name;
        string description;
        float increment;
        switch (army)
        {
            case ArmyType.Tierra:
                int tierraPick = UnityEngine.Random.Range(0, 6);
                if (tierraPick == 2 && knightSummonAbility != null)
                    return NewSpecialUpgrade("special_knight_power", "Caballero · nivel", "+" + (knightSummonAbility.HealthAndDamageIncreasePerLevel * 100f).ToString("0.##") + " % de vida y daño para futuras invocaciones");
                if (tierraPick == 3 && knightSummonAbility != null)
                    return NewSpecialUpgrade("special_knight_cooldown", "Caballero · cooldown", "Cooldown −" + knightSummonAbility.CooldownReductionPerLevel.ToString("0.##") + " s");
                if (tierraPick == 4 && warBlessingAbility != null)
                    return NewSpecialUpgrade("special_war_blessing_power", "Acelerar aliado · potencia", "Velocidad de ataque adicional +" + warBlessingAbility.PowerUpgradePercent.ToString("0.##") + " puntos porcentuales");
                if (tierraPick == 5 && warBlessingAbility != null)
                    return NewSpecialUpgrade("special_war_blessing_cooldown", "Acelerar aliado · cooldown", "Cooldown −" + warBlessingAbility.CooldownReductionPerLevel.ToString("0.##") + " s");
                bool normal = tierraPick == 0 || (tierraPick == 1 && (knightSummonAbility == null || warBlessingAbility == null));
                id = normal ? "special_grasp_normal" : "special_grasp_boss";
                name = normal ? "Agarre prolongado" : "Agarre contra bosses";
                increment = normal ? zombieGraspAbility.NormalDurationUpgrade : zombieGraspAbility.BossDurationUpgrade;
                description = normal ? "Inmovilización contra enemigos normales +" + increment.ToString("0.##") + " s" :
                    "Inmovilización contra bosses +" + increment.ToString("0.##") + " s";
                break;
            case ArmyType.Aire:
                int pick = UnityEngine.Random.Range(0, 6);
                if (pick == 0 && fireRageAbility != null)
                {
                    return new UpgradeOffer { id = "special_fire_rage_damage", category = "specialabilities",
                        name = "Furia ígnea · potencia", description = "Daño adicional de Furia ígnea +" + fireRageAbility.DamageUpgradePercent.ToString("0.##") + " puntos porcentuales",
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                }
                if (pick == 1 && fireRageAbility != null)
                {
                    return new UpgradeOffer { id = "special_fire_rage_duration", category = "specialabilities",
                        name = "Furia ígnea · duración", description = "Duración de Furia ígnea +" + fireRageAbility.DurationUpgradeSeconds.ToString("0.##") + " s",
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                }
                if (pick == 2 && heroCloneAbility != null)
                {
                    return new UpgradeOffer { id = "special_clone_duration", category = "specialabilities",
                        name = "Clon · duración", description = "Duración del clon +" + heroCloneAbility.DurationUpgradeSeconds.ToString("0.##") + " s",
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                }
                if (pick == 3 && heroCloneAbility != null)
                {
                    return new UpgradeOffer { id = "special_clone_cooldown", category = "specialabilities",
                        name = "Clon · cooldown", description = "Cooldown del clon −" + heroCloneAbility.CooldownReductionSeconds.ToString("0.##") + " s",
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                }
                if (pick == 4 && meteorShowerAbility != null)
                    return new UpgradeOffer { id = "special_meteor_count", category = "specialabilities",
                        name = "Lluvia abundante", description = "Meteoritos por activación +" + meteorShowerAbility.MeteorCountUpgrade.ToString("0.##"),
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                if (pick == 5 && meteorShowerAbility != null)
                    return new UpgradeOffer { id = "special_meteor_damage", category = "specialabilities",
                        name = "Meteoritos devastadores", description = "Daño por meteorito +" + meteorShowerAbility.DamageUpgrade.ToString("0.##"),
                        stars = SpecialAbilityStars, isSpecialAbility = true };
                bool moreMeteors = UnityEngine.Random.Range(0, 2) == 0;
                id = moreMeteors ? "special_meteor_count" : "special_meteor_damage";
                name = moreMeteors ? "Lluvia abundante" : "Meteoritos devastadores";
                increment = moreMeteors ? meteorShowerAbility.MeteorCountUpgrade : meteorShowerAbility.DamageUpgrade;
                description = moreMeteors ? "Meteoritos por activación +" + increment.ToString("0.##") :
                    "Daño por meteorito +" + increment.ToString("0.##");
                break;
            default:
                int marPick = UnityEngine.Random.Range(0, 6);
                if (marPick == 2 && waterTornadoAbility != null)
                    return NewSpecialUpgrade("special_water_tornado_damage", "Huracán · potencia", "Daño del huracán +" + waterTornadoAbility.DamageUpgradePercent.ToString("0.##") + " % por nivel");
                if (marPick == 3 && waterTornadoAbility != null)
                    return NewSpecialUpgrade("special_water_tornado_push", "Huracán · empuje", "Distancia de empuje +" + waterTornadoAbility.PushDistanceUpgrade.ToString("0.##") + " por nivel");
                if (marPick == 4 && electricBirdAbility != null)
                    return NewSpecialUpgrade("special_electric_bird_duration", "Ave eléctrica · duración", "Debuff +" + electricBirdAbility.DurationUpgradeSeconds.ToString("0.##") + " s por nivel");
                if (marPick == 5 && electricBirdAbility != null)
                    return NewSpecialUpgrade("special_electric_bird_cooldown", "Ave eléctrica · cooldown", "Cooldown −" + electricBirdAbility.CooldownReductionSeconds.ToString("0.##") + " s por nivel");
                if (marPick >= 2) marPick = UnityEngine.Random.Range(0, 2);
                bool moreHits = marPick == 0;
                id = moreHits ? "special_lightning_count" : "special_lightning_damage";
                name = moreHits ? "Tormenta intensificada" : "Rayos devastadores";
                increment = moreHits ? lightningStormAbility.HitCountUpgrade : lightningStormAbility.DamageUpgrade;
                description = moreHits ? "Impactos de rayo por activación +" + increment.ToString("0.##") :
                    "Daño por impacto +" + increment.ToString("0.##");
                break;
        }
        return new UpgradeOffer { id = id, category = "specialabilities", name = name, description = description,
            stars = SpecialAbilityStars, isSpecialAbility = true };
    }

    private static UpgradeOffer NewSpecialUpgrade(string id, string name, string description) =>
        new UpgradeOffer { id = id, category = "specialabilities", name = name, description = description,
            stars = SpecialAbilityStars, isSpecialAbility = true };

    public bool TrySelectUpgrade(ArmyType army, string id, string offerToken, out string message)
    {
        message = "No hay una oferta pendiente.";
        if (!CanCombat || !IsValidArmy(army) || string.IsNullOrEmpty(id)) return false;
        int index = (int)army;
        List<UpgradeOffer[]> queue = queuedBossOffers[index].Count > 0 ? queuedBossOffers[index] : queuedUpgradeOffers[index];
        UpgradeOffer offer = queue.Count == 0 ? null : Array.Find(queue[0], item => item != null && item.id == id && !string.IsNullOrEmpty(offerToken) && item.offerToken == offerToken);
        if (offer == null)
        { message = "La opción no pertenece a tu oferta activa."; return false; }
        UpgradeCatalog.Definition definition = upgradeCatalog != null ? upgradeCatalog.Find(id) : null;
        if (offer.isSpecialAbility) ApplySpecialAbilityUpgrade(army, id);
        else if (definition == null || !IsUpgradeApplicable(army, definition))
        { message = "La mejora ya no está disponible."; return false; }
        else ApplyUpgradeEffect(army, definition,
            offer.stars >= 3 ? definition.legendaryEffect : UpgradeCatalog.GetOfferEffect(definition, offer.stars));
        queue.RemoveAt(0);
        StateRevision++;
        message = "Mejora aplicada: " + offer.name + ".";
        return true;
    }

    private float GetConfiguredBaseExperience(int index) => index == 0 ? tierraEnemyExperience : index == 1 ? aireEnemyExperience : marEnemyExperience;

    private void GrantEnemyExperience(bool boss)
    {
        for (int i = 0; i < 3; i++)
        {
            float baseReward = GetConfiguredBaseExperience(i) * (boss ? bossExperienceMultiplier : 1f);
            float total = baseReward * experienceMultipliers[i] + experienceRemainders[i];
            int whole = Mathf.FloorToInt(total + 0.00001f);
            experienceRemainders[i] = total - whole;
            experience[i] += whole;
            ArmyType army = (ArmyType)i;
            while (experience[i] >= ExperienceNeededFor(army, levels[i]))
            {
                experience[i] -= ExperienceNeededFor(army, levels[i]);
                levels[i]++;
                queuedUpgradeOffers[i].Add(GenerateLevelOffer((ArmyType)i));
            }
        }
        StateRevision++;
    }

    private UpgradeOffer[] GenerateLevelOffer(ArmyType army)
    {
        return GenerateProgressionOffer(army, 1);
    }

    private UpgradeOffer[] GenerateProgressionOffer(ArmyType army, int stars)
    {
        if (upgradeCatalog == null) return new[] { new UpgradeOffer { id = "unavailable", name = "Sin mejoras disponibles", stars = stars, offerToken = "unavailable" } };
        List<UpgradeCatalog.Definition> eligible = new List<UpgradeCatalog.Definition>();
        foreach (var definition in upgradeCatalog.Definitions)
        {
            if (definition == null || (definition.eligibleArmies != null && definition.eligibleArmies.Length > 0 && Array.IndexOf(definition.eligibleArmies, army) < 0))
                continue;
            if (definition.effect == UpgradeEffect.UnlockTower)
            {
                TowerManager towerManager = FindAnyObjectByType<TowerManager>();
                if (stars < 3 || towerManager == null || towerManager.HasTower(army)) continue;
            }
            else if (!IsUpgradeApplicable(army, definition)) continue;
            eligible.Add(definition);
        }
        List<UpgradeOffer> result = new List<UpgradeOffer>(3);
        float rareChance = Mathf.Clamp01(upgradeCatalog.RareOfferChance + luckStacks[(int)army] * upgradeCatalog.LuckRareChanceIncrement);
        if (stars >= 3)
        {
            UpgradeCatalog.Definition towerUnlock = eligible.Find(definition => definition.effect == UpgradeEffect.UnlockTower);
            if (towerUnlock != null)
            {
                eligible.Remove(towerUnlock);
                result.Add(new UpgradeOffer { id = towerUnlock.id, offerToken = Guid.NewGuid().ToString("N"), category = "towers",
                    name = towerUnlock.displayName, description = UpgradeCatalog.FormatDescription(towerUnlock, 3), stars = 3 });
            }
            UpgradeOffer special = upgradeCatalog.SpecialAbilityOffersAfterBoss ? GenerateSpecialAbilityOffer(army) : null;
            if (special != null) { special.offerToken = Guid.NewGuid().ToString("N"); special.stars = 3; result.Add(special); }
        }
        while (result.Count < 3 && eligible.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, eligible.Count);
            var definition = eligible[pick]; eligible.RemoveAt(pick);
            int optionStars = stars >= 3 ? 3 : (UnityEngine.Random.value < rareChance ? 2 : 1);
            string id = definition.id;
            string category = definition.effect == UpgradeEffect.ExperienceMultiplier || definition.effect == UpgradeEffect.Luck
                ? "progression" : definition.category.ToString().ToLowerInvariant();
            result.Add(new UpgradeOffer { id = id, offerToken = Guid.NewGuid().ToString("N"), category = category,
                name = definition.displayName, description = UpgradeCatalog.FormatDescription(definition, optionStars, upgradeCatalog.LuckRareChanceIncrement), stars = optionStars });
        }
        if (result.Count < 3) Debug.LogWarning("Catálogo: solo hay " + result.Count + " mejoras elegibles para " + army + "; no se duplicarán.", this);
        if (result.Count == 0) result.Add(new UpgradeOffer { id = "unavailable", offerToken = Guid.NewGuid().ToString("N"), name = "Sin mejoras elegibles", description = "Revisa el catálogo de mejoras.", stars = stars });
        return result.ToArray();
    }

    private void QueueBossOffers()
    {
        for (int i = 0; i < 3; i++) queuedBossOffers[i].Add(GenerateProgressionOffer((ArmyType)i, 3));
        StateRevision++;
    }

    private void ApplySpecialAbilityUpgrade(ArmyType army, string id)
    {
        int index = (int)army;
        switch (id)
        {
            case "special_grasp_normal": graspNormalDurationBonus[index] += zombieGraspAbility.NormalDurationUpgrade; break;
            case "special_grasp_boss": graspBossDurationBonus[index] += zombieGraspAbility.BossDurationUpgrade; break;
            case "special_meteor_count": meteorCountBonus[index] += meteorShowerAbility.MeteorCountUpgrade; break;
            case "special_meteor_damage": meteorDamageBonus[index] += meteorShowerAbility.DamageUpgrade; break;
            case "special_lightning_count": lightningHitCountBonus[index] += lightningStormAbility.HitCountUpgrade; break;
            case "special_lightning_damage": lightningDamageBonus[index] += lightningStormAbility.DamageUpgrade; break;
            case "special_fire_rage_damage": if (army == ArmyType.Aire && fireRageAbility != null) fireRageDamageBonusPercent += fireRageAbility.DamageUpgradePercent; break;
            case "special_fire_rage_duration": if (army == ArmyType.Aire && fireRageAbility != null) fireRageDurationBonus += fireRageAbility.DurationUpgradeSeconds; break;
            case "special_clone_duration": if (army == ArmyType.Aire && heroCloneAbility != null) cloneDurationBonus += heroCloneAbility.DurationUpgradeSeconds; break;
            case "special_clone_cooldown": if (army == ArmyType.Aire && heroCloneAbility != null) cloneCooldownReduction += heroCloneAbility.CooldownReductionSeconds; break;
            case "special_knight_power": if (army == ArmyType.Tierra && knightSummonAbility != null) knightUpgradeLevel++; break;
            case "special_knight_cooldown": if (army == ArmyType.Tierra && knightSummonAbility != null) knightCooldownReductionLevel++; break;
            case "special_war_blessing_power": if (army == ArmyType.Tierra && warBlessingAbility != null) warBlessingPowerLevel++; break;
            case "special_war_blessing_cooldown": if (army == ArmyType.Tierra && warBlessingAbility != null) warBlessingCooldownReductionLevel++; break;
            case "special_water_tornado_damage": if (army == ArmyType.Mar && waterTornadoAbility != null) tornadoDamageLevel++; break;
            case "special_water_tornado_push": if (army == ArmyType.Mar && waterTornadoAbility != null) tornadoPushLevel++; break;
            case "special_electric_bird_duration": if (army == ArmyType.Mar && electricBirdAbility != null) electricBirdDurationLevel++; break;
            case "special_electric_bird_cooldown": if (army == ArmyType.Mar && electricBirdAbility != null) electricBirdCooldownLevel++; break;
        }
    }

    private void ApplyUpgradeEffect(ArmyType army, UpgradeCatalog.Definition definition, float effect)
    {
        int index = (int)army;
        bool percentage = definition.valueType == UpgradeValueType.Percentage;
        float multiplier = percentage ? 1f + effect / 100f : 1f;
        switch (definition.effect)
        {
            case UpgradeEffect.UnlockTower:
                FindAnyObjectByType<TowerManager>()?.UnlockTower(army);
                break;
            case UpgradeEffect.HeroBossDamage:
                if (army == ArmyType.Mar) marBossDamageBonusPercent += effect;
                break;
            case UpgradeEffect.TowerDamage: if (percentage) towerUpgradeMultipliers[index] *= multiplier; else towerDamageFixedBonus[index] += effect; break;
            case UpgradeEffect.TowerAttackSpeed: if (percentage) towerSpeedMultipliers[index] *= multiplier; else towerAttackRateFixedBonus[index] += effect; break;
            case UpgradeEffect.TowerRange: if (percentage) towerRangeMultipliers[index] *= multiplier; else towerRangeFixedBonus[index] += effect; break;
            case UpgradeEffect.HeroDamage: if (percentage) heroUpgradeMultipliers[index] *= multiplier; else heroDamageFixedBonus[index] += effect; break;
            case UpgradeEffect.HeroAttackSpeed: if (percentage) heroSpeedMultipliers[index] *= multiplier; else heroAttackRateFixedBonus[index] += effect; break;
            case UpgradeEffect.HeroRange: if (percentage) heroRangeMultipliers[index] *= multiplier; else heroRangeFixedBonus[index] += effect; break;
            case UpgradeEffect.HeroSplash: if (percentage) heroSplashMultipliers[index] *= multiplier; else heroSplashFixedBonus[index] += effect; break;
            case UpgradeEffect.ExperienceMultiplier: experienceMultipliers[index] += effect / 100f; break;
            case UpgradeEffect.Luck: luckStacks[index] += Mathf.Max(1, Mathf.RoundToInt(effect)); break;
        }
        if (definition.category == UpgradeCategory.Towers)
            FindAnyObjectByType<TowerManager>()?.RefreshArmyUpgrade(army);
    }

    private bool IsUpgradeApplicable(ArmyType army, UpgradeCatalog.Definition definition)
    {
        if (definition.category == UpgradeCategory.Towers)
        {
            TowerManager towerManager = FindAnyObjectByType<TowerManager>();
            if (definition.effect == UpgradeEffect.UnlockTower)
                return towerManager != null && !towerManager.HasTower(army);
            TowerConfig tower = towerManager != null ? towerManager.GetConfig(army) : null;
            if (towerManager == null || !towerManager.HasTower(army) || tower == null || tower.Hero == null)
                return false;
            if (definition.effect == UpgradeEffect.TowerRange) return tower.Hero.Range > 0f;
            if (definition.effect == UpgradeEffect.TowerAttackSpeed) return tower.Hero.AttackCooldown > 0f;
            return true;
        }
        HeroConfig hero = GetHeroConfig(army);
        if (hero == null) return false;
        if (definition.effect == UpgradeEffect.HeroBossDamage) return army == ArmyType.Mar;
        if (definition.effect == UpgradeEffect.HeroSplash) return hero.SplashRadius > 0f;
        if (definition.effect == UpgradeEffect.HeroRange) return hero.Range > 0f;
        if (definition.effect == UpgradeEffect.HeroAttackSpeed) return hero.AttackCooldown > 0f;
        return true;
    }

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
    public float GetTowerDamage(ArmyType army, float baseDamage) => baseDamage * GetTowerUpgradeMultiplier(army) + GetTowerDamageFixedBonus(army);
    public float GetTowerDamageFixedBonus(ArmyType army) => IsValidArmy(army) ? towerDamageFixedBonus[(int)army] : 0f;
    public float GetHeroDamageFixedBonus(ArmyType army) => IsValidArmy(army) ? heroDamageFixedBonus[(int)army] : 0f;
    public float GetTowerUpgradeMultiplier(ArmyType army) => IsValidArmy(army) ? towerUpgradeMultipliers[(int)army] : 1f;
    public float GetHeroUpgradeMultiplier(ArmyType army) => IsValidArmy(army) ? heroUpgradeMultipliers[(int)army] : 1f;

    private float GetDamageForSummary(ArmyType army, DamageCategory category)
    {
        if (!IsValidArmy(army)) return 0f;
        int armyIndex = (int)army;
        return matchStatus == MatchStatus.Finished
            ? finalDamageDealt[armyIndex, (int)category]
            : damageDealt[armyIndex, (int)category];
    }

    private void StartNextWave()
    {
        wave++;
        remainingToSpawn = 4 + wave * 2;
        waveInProgress = true;
        spawnTimer = 0f;
        StateRevision++;
    }

    public bool TryStartMatch()
    {
        if (matchStatus != MatchStatus.WaitingForPlayers)
            return false;
        matchStatus = MatchStatus.InProgress;
        paused = false;
        finished = false;
        StateRevision++;
        Debug.Log("Partida iniciada.", this);
        GameSoundManager.Instance?.PlayMatchStart();
        StartNextWave();
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
        if (!IsValidArmy(army)) return 0f;
        TimedCombatEffects effects = defenders != null && defenders[(int)army] != null ? defenders[(int)army].Effects : null;
        return GetHeroDamageWithEffects(army, effects);
    }

    private float GetHeroDamageWithEffects(ArmyType army, TimedCombatEffects effects)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        return (config.Damage * GetHeroUpgradeMultiplier(army) + GetHeroDamageFixedBonus(army)) *
               (effects != null ? effects.DamageMultiplier : 1f);
    }

    public float GetHeroAttackCooldown(ArmyType army)
    {
        if (!IsValidArmy(army)) return 0f;
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        TimedCombatEffects effects = defenders != null && defenders[(int)army] != null ? defenders[(int)army].Effects : null;
        return GetHeroAttackCooldownWithEffects(army, effects);
    }

    private float GetHeroAttackCooldownWithEffects(ArmyType army, TimedCombatEffects effects)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        float cooldown = ApplyHeroAttackSpeedToCooldown(config.AttackCooldown, army);
        return effects != null && effects.AttackSpeedMultiplier > 0f ? cooldown / effects.AttackSpeedMultiplier : cooldown;
    }

    public float GetSpecialAbilityCooldown(ArmyType army) =>
        ApplyHeroAttackSpeedToCooldown(GetBaseSpecialCooldown(army), army);

    public float GetSpecialAbilityCooldownRemaining(ArmyType army) =>
        IsValidArmy(army) ? Mathf.Max(0f, specialCooldownEndsAt[(int)army] - Time.time) : 0f;

    public bool CanActivateSpecial(ArmyType army) => CanActivateSpecial(army, false);

    public bool CanActivateSpecial(ArmyType army, bool bypassCooldown) =>
        CanCombat && IsValidArmy(army) && defenders != null && defenders[(int)army] != null &&
        GetSpecialAbilityConfig(army) != null &&
        (bypassCooldown || GetSpecialAbilityCooldownRemaining(army) <= 0f) && HasLivingEnemies();

    public float GetFireRageDamageBonusPercent() => fireRageAbility != null ? fireRageAbility.BonusDamagePercent + fireRageDamageBonusPercent : 0f;
    public string GetFireRageAbilityName() => fireRageAbility != null ? fireRageAbility.AbilityName : "Furia ígnea";
    public string GetFireRageAbilityDescription() => fireRageAbility != null ? fireRageAbility.Description : string.Empty;
    public float GetFireRageDuration() => fireRageAbility != null ? fireRageAbility.Duration + fireRageDurationBonus : 0f;
    public float GetFireRageCooldown() => fireRageAbility != null ? fireRageAbility.Cooldown : 0f;
    public float GetFireRageCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Aire, FireRageAbilityId);
    public bool CanActivateFireRage(bool bypassCooldown = false) => CanCombat && defenders != null && defenders[(int)ArmyType.Aire] != null &&
        fireRageAbility != null && (bypassCooldown || GetFireRageCooldownRemaining() <= 0f);
    public float GetCloneDuration() => heroCloneAbility != null ? heroCloneAbility.Duration + cloneDurationBonus : 0f;
    public string GetCloneAbilityName() => heroCloneAbility != null ? heroCloneAbility.AbilityName : "Clon";
    public string GetCloneAbilityDescription() => heroCloneAbility != null ? heroCloneAbility.Description : string.Empty;
    public float GetCloneCooldown() => heroCloneAbility != null
        ? Mathf.Max(heroCloneAbility.MinimumCooldown, heroCloneAbility.Cooldown - cloneCooldownReduction) : 0f;
    public float GetCloneCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Aire, HeroCloneAbilityId);
    public bool CanActivateClone(bool bypassCooldown = false) => CanCombat && defenders != null && defenders[(int)ArmyType.Aire] != null &&
        heroCloneAbility != null && (bypassCooldown || GetCloneCooldownRemaining() <= 0f) && activeClone == null &&
        !cloneSpawnPending && HasLivingEnemies();
    public string GetKnightSummonName() => knightSummonAbility != null ? knightSummonAbility.AbilityName : "Invocar caballero";
    public string GetKnightSummonDescription() => knightSummonAbility != null ? knightSummonAbility.Description : string.Empty;
    public float GetKnightSummonCooldown() => knightSummonAbility == null ? 0f : Mathf.Max(knightSummonAbility.MinimumCooldown,
        knightSummonAbility.Cooldown - knightCooldownReductionLevel * knightSummonAbility.CooldownReductionPerLevel);
    public float GetKnightSummonCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Tierra, KnightSummonAbilityId);
    public bool CanActivateKnightSummon(bool bypassCooldown = false) => CanCombat && knightSummonAbility != null &&
        knightSummonAbility.KnightPrefab != null && defenders != null && defenders[(int)ArmyType.Tierra] != null &&
        (bypassCooldown || GetKnightSummonCooldownRemaining() <= 0f);
    public string GetWarBlessingName() => warBlessingAbility != null ? warBlessingAbility.AbilityName : "Acelerar aliado";
    public string GetWarBlessingDescription() => warBlessingAbility != null ? warBlessingAbility.Description : string.Empty;
    public float GetWarBlessingBonusPercent() => warBlessingAbility != null
        ? warBlessingAbility.BonusAttackSpeedPercent + warBlessingPowerLevel * warBlessingAbility.PowerUpgradePercent : 0f;
    public float GetWarBlessingDuration() => warBlessingAbility != null ? warBlessingAbility.Duration : 0f;
    public float GetWarBlessingCooldown() => warBlessingAbility == null ? 0f : Mathf.Max(warBlessingAbility.MinimumCooldown,
        warBlessingAbility.Cooldown - warBlessingCooldownReductionLevel * warBlessingAbility.CooldownReductionPerLevel);
    public float GetWarBlessingCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Tierra, WarBlessingAbilityId);
    public bool CanActivateWarBlessing(ArmyType target, bool bypassCooldown = false) => CanCombat && warBlessingAbility != null &&
        (target == ArmyType.Aire || target == ArmyType.Mar) && defenders != null && defenders[(int)target] != null &&
        defenders[(int)target].View != null && (bypassCooldown || GetWarBlessingCooldownRemaining() <= 0f);

    public string GetWaterTornadoName() => waterTornadoAbility != null ? waterTornadoAbility.AbilityName : "Huracán imparable";
    public string GetWaterTornadoDescription() => waterTornadoAbility != null ? waterTornadoAbility.Description : string.Empty;
    public float GetWaterTornadoCooldown() => waterTornadoAbility != null ? waterTornadoAbility.Cooldown : 0f;
    public float GetWaterTornadoCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Mar, WaterTornadoAbilityId);
    public float GetWaterTornadoDamage() => waterTornadoAbility != null
        ? ScaleSpecialDamage(waterTornadoAbility.Damage * (1f + tornadoDamageLevel * waterTornadoAbility.DamageUpgradePercent / 100f), ArmyType.Mar) : 0f;
    public float GetWaterTornadoPushDistance() => waterTornadoAbility != null
        ? Mathf.Sign(waterTornadoAbility.PushDistance) * (Mathf.Abs(waterTornadoAbility.PushDistance) + tornadoPushLevel * waterTornadoAbility.PushDistanceUpgrade) : 0f;
    public bool CanActivateWaterTornado(bool bypassCooldown = false) => CanCombat && waterTornadoAbility != null &&
        waterTornadoAbility.TornadoPrefab != null && HasLivingEnemies() &&
        (bypassCooldown || GetWaterTornadoCooldownRemaining() <= 0f);

    public string GetElectricBirdName() => electricBirdAbility != null ? electricBirdAbility.AbilityName : "Ave eléctrica";
    public string GetElectricBirdDescription() => electricBirdAbility != null ? electricBirdAbility.Description : string.Empty;
    public float GetElectricBirdDuration() => electricBirdAbility != null
        ? electricBirdAbility.DebuffDuration + electricBirdDurationLevel * electricBirdAbility.DurationUpgradeSeconds : 0f;
    public float GetElectricBirdCooldown() => electricBirdAbility == null ? 0f : Mathf.Max(electricBirdAbility.MinimumCooldown,
        electricBirdAbility.Cooldown - electricBirdCooldownLevel * electricBirdAbility.CooldownReductionSeconds);
    public float GetElectricBirdCooldownRemaining() => GetAdditionalCooldownRemaining(ArmyType.Mar, ElectricBirdAbilityId);
    public bool CanActivateElectricBird(bool bypassCooldown = false) => CanCombat && electricBirdAbility != null &&
        electricBirdAbility.ProjectilePrefab != null && defenders != null && defenders[(int)ArmyType.Mar] != null &&
        defenders[(int)ArmyType.Mar].AttackSpawn != null && SelectLightningTarget() != null &&
        (bypassCooldown || GetElectricBirdCooldownRemaining() <= 0f);

    public bool TryActivateSpecial(ArmyType army, string abilityId, out string message)
        => TryActivateSpecial(army, abilityId, out message, false);

    public bool TryActivateSpecial(ArmyType army, string abilityId, out string message, bool bypassCooldown)
        => TryActivateSpecial(army, abilityId, null, out message, bypassCooldown);

    public bool TryActivateSpecial(ArmyType army, string abilityId, string targetArmy, out string message, bool bypassCooldown)
    {
        message = string.Empty;
        if (!IsValidArmy(army)) { message = "Ejército desconocido."; return false; }
        if (!CanCombat) { message = "Las habilidades solo se pueden activar durante el combate."; return false; }
        if (defenders == null || defenders[(int)army] == null || defenders[(int)army].View == null)
        { message = "El héroe no está disponible."; return false; }

        if (army == ArmyType.Aire && abilityId == FireRageAbilityId)
        {
            if (fireRageAbility == null) { message = "Furia ígnea no está configurada."; return false; }
            if (!bypassCooldown && GetFireRageCooldownRemaining() > 0f) { message = "Furia ígnea aún está en cooldown."; return false; }
            Defender aire = defenders[(int)ArmyType.Aire];
            aire.Effects.Apply(new TimedCombatEffects.Snapshot
            {
                id = FireRageAbilityId,
                owner = ArmyType.Aire.ToString(),
                remaining = GetFireRageDuration(),
                damageMultiplier = 1f + GetFireRageDamageBonusPercent() / 100f,
                attackSpeedMultiplier = 1f,
                movementSpeedMultiplier = 1f,
                incomingDamageMultiplier = 1f,
                visualPrefab = fireRageAbility.BuffVisualPrefab,
                hasTint = true,
                tintMultiplier = fireRageAbility.BuffTint
            });
            StartAbilityCooldown(army, abilityId, fireRageAbility.Cooldown);
            PlayNewAbilityActivation(aire, fireRageAbility);
            message = "Furia ígnea activada.";
            return true;
        }

        if (army == ArmyType.Aire && abilityId == HeroCloneAbilityId)
        {
            if (heroCloneAbility == null) { message = "Clon no está configurado."; return false; }
            if (!bypassCooldown && GetCloneCooldownRemaining() > 0f) { message = "Clon aún está en cooldown."; return false; }
            if (activeClone != null || cloneSpawnPending) { message = "Ya hay un clon activo o preparándose."; return false; }
            if (!HasLivingEnemies()) { message = "No hay enemigos vivos para el clon."; return false; }
            StartAbilityCooldown(army, abilityId, GetCloneCooldown());
            PlayNewAbilityActivation(defenders[(int)ArmyType.Aire], heroCloneAbility);
            cloneSpawnPending = true;
            cloneSpawnRemaining = heroCloneAbility.SpawnDelay;
            if (cloneSpawnRemaining <= 0f)
            {
                cloneSpawnPending = false;
                if (!CreateHeroClone()) { message = "No se pudo crear el clon."; return false; }
            }
            message = "Clon preparándose.";
            StateRevision++;
            return true;
        }

        if (army == ArmyType.Tierra && abilityId == KnightSummonAbilityId)
        {
            if (knightSummonAbility == null || knightSummonAbility.KnightPrefab == null)
            { message = "Invocar caballero no está configurada correctamente."; return false; }
            if (!bypassCooldown && GetKnightSummonCooldownRemaining() > 0f)
            { message = "Invocar caballero aún está en cooldown."; return false; }
            Vector2 offset = knightSummonAbility.SpawnOffset;
            Transform hero = defenders[(int)ArmyType.Tierra].View.transform;
            Vector3 position = new Vector3(hero.position.x + offset.x, hero.position.y + offset.y, hero.position.z);
            GameObject view = Instantiate(knightSummonAbility.KnightPrefab, position, Quaternion.identity);
            view.name = "Caballero de Tierra";
            view.tag = "Untagged";
            KnightAlly ally = view.GetComponent<KnightAlly>();
            if (ally == null) ally = view.AddComponent<KnightAlly>();
            ally.Initialize(this, knightSummonAbility, knightUpgradeLevel);
            knightAllies.Add(ally);
            StartAbilityCooldown(army, abilityId, GetKnightSummonCooldown());
            PlayNewAbilityActivation(defenders[(int)ArmyType.Tierra], knightSummonAbility);
            message = "Caballero invocado.";
            StateRevision++;
            return true;
        }

        if (army == ArmyType.Tierra && abilityId == WarBlessingAbilityId)
        {
            bool parsedTarget = TryParseWarBlessingTarget(targetArmy, out ArmyType target);
            if (!parsedTarget || (target != ArmyType.Aire && target != ArmyType.Mar))
            { message = "Acelerar aliado solo puede dirigirse a Aire o Agua."; return false; }
            if (warBlessingAbility == null) { message = "Acelerar aliado no está configurada."; return false; }
            if (!bypassCooldown && GetWarBlessingCooldownRemaining() > 0f)
            { message = "Acelerar aliado aún está en cooldown."; return false; }
            Defender targetDefender = defenders[(int)target];
            if (targetDefender == null || targetDefender.View == null || targetDefender.Effects == null)
            { message = "El héroe objetivo no está disponible."; return false; }
            float bonus = GetWarBlessingBonusPercent();
            targetDefender.Effects.Apply(new TimedCombatEffects.Snapshot
            {
                id = WarBlessingAbilityId,
                owner = ArmyType.Tierra.ToString(),
                remaining = GetWarBlessingDuration(),
                damageMultiplier = 1f,
                attackSpeedMultiplier = 1f + bonus / 100f,
                movementSpeedMultiplier = 1f,
                incomingDamageMultiplier = 1f,
                hasTint = true,
                tintMultiplier = warBlessingAbility.BuffTint
            });
            StartAbilityCooldown(ArmyType.Tierra, abilityId, GetWarBlessingCooldown());
            PlayNewAbilityActivation(defenders[(int)ArmyType.Tierra], warBlessingAbility);
            message = "Acelerar aliado aplicado a " + target + ".";
            StateRevision++;
            return true;
        }

        if (army == ArmyType.Mar && abilityId == WaterTornadoAbilityId)
        {
            if (!CanActivateWaterTornado(bypassCooldown)) { message = "Huracán imparable no está disponible."; return false; }
            Vector2 start = waterTornadoAbility.InitialWorldPosition;
            GameObject tornado = Instantiate(waterTornadoAbility.TornadoPrefab,
                new Vector3(start.x, start.y, 0f), Quaternion.identity);
            tornado.name = "Huracán imparable";
            SpriteRenderer tornadoRenderer = tornado.GetComponentInChildren<SpriteRenderer>();
            if (tornadoRenderer != null) tornadoRenderer.sortingOrder = 16;
            tornadoProjectiles.Add(new TornadoProjectile { View = tornado });
            StartAbilityCooldown(ArmyType.Mar, abilityId, waterTornadoAbility.Cooldown);
            PlayNewAbilityActivation(defenders[(int)ArmyType.Mar], waterTornadoAbility);
            message = "Huracán imparable lanzado.";
            StateRevision++;
            return true;
        }

        if (army == ArmyType.Mar && abilityId == ElectricBirdAbilityId)
        {
            SkeletonEnemy target = SelectLightningTarget();
            if (!CanActivateElectricBird(bypassCooldown) || target == null)
            { message = "Ave eléctrica no tiene un objetivo válido."; return false; }
            Defender mar = defenders[(int)ArmyType.Mar];
            electricBirdProjectiles.Add(new ElectricBirdProjectile
            {
                Target = target,
                LaunchDelayRemaining = electricBirdAbility.LaunchDelay
            });
            StartAbilityCooldown(ArmyType.Mar, abilityId, GetElectricBirdCooldown());
            PlayNewAbilityActivation(mar, electricBirdAbility);
            message = "Ave eléctrica lanzada.";
            StateRevision++;
            return true;
        }

        if (abilityId != GetLegacySpecialAbilityId(army))
        { message = "Habilidad desconocida para este ejército."; return false; }
        return TryActivateLegacySpecial(army, out message, bypassCooldown);
    }

    public string GetSpecialAbilityName(ArmyType army)
    {
        HeroSpecialAbilityConfig config = GetSpecialAbilityConfig(army);
        return config != null && !string.IsNullOrWhiteSpace(config.AbilityName)
            ? config.AbilityName
            : "Habilidad especial";
    }

    public string GetSpecialAbilityDescription(ArmyType army) =>
        GetSpecialAbilityConfig(army) != null ? GetSpecialAbilityConfig(army).Description : string.Empty;

    public int GetSpecialAbilityEffectCount(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Aire: return meteorShowerAbility != null ? meteorShowerAbility.MeteorCount + meteorCountBonus[(int)army] : 0;
            case ArmyType.Mar: return lightningStormAbility != null ? lightningStormAbility.HitCount + lightningHitCountBonus[(int)army] : 0;
            default: return 0;
        }
    }

    public float GetSpecialAbilityDamagePerEffect(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Aire:
                return meteorShowerAbility != null ? ScaleSpecialDamage(meteorShowerAbility.Damage + meteorDamageBonus[(int)army], army) : 0f;
            case ArmyType.Mar:
                return lightningStormAbility != null ? ScaleSpecialDamage(lightningStormAbility.DamagePerHit + lightningDamageBonus[(int)army], army) : 0f;
            default: return 0f;
        }
    }

    public float GetSpecialAbilityNormalDuration(ArmyType army) =>
        army == ArmyType.Tierra && zombieGraspAbility != null ? zombieGraspAbility.NormalEnemyDuration + graspNormalDurationBonus[(int)army] : 0f;

    public float GetSpecialAbilityBossDuration(ArmyType army) =>
        army == ArmyType.Tierra && zombieGraspAbility != null
            ? Mathf.Min(GetSpecialAbilityNormalDuration(army), zombieGraspAbility.BossDuration + graspBossDurationBonus[(int)army])
            : 0f;

    public bool TryActivateSpecial(ArmyType army, out string message)
        => TryActivateSpecial(army, GetLegacySpecialAbilityId(army), out message);

    public bool TryActivateSpecial(ArmyType army, out string message, bool bypassCooldown)
        => TryActivateSpecial(army, GetLegacySpecialAbilityId(army), out message, bypassCooldown);

    private bool TryActivateLegacySpecial(ArmyType army, out string message, bool bypassCooldown)
    {
        message = string.Empty;
        if (!IsValidArmy(army))
        {
            message = "Ejército desconocido.";
            return false;
        }
        if (GetSpecialAbilityConfig(army) == null)
        {
            message = "La habilidad especial no está configurada.";
            return false;
        }
        if (!CanCombat)
        {
            message = "Las habilidades solo se pueden activar durante el combate.";
            return false;
        }
        float remaining = GetSpecialAbilityCooldownRemaining(army);
        if (!bypassCooldown && remaining > 0f)
        {
            message = "Disponible en " + Mathf.CeilToInt(remaining) + " s.";
            return false;
        }
        if (!HasLivingEnemies())
        {
            message = "No hay enemigos vivos.";
            return false;
        }

        Defender defender = defenders != null ? defenders[(int)army] : null;
        if (defender == null || defender.View == null)
        {
            message = "El héroe no está disponible.";
            return false;
        }

        SkeletonEnemy lightningTarget = army == ArmyType.Mar ? SelectLightningTarget() : null;
        if (army == ArmyType.Mar && lightningTarget == null)
        {
            message = "No hay enemigos vivos.";
            return false;
        }

        HeroSpecialAbilityConfig abilityConfig = GetSpecialAbilityConfig(army);
        GameSoundManager.Instance?.PlayAbilityEffect(abilityConfig.ActivationSound, abilityConfig.ActivationSoundPlayCount);
        PlaySpecialAttackAnimation(defender);
        specialCooldownEndsAt[(int)army] = Time.time + GetSpecialAbilityCooldown(army);
        specialCooldownsWereRunning = true;
        StateRevision++;

        switch (army)
        {
            case ArmyType.Tierra:
                ActivateZombieGrasp();
                message = "Zombie Grasp activado.";
                break;
            case ArmyType.Aire:
                StartCoroutine(PerformMeteorShower());
                message = "Meteor Shower activado.";
                break;
            case ArmyType.Mar:
                StartCoroutine(PerformLightningStorm(lightningTarget));
                message = "Lightning Storm activado.";
                break;
        }
        return true;
    }

    private static string GetLegacySpecialAbilityId(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Tierra: return "zombie_grasp";
            case ArmyType.Aire: return "meteor_shower";
            case ArmyType.Mar: return "lightning_storm";
            default: return string.Empty;
        }
    }

    private static bool TryParseWarBlessingTarget(string value, out ArmyType target)
    {
        if (string.Equals(value, "agua", StringComparison.OrdinalIgnoreCase))
        { target = ArmyType.Mar; return true; }
        return Enum.TryParse(value, true, out target);
    }

    private string GetCooldownKey(ArmyType army, string abilityId) => ((int)army) + ":" + abilityId;
    private float GetAdditionalCooldownRemaining(ArmyType army, string abilityId) =>
        additionalCooldownEndsAt.TryGetValue(GetCooldownKey(army, abilityId), out float endAt) ? Mathf.Max(0f, endAt - Time.time) : 0f;
    private void StartAbilityCooldown(ArmyType army, string abilityId, float duration)
    {
        additionalCooldownEndsAt[GetCooldownKey(army, abilityId)] = Time.time + Mathf.Max(0f, duration);
        specialCooldownsWereRunning = true;
        StateRevision++;
    }

    private void PlayNewAbilityActivation(Defender defender, HeroSpecialAbilityConfig config)
    {
        GameSoundManager.Instance?.PlayAbilityEffect(config.ActivationSound, config.ActivationSoundPlayCount);
        PlaySpecialAttackAnimation(defender);
        StateRevision++;
    }

    private bool CreateHeroClone()
    {
        Defender original = defenders[(int)ArmyType.Aire];
        HeroConfig config = original.Config;
        if (original.View == null || config == null || heroCloneAbility == null) return false;
        Vector3 position = original.View.transform.position + Vector3.left * heroCloneAbility.HorizontalSeparation;
        GameObject view = Instantiate(original.View, position, original.View.transform.rotation, original.View.transform.parent);
        view.name = "Clon temporal - " + config.DisplayName;
        view.transform.localScale = original.View.transform.localScale;
        Animator animator = view.GetComponent<Animator>();
        if (animator == null) animator = view.GetComponentInChildren<Animator>();
        TimedCombatEffects effects = view.GetComponent<TimedCombatEffects>();
        if (effects == null) effects = view.AddComponent<TimedCombatEffects>();
        effects.ClearAll();
        RemoveDuplicatedEffectVisuals(view.transform);

        foreach (SpriteOutlineUvBounds outline in view.GetComponentsInChildren<SpriteOutlineUvBounds>(true))
        {
            SpriteRenderer outlineRenderer = outline.GetComponent<SpriteRenderer>();
            if (outlineRenderer != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                outlineRenderer.GetPropertyBlock(block);
                block.SetFloat(Shader.PropertyToID("_OutlinePixels"), 0f);
                block.SetFloat(Shader.PropertyToID("_OutlineLocalWidth"), 0f);
                outlineRenderer.SetPropertyBlock(block);
            }
            outline.enabled = false;
            Destroy(outline);
        }

        foreach (MonoBehaviour behaviour in view.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is TimedCombatEffects)) behaviour.enabled = false;
        foreach (Collider2D collider in view.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (Collider collider in view.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (SpriteRenderer renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.color = new Color(heroCloneAbility.Tint.r, heroCloneAbility.Tint.g, heroCloneAbility.Tint.b,
                heroCloneAbility.TintAlpha);
            renderer.sortingOrder += 1;
        }
        // This is deliberately a one-time snapshot: later buffs on the real hero do not propagate to this clone.
        // Copy after the clone tint is established so temporary tint effects restore to that clone-specific color.
        original.Effects?.CopyActiveTo(effects);
        Transform attackSpawn = view.transform.Find("AttackSpawn");
        activeClone = new CloneRuntime
        {
            View = view,
            Animator = animator,
            Effects = effects,
            AttackSpawn = attackSpawn,
            Remaining = GetCloneDuration(),
            AttackTimer = 0f
        };
        StateRevision++;
        return true;
    }

    private void UpdatePendingCloneSpawn()
    {
        if (!cloneSpawnPending) return;
        cloneSpawnRemaining -= Time.deltaTime;
        if (cloneSpawnRemaining > 0f) return;
        cloneSpawnPending = false;
        cloneSpawnRemaining = 0f;
        if (!CanCombat || activeClone != null || defenders == null || defenders[(int)ArmyType.Aire] == null)
        {
            StateRevision++;
            return;
        }
        if (!CreateHeroClone())
            Debug.LogWarning("No se pudo crear el clon al finalizar su retraso de aparición.", this);
        StateRevision++;
    }

    private static void RemoveDuplicatedEffectVisuals(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("Effect - ", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            else RemoveDuplicatedEffectVisuals(child);
        }
    }

    private void UpdateClone()
    {
        CloneRuntime clone = activeClone;
        if (clone == null) return;
        if (!CanCombat || clone.View == null) { RemoveActiveClone(); return; }
        clone.Remaining -= Time.deltaTime;
        if (clone.Remaining <= 0f) { RemoveActiveClone(); return; }

        if (clone.ShotPending)
        {
            clone.PendingShotTimer -= Time.deltaTime;
            if (clone.PendingShotTimer > 0f) return;
            clone.ShotPending = false;
            SkeletonEnemy pendingTarget = FindTarget(clone.View.transform.position, GetHeroRange(ArmyType.Aire));
            if (pendingTarget != null)
                FireProjectile(clone.View, aireConfig.DisplayName + " · Clon", aireConfig, clone.PendingDamage,
                    GetHeroSplashRadius(ArmyType.Aire), pendingTarget.transform.position, pendingTarget,
                    aireConfig.ProjectileAnimatorController, GetCloneAttackSpawnPosition(clone), DamageCategory.Clone);
        }

        clone.AttackTimer -= Time.deltaTime;
        if (clone.AttackTimer > 0f) return;
        SkeletonEnemy target = FindTarget(clone.View.transform.position, GetHeroRange(ArmyType.Aire));
        if (target == null) return;
        clone.AttackTimer = GetHeroAttackCooldownWithEffects(ArmyType.Aire, clone.Effects);
        clone.PendingDamage = GetHeroDamageWithEffects(ArmyType.Aire, clone.Effects);
        if (clone.Animator != null && clone.Animator.runtimeAnimatorController != null &&
            clone.Animator.HasState(0, Animator.StringToHash(NormalAttackState)))
            clone.Animator.Play(NormalAttackState, 0, 0f);
        clone.ShotPending = true;
        clone.PendingShotTimer = aireConfig.ProjectileDelay;
    }

    private static Vector3 GetCloneAttackSpawnPosition(CloneRuntime clone) => clone.AttackSpawn != null
        ? clone.AttackSpawn.position : clone.View.transform.position + Vector3.right * 0.2f;

    private void TickCombatEffects(float deltaTime)
    {
        if (!CanCombat || deltaTime <= 0f) return;
        if (defenders != null)
            foreach (Defender defender in defenders) defender?.Effects?.Tick(deltaTime);
        foreach (SkeletonEnemy enemy in enemies) enemy?.Effects?.Tick(deltaTime);
        activeClone?.Effects?.Tick(deltaTime);
    }

    private void RemoveActiveClone()
    {
        if (activeClone == null) return;
        if (activeClone.Effects != null) activeClone.Effects.ClearAll();
        GameObject cloneView = activeClone.View;
        activeClone = null;
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            if (projectiles[i].Category != DamageCategory.Clone) continue;
            if (projectiles[i].View != null) Destroy(projectiles[i].View);
            projectiles.RemoveAt(i);
        }
        if (cloneView != null) Destroy(cloneView);
        StateRevision++;
    }

    private float ApplyHeroAttackSpeedToCooldown(float baseCooldown, ArmyType army)
    {
        if (baseCooldown <= 0f)
            return baseCooldown;
        float attacksPerSecond = (1f / baseCooldown) * GetHeroAttackSpeedMultiplier(army) +
                                 GetHeroAttackRateFixedBonus(army);
        return attacksPerSecond > 0f ? 1f / attacksPerSecond : baseCooldown;
    }

    private float GetBaseSpecialCooldown(ArmyType army)
    {
        HeroSpecialAbilityConfig config = GetSpecialAbilityConfig(army);
        return config != null ? config.Cooldown : 0f;
    }

    private HeroSpecialAbilityConfig GetSpecialAbilityConfig(ArmyType army)
    {
        switch (army)
        {
            case ArmyType.Tierra: return zombieGraspAbility;
            case ArmyType.Aire: return meteorShowerAbility;
            case ArmyType.Mar: return lightningStormAbility;
            default: return null;
        }
    }

    private bool HasLivingEnemies()
    {
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
                return true;
        }
        return false;
    }

    private SkeletonEnemy SelectLightningTarget()
    {
        SkeletonEnemy firstBoss = null;
        SkeletonEnemy highestHealthEnemy = null;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            if (enemy.IsBoss)
            {
                if (firstBoss == null)
                    firstBoss = enemy;
            }
            else if (highestHealthEnemy == null || enemy.CurrentHealth > highestHealthEnemy.CurrentHealth)
            {
                highestHealthEnemy = enemy;
            }
        }
        return firstBoss != null ? firstBoss : highestHealthEnemy;
    }

    private void PlaySpecialAttackAnimation(Defender defender)
    {
        if (defender.Animator == null || defender.Animator.runtimeAnimatorController == null)
            return;
        int specialState = Animator.StringToHash("Attack_Special");
        if (!defender.Animator.HasState(0, specialState))
        {
            if (!defender.SpecialAnimationWarningLogged)
            {
                Debug.LogWarning("El controlador de " + defender.Config.DisplayName +
                    " no contiene Attack_Special; añade ese estado al Animator para reproducir la animación.", defender.View);
                defender.SpecialAnimationWarningLogged = true;
            }
            return;
        }
        defender.Animator.Play(specialState, 0, 0f);
        defender.SpecialAnimationActive = true;
        defender.SpecialAnimationStartedAt = Time.time;
    }

    private void UpdateSpecialAnimations()
    {
        if (defenders == null)
            return;
        int specialState = Animator.StringToHash("Attack_Special");
        int idleState = Animator.StringToHash("Idle");
        foreach (Defender defender in defenders)
        {
            if (defender == null || !defender.SpecialAnimationActive)
                continue;
            if (defender.Animator == null)
            {
                defender.SpecialAnimationActive = false;
                continue;
            }
            AnimatorStateInfo state = defender.Animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != specialState && Time.time - defender.SpecialAnimationStartedAt < 0.1f)
                continue;
            if (state.shortNameHash == specialState && state.normalizedTime < 1f)
                continue;

            if (state.shortNameHash == specialState && defender.Animator.HasState(0, idleState))
                defender.Animator.Play(idleState, 0, 0f);
            defender.SpecialAnimationActive = false;
        }
    }

    private void CleanupSpecialEffects()
    {
        for (int i = specialEffects.Count - 1; i >= 0; i--)
        {
            if (specialEffects[i] == null)
                specialEffects.RemoveAt(i);
        }
    }

    private void UpdateSpecialCooldownState()
    {
        bool hasRunningCooldown = HasActiveSpecialCooldowns();
        bool cooldownDisplayNeedsRefresh = hasRunningCooldown && Time.time >= nextCooldownStateRefreshAt;
        if (hasRunningCooldown != specialCooldownsWereRunning || cooldownDisplayNeedsRefresh)
        {
            specialCooldownsWereRunning = hasRunningCooldown;
            nextCooldownStateRefreshAt = Time.time + 1f;
            StateRevision++;
        }
    }

    private bool HasActiveSpecialCooldowns()
    {
        for (int i = 0; i < specialCooldownEndsAt.Length; i++)
        {
            if (specialCooldownEndsAt[i] > Time.time)
                return true;
        }
        foreach (float endAt in additionalCooldownEndsAt.Values)
            if (endAt > Time.time) return true;
        return false;
    }

    private void ActivateZombieGrasp()
    {
        ZombieGraspAbilityConfig ability = zombieGraspAbility;
        if (ability == null || ability.EffectController == null)
        {
            if (!warnedMissingZombieGraspController)
                Debug.LogWarning("Asigna el asset Zombie Grasp y su controlador de efecto; la inmovilización se aplicará sin efecto visual.", this);
            warnedMissingZombieGraspController = true;
        }
        float normalDuration = ability != null ? Mathf.Max(0f, ability.NormalEnemyDuration + graspNormalDurationBonus[(int)ArmyType.Tierra]) : 0f;
        float bossDuration = ability != null ? Mathf.Min(normalDuration, Mathf.Max(0f, ability.BossDuration + graspBossDurationBonus[(int)ArmyType.Tierra])) : 0f;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            enemy.ApplyRooted(enemy.IsBoss ? bossDuration : normalDuration);
            float rootDuration = enemy.IsBoss ? bossDuration : normalDuration;
            float holdAt = Mathf.Min(0.5f, rootDuration * 0.4f);
            float clipLength = GetEffectClipLength(ability != null ? ability.EffectController : null);
            GameObject graspEffect = CreateAnimatedEffect(ability != null ? ability.EffectController : null,
                enemy.transform.position, ability != null ? ability.EffectScale : 1f,
                null, rootDuration + clipLength - holdAt + 0.1f, false);
            if (graspEffect != null)
                StartCoroutine(HoldZombieGraspEffect(graspEffect, rootDuration, holdAt, clipLength));
        }
    }

    private IEnumerator PerformMeteorShower()
    {
        MeteorShowerAbilityConfig ability = meteorShowerAbility;
        if (ability == null || ability.EffectController == null)
        {
            if (!warnedMissingMeteorController)
                Debug.LogWarning("Asigna el asset Meteor Shower y su controlador de efecto; los impactos se aplicarán sin efecto visual.", this);
            warnedMissingMeteorController = true;
        }
        int meteorCount = ability != null ? ability.MeteorCount + meteorCountBonus[(int)ArmyType.Aire] : 0;
        List<Vector3> strikePositions = new List<Vector3>(Mathf.Max(1, meteorCount));
        int count = Mathf.Max(1, meteorCount);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetMeteorStrikePosition(strikePositions, ability);
            strikePositions.Add(position);
            CreateAnimatedEffect(ability != null ? ability.EffectController : null, position,
                ability != null ? ability.EffectScale : 1f, null);
            StartCoroutine(ResolveMeteorImpact(position, ability));
            if (i < count - 1)
                yield return new WaitForSeconds(ability != null ? ability.Interval : 0f);
        }
    }

    private IEnumerator ResolveMeteorImpact(Vector3 position, MeteorShowerAbilityConfig ability)
    {
        if (ability != null && ability.ImpactDelay > 0f)
            yield return new WaitForSeconds(ability.ImpactDelay);
        if (!CanCombat)
            yield break;

        float damage = ScaleSpecialDamage(ability != null ? ability.Damage + meteorDamageBonus[(int)ArmyType.Aire] : 0f, ArmyType.Aire);
        float radius = ability != null ? Mathf.Max(0f, ability.Radius) : 0f;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || Vector2.Distance(position, enemy.transform.position) > radius)
                continue;
            ApplyDamage(enemy, damage, aireConfig != null ? aireConfig.ProjectileColor : Color.red,
                ArmyType.Aire, DamageCategory.SpecialAbility);
        }
    }

    private Vector3 GetMeteorStrikePosition(List<Vector3> previousPositions, MeteorShowerAbilityConfig ability)
    {
        float left;
        float right;
        float minY;
        float maxY;
        SpriteRenderer area = GetMeteorShowerAreaRenderer();
        if (area != null && area.sprite != null)
        {
            Bounds bounds = area.bounds;
            left = bounds.min.x;
            right = bounds.max.x;
            minY = bounds.min.y;
            maxY = bounds.max.y;
        }
        else
        {
            float padding = ability != null ? ability.HorizontalPadding : 0f;
            float maximumX = ability != null ? ability.MaximumX : float.MaxValue;
            left = Mathf.Min(enemySpawner.position.x, castleTarget.position.x) + padding;
            right = Mathf.Min(Mathf.Max(enemySpawner.position.x, castleTarget.position.x) - padding,
                maximumX);
            if (right < left)
                left = right = Mathf.Min((enemySpawner.position.x + castleTarget.position.x) * 0.5f, maximumX);

            float centerY = ability != null ? ability.CenterY : 0f;
            float verticalHalfRange = ability != null ? ability.VerticalHalfRange : 0f;
            minY = centerY - verticalHalfRange;
            maxY = centerY + verticalHalfRange;
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.orthographic)
            {
                minY = Mathf.Max(minY, mainCamera.transform.position.y - mainCamera.orthographicSize + 0.2f);
                maxY = Mathf.Min(maxY, mainCamera.transform.position.y + mainCamera.orthographicSize - 0.2f);
            }
            if (maxY < minY)
                minY = maxY = centerY;
        }

        Vector3 position = default;
        float minimumSeparation = ability != null ? ability.MinimumSeparation : 0f;
        int attempts = Mathf.Max(8, previousPositions.Count * 3);
        for (int i = 0; i < attempts; i++)
        {
            position = new Vector3(UnityEngine.Random.Range(left, right), UnityEngine.Random.Range(minY, maxY),
                (enemySpawner.position.z + castleTarget.position.z) * 0.5f);
            bool separated = true;
            foreach (Vector3 previous in previousPositions)
            {
                if (Vector2.Distance(position, previous) < minimumSeparation)
                {
                    separated = false;
                    break;
                }
            }
            if (separated)
                return position;
        }
        return position;
    }

    private SpriteRenderer GetMeteorShowerAreaRenderer()
    {
        if (meteorShowerArea == null)
        {
            GameObject area = GameObject.Find("Area_MeteorShower");
            if (area != null)
                meteorShowerArea = area.GetComponent<SpriteRenderer>();
        }
        return meteorShowerArea;
    }

    private IEnumerator PerformLightningStorm(SkeletonEnemy target)
    {
        LightningStormAbilityConfig ability = lightningStormAbility;
        if (ability == null || ability.EffectController == null)
        {
            if (!warnedMissingLightningController)
                Debug.LogWarning("Asigna el asset Lightning Storm y su controlador de efecto; los impactos se aplicarán sin efecto visual.", this);
            warnedMissingLightningController = true;
        }
        int hitCount = Mathf.Max(1, ability != null ? ability.HitCount + lightningHitCountBonus[(int)ArmyType.Mar] : 0);
        for (int i = 0; i < hitCount; i++)
        {
            if (target == null || target.IsDead)
                yield break;
            Vector3 impactPosition = target.transform.position;
            CreateAnimatedEffect(ability != null ? ability.EffectController : null, impactPosition,
                ability != null ? ability.EffectScale : 1f, null);
            if (ability != null && ability.ImpactDelay > 0f)
                yield return new WaitForSeconds(ability.ImpactDelay);
            if (target == null || target.IsDead)
                yield break;
            ApplyDamage(target, ScaleSpecialDamage(ability != null ? ability.DamagePerHit + lightningDamageBonus[(int)ArmyType.Mar] : 0f, ArmyType.Mar),
                marConfig != null ? marConfig.ProjectileColor : Color.cyan,
                ArmyType.Mar, DamageCategory.SpecialAbility);
            if (i < hitCount - 1)
                yield return new WaitForSeconds(ability != null ? ability.HitInterval : 0f);
        }
    }

    private float ScaleSpecialDamage(float baseDamage, ArmyType army) =>
        baseDamage * GetHeroUpgradeMultiplier(army) + GetHeroDamageFixedBonus(army);

    private IEnumerator HoldZombieGraspEffect(GameObject effect, float duration, float holdAt, float clipLength)
    {
        if (holdAt > 0f)
            yield return new WaitForSeconds(holdAt);
        if (effect == null)
            yield break;

        Animator animator = effect.GetComponent<Animator>();
        if (animator != null)
            animator.speed = 0f;
        float remaining = Mathf.Max(0f, duration - holdAt);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
        if (effect == null)
            yield break;

        if (animator != null)
            animator.speed = 1f;
        float animationRemaining = Mathf.Max(0f, clipLength - holdAt);
        if (animationRemaining > 0f)
            yield return new WaitForSeconds(animationRemaining);
        if (effect != null)
            Destroy(effect);
    }

    private GameObject CreateAnimatedEffect(RuntimeAnimatorController controller, Vector3 position,
        float scale, Transform parent, float lifetimeOverride = -1f, bool destroyOnAnimationEvent = true)
    {
        if (controller == null)
            return null;
        GameObject effect = new GameObject("Special Effect - " + controller.name);
        effect.transform.position = position;
        effect.transform.localScale = Vector3.one * scale;
        if (parent != null)
            effect.transform.SetParent(parent, true);

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 20;
        Effects_DestroyEvent destroyEvent = effect.AddComponent<Effects_DestroyEvent>();
        destroyEvent.DestroyOnEvent = destroyOnAnimationEvent;
        Animator animator = effect.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        specialEffects.Add(effect);

        float lifetime = GetEffectClipLength(controller);
        Destroy(effect, lifetimeOverride >= 0f ? lifetimeOverride : lifetime + 0.05f);
        return effect;
    }

    private static float GetEffectClipLength(RuntimeAnimatorController controller)
    {
        if (controller != null)
        {
            AnimationClip[] clips = controller.animationClips;
            if (clips != null && clips.Length > 0 && clips[0] != null && clips[0].length > 0f)
                return clips[0].length;
        }
        return 1.25f;
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
        // Refresh client-side ability availability when enemies first enter the map.
        StateRevision++;
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

            if (enemy.Tick(Time.deltaTime, castleTarget.position, FindKnightTargetForEnemy(enemy.transform.position)))
            {
                Destroy(enemy.gameObject);
                enemies.RemoveAt(i);
            }
        }
    }

    private KnightAlly FindKnightTargetForEnemy(Vector3 enemyPosition)
    {
        KnightAlly best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (KnightAlly ally in knightAllies)
        {
            if (ally == null || ally.IsDead) continue;
            float distance = Vector2.Distance(enemyPosition, ally.transform.position);
            if (distance <= ally.EnemyEngagementRange && distance < bestDistance)
            { best = ally; bestDistance = distance; }
        }
        return best;
    }

    private void UpdateKnightAllies()
    {
        float advanceTargetX = enemySpawner != null ? enemySpawner.position.x : castleTarget.position.x;
        SkeletonEnemy[] targets = enemies.ToArray();
        for (int i = knightAllies.Count - 1; i >= 0; i--)
        {
            KnightAlly ally = knightAllies[i];
            if (ally == null)
            { knightAllies.RemoveAt(i); continue; }
            if (ally.Tick(Time.deltaTime, targets, advanceTargetX))
            {
                Destroy(ally.gameObject);
                knightAllies.RemoveAt(i);
            }
        }
    }

    private void ClearKnightAllies()
    {
        foreach (KnightAlly ally in knightAllies)
            if (ally != null) Destroy(ally.gameObject);
        knightAllies.Clear();
    }

    private void ClearWaterProjectiles()
    {
        foreach (TornadoProjectile projectile in tornadoProjectiles)
            if (projectile != null && projectile.View != null) Destroy(projectile.View);
        tornadoProjectiles.Clear();
        foreach (ElectricBirdProjectile projectile in electricBirdProjectiles)
            if (projectile != null && projectile.View != null) Destroy(projectile.View);
        electricBirdProjectiles.Clear();
    }

    internal void ApplyKnightDamage(SkeletonEnemy enemy, float damage, KnightAlly source)
    {
        if (!CanCombat || source == null || source.IsDead || enemy == null || enemy.IsDead) return;
        ApplyDamage(enemy, damage, new Color(0.95f, 0.82f, 0.48f), ArmyType.Tierra, DamageCategory.SpecialAbility);
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
                    defender.ShotPending = false;
                    if (IsValidPendingHeroShot(defender))
                    {
                        SkeletonEnemy currentTarget = FindTarget(defender);
                        if (currentTarget != null)
                            FireProjectile(defender, currentTarget);
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
            if (!defender.SpecialAnimationActive && defender.Animator != null && defender.Animator.runtimeAnimatorController != null)
                defender.Animator.Play(NormalAttackState, 0, 0f);
            defender.ShotPending = true;
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
        Vector3? launchPosition = null, DamageCategory damageCategory = DamageCategory.Hero)
    {
        if (target == null || target.IsDead)
            return;

        FireProjectile(source, sourceName, config, damage, splashRadius, target.transform.position, target,
            animatorController, launchPosition, damageCategory);
    }

    private void FireProjectile(GameObject source, string sourceName, HeroConfig config, float damage,
        float splashRadius, Vector3 destination, SkeletonEnemy target, RuntimeAnimatorController animatorController = null,
        Vector3? launchPosition = null, DamageCategory damageCategory = DamageCategory.Hero)
    {
        if (source == null || config == null)
            return;

        GameSoundManager.Instance?.PlayAttack(config.Army);

        Vector3 origin = launchPosition ?? source.transform.position + Vector3.right * 0.2f;
        GameObject view = new GameObject("Proyectil - " + sourceName);
        view.transform.position = origin;
        float projectileScale = animatorController != null ? config.ProjectileSize : 0.14f;
        if (damageCategory == DamageCategory.Tower)
            projectileScale *= 0.8f;
        view.transform.localScale = Vector3.one * projectileScale;
        SetProjectileVisualDirection(view.transform, destination - origin);

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 15;
        bool towerProjectile = damageCategory == DamageCategory.Tower;
        if (animatorController != null && !towerProjectile)
        {
            Animator animator = view.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
        }
        else if (towerProjectile)
        {
            renderer.sprite = GetTowerProjectileSprite();
            renderer.color = config.ProjectileColor;
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
            Army = config.Army,
            Category = damageCategory,
            Speed = config.ProjectileSpeed,
            RemainingLifetime = config.ProjectileLifetime
        });
    }

    private Sprite GetTowerProjectileSprite()
    {
        if (towerProjectileSprite != null) return towerProjectileSprite;
        const int size = 32;
        towerProjectileTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tower Projectile Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            float alpha = Mathf.Clamp01(radius + 0.75f - distance);
            towerProjectileTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        towerProjectileTexture.Apply();
        towerProjectileSprite = Sprite.Create(towerProjectileTexture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
        return towerProjectileSprite;
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

    private void UpdateWaterProjectiles(float deltaTime)
    {
        Bounds worldBounds = GetWaterWorldBounds();
        for (int i = tornadoProjectiles.Count - 1; i >= 0; i--)
        {
            TornadoProjectile tornado = tornadoProjectiles[i];
            if (tornado == null || tornado.View == null)
            { tornadoProjectiles.RemoveAt(i); continue; }

            Transform view = tornado.View.transform;
            Vector3 start = view.position;
            view.position += Vector3.left * waterTornadoAbility.TravelSpeed * deltaTime;
            Vector3 end = view.position;
            float radius = waterTornadoAbility.CollisionRadius;
            foreach (SkeletonEnemy enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || tornado.HitEnemies.Contains(enemy)) continue;
                if (DistanceToSegment(enemy.transform.position, start, end) > radius + GetEnemyHitRadius(enemy)) continue;
                tornado.HitEnemies.Add(enemy);
                ApplyDamage(enemy, GetWaterTornadoDamage(), marConfig != null ? marConfig.ProjectileColor : Color.cyan,
                    ArmyType.Mar, DamageCategory.SpecialAbility);
                float push = GetWaterTornadoPushDistance() * (enemy.IsBoss ? waterTornadoAbility.BossPushMultiplier : 1f);
                enemy.TryApplyKnockback(push, waterTornadoAbility.PushSpeed,
                    worldBounds.min.x + GetEnemyHitRadius(enemy), worldBounds.max.x - GetEnemyHitRadius(enemy));
            }

            if (view.position.x + radius < worldBounds.min.x)
            {
                Destroy(tornado.View);
                tornadoProjectiles.RemoveAt(i);
            }
        }

        for (int i = electricBirdProjectiles.Count - 1; i >= 0; i--)
        {
            ElectricBirdProjectile bird = electricBirdProjectiles[i];
            if (bird == null)
            {
                electricBirdProjectiles.RemoveAt(i);
                continue;
            }
            if (!bird.Launched)
            {
                bird.LaunchDelayRemaining -= deltaTime;
                if (bird.LaunchDelayRemaining > 0f) continue;
                if (!CanCombat || defenders == null || defenders[(int)ArmyType.Mar] == null ||
                    defenders[(int)ArmyType.Mar].AttackSpawn == null)
                {
                    electricBirdProjectiles.RemoveAt(i);
                    continue;
                }
                bird.Target = bird.Target != null && !bird.Target.IsDead ? bird.Target : SelectLightningTarget();
                if (bird.Target == null) { electricBirdProjectiles.RemoveAt(i); continue; }
                Vector3 origin = defenders[(int)ArmyType.Mar].AttackSpawn.position;
                bird.View = Instantiate(electricBirdAbility.ProjectilePrefab, origin, Quaternion.identity);
                bird.View.name = "Ave eléctrica";
                SpriteRenderer visual = bird.View.GetComponentInChildren<SpriteRenderer>();
                if (visual != null) { visual.flipX = true; visual.sortingOrder = 17; }
                Animator flightAnimator = bird.View.GetComponentInChildren<Animator>();
                int movingState = Animator.StringToHash("Moving");
                if (flightAnimator != null && flightAnimator.HasState(0, movingState))
                    flightAnimator.Play(movingState, 0, 0f);
                bird.RemainingLifetime = Mathf.Max(1f, Vector2.Distance(origin, bird.Target.transform.position) / electricBirdAbility.ProjectileSpeed + 2f);
                bird.Launched = true;
            }
            if (bird.Impacted)
            {
                bird.ImpactRemaining -= deltaTime;
                if (bird.ImpactRemaining <= 0f)
                { Destroy(bird.View); electricBirdProjectiles.RemoveAt(i); }
                continue;
            }
            bird.RemainingLifetime -= deltaTime;
            if (bird.Target == null || bird.Target.IsDead)
                bird.Target = SelectLightningTarget();
            if (bird.Target == null || bird.RemainingLifetime <= 0f)
            { Destroy(bird.View); electricBirdProjectiles.RemoveAt(i); continue; }

            Transform view = bird.View.transform;
            Vector3 start = view.position;
            Vector3 destination = bird.Target.transform.position;
            view.position = Vector3.MoveTowards(start, destination, electricBirdAbility.ProjectileSpeed * deltaTime);
            if (DistanceToSegment(bird.Target.transform.position, start, view.position) <=
                electricBirdAbility.HitRadius + GetEnemyHitRadius(bird.Target))
            {
                bird.Target.Effects.Apply(new TimedCombatEffects.Snapshot
                {
                    id = ElectricBirdAbilityId,
                    owner = ArmyType.Mar.ToString(),
                    remaining = GetElectricBirdDuration(),
                    damageMultiplier = 1f,
                    attackSpeedMultiplier = 1f,
                    movementSpeedMultiplier = 1f - electricBirdAbility.SlowPercent / 100f,
                    incomingDamageMultiplier = electricBirdAbility.VulnerabilityMultiplier,
                    hasTint = true,
                    tintMultiplier = electricBirdAbility.DebuffTint
                });
                bird.Impacted = true;
                Animator animator = bird.View.GetComponentInChildren<Animator>();
                int hitState = Animator.StringToHash("ElectricBird_Hit");
                bird.ImpactRemaining = GetAnimatorClipLength(animator, "ElectricBird_Hit", 0.2f);
                if (animator != null && animator.HasState(0, hitState)) animator.Play(hitState, 0, 0f);
                StateRevision++;
            }
        }
    }

    private Bounds GetWaterWorldBounds()
    {
        if (gameCamera != null && gameCamera.orthographic)
        {
            float halfHeight = gameCamera.orthographicSize;
            float halfWidth = halfHeight * gameCamera.aspect;
            Vector3 center = gameCamera.transform.position;
            return new Bounds(center, new Vector3(halfWidth * 2f, halfHeight * 2f, 100f));
        }
        float left = castleTarget != null ? castleTarget.position.x - 2f : -10f;
        float right = enemySpawner != null ? enemySpawner.position.x + 2f : 10f;
        return new Bounds(new Vector3((left + right) * 0.5f, 0f, 0f), new Vector3(right - left, 20f, 100f));
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector2 segment = (Vector2)(end - start);
        float length = segment.sqrMagnitude;
        float t = length > 0f ? Mathf.Clamp01(Vector2.Dot((Vector2)(point - start), segment) / length) : 0f;
        return Vector2.Distance(point, (Vector2)start + segment * t);
    }

    private static float GetEnemyHitRadius(SkeletonEnemy enemy)
    {
        SpriteRenderer renderer = enemy != null ? enemy.GetComponentInChildren<SpriteRenderer>() : null;
        return renderer != null ? Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y) : 0.2f;
    }

    private static float GetAnimatorClipLength(Animator animator, string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return fallback;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name == clipName) return Mathf.Max(0.05f, clip.length);
        return fallback;
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
                    ApplyProjectileDamageToEnemy(projectile, enemy);
            }
        }
        else
        {
            ApplyProjectileDamageToEnemy(projectile, impactTarget);
        }
    }

    private void ApplyProjectileDamageToEnemy(Projectile projectile, SkeletonEnemy enemy)
    {
        float damage = projectile.Damage;
        if (projectile.Category == DamageCategory.Hero && projectile.Army == ArmyType.Mar && enemy.IsBoss)
            damage *= 1f + marBossDamageBonusPercent / 100f;
        ApplyDamage(enemy, damage, projectile.DamageColor, projectile.Army, projectile.Category);
    }

    private void ApplyDamage(SkeletonEnemy enemy, float amount, Color damageColor, ArmyType army, DamageCategory category)
    {
        if (!CanCombat || enemy == null || enemy.IsDead || !IsValidArmy(army))
            return;

        float appliedDamage = enemy.ApplyDamage(amount);
        if (appliedDamage <= 0f)
            return;
        damageDealt[(int)army, (int)category] += appliedDamage;
        ShowDamage(enemy, appliedDamage, damageColor);
        if (enemy.IsDead && !enemy.RewardGranted)
        {
            enemy.MarkRewardGranted();
            GrantEnemyExperience(enemy.IsBoss);
            if (enemy.IsBoss) QueueBossOffers();
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
        float bestX = float.NegativeInfinity;
        const float horizontalTieTolerance = 0.0001f;
        foreach (SkeletonEnemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            Vector3 enemyPosition = enemy.transform.position;
            if (Vector2.Distance(origin, enemyPosition) > range)
                continue;
            float x = enemyPosition.x;
            // Enemies are stored in spawn order, so the first enemy wins near-equal X ties.
            if (best == null || x > bestX + horizontalTieTolerance)
            {
                best = enemy;
                bestX = x;
            }
        }
        return best;
    }

    internal bool CanCombat => matchStatus == MatchStatus.InProgress && waveInProgress && !paused && !finished;

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

    internal void RestartFinishedMatch()
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

        Transform cannon = tower.Cannon;
        FireProjectile(tower.gameObject, tower.Config.DisplayName, tower.Config.Hero,
            tower.Damage, tower.SplashRadius, target,
            launchPosition: cannon != null ? cannon.position : tower.transform.position + Vector3.right * 0.2f,
            damageCategory: DamageCategory.Tower);
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

        for (int army = 0; army < 3; army++)
            for (int category = 0; category < 4; category++)
                finalDamageDealt[army, category] = damageDealt[army, category];
        RemoveActiveClone();
        ClearKnightAllies();
        ClearWaterProjectiles();
        cloneSpawnPending = false;
        cloneSpawnRemaining = 0f;
        if (defenders != null)
            foreach (Defender defender in defenders) defender?.Effects?.ClearAll();
        foreach (SkeletonEnemy enemy in enemies) enemy?.Effects?.ClearAll();
        finished = true;
        victory = didWin;
        matchStatus = MatchStatus.Finished;
        StateRevision++;
        Debug.Log(didWin ? "Partida finalizada: victoria." : "Partida finalizada: derrota.", this);
        GameSoundManager.Instance?.FadeOutTheme(2f);
        if (!didWin)
            GameSoundManager.Instance?.PlayDefeat();
    }

    private void OnDestroy()
    {
        cloneSpawnPending = false;
        cloneSpawnRemaining = 0f;
        RemoveActiveClone();
        ClearKnightAllies();
        ClearWaterProjectiles();
        if (defenders != null)
            foreach (Defender defender in defenders) defender?.Effects?.ClearAll();
        if (projectileSprite != null)
            Destroy(projectileSprite);
        if (towerProjectileSprite != null)
            Destroy(towerProjectileSprite);
        if (towerProjectileTexture != null)
            Destroy(towerProjectileTexture);
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
