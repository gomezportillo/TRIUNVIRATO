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
    private enum DamageCategory { Hero = 0, Tower = 1, SpecialAbility = 2 }
    private const float CastleStartHealth = 100f;
    private const int SpecialAbilityStars = 3;
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
    [SerializeField, Tooltip("Si se asigna, los límites del SpriteRenderer delimitan dónde caen los meteoritos.")]
    private SpriteRenderer meteorShowerArea;
    private readonly List<SkeletonEnemy> enemies = new List<SkeletonEnemy>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<FloatingDamage> floatingDamage = new List<FloatingDamage>();
    private readonly List<GameObject> specialEffects = new List<GameObject>();
    private Defender[] defenders;
    private Sprite projectileSprite;
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
    private readonly float[,] damageDealt = new float[3, 3];
    private readonly float[,] finalDamageDealt = new float[3, 3];
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
    private readonly float[] specialCooldownEndsAt = new float[3];
    private bool specialCooldownsWereRunning;
    private bool warnedMissingZombieGraspController;
    private bool warnedMissingMeteorController;
    private bool warnedMissingLightningController;
    private MatchStatus matchStatus = MatchStatus.WaitingForPlayers;

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
        public float AttackTimer;
        public bool ShotPending;
        public bool SpecialAnimationActive;
        public bool SpecialAnimationWarningLogged;
        public float SpecialAnimationStartedAt;
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
        public ArmyType Army;
        public DamageCategory Category;
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
        if (upgradeCatalog == null)
            Debug.LogError("Asigna UpgradeCatalog desde Assets/GameData/Upgrades en el Inspector.", this);
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
        UpdateProjectiles();
        UpdateFloatingDamage();
        RemoveFinishedEnemyDeaths();

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

    private void ResetMatch(bool resumeMatch)
    {
        StopAllCoroutines();
        foreach (GameObject effect in specialEffects)
        {
            if (effect != null)
                Destroy(effect);
        }
        specialEffects.Clear();
        Array.Clear(specialCooldownEndsAt, 0, specialCooldownEndsAt.Length);
        specialCooldownsWereRunning = false;
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
                defender.PendingTarget = null;
                defender.PendingTargetPosition = Vector3.zero;
                defender.PendingShotTimer = 0f;
                defender.SpecialAnimationActive = false;
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
                spawnTimer = 1f;
            }
        }
        else if (enemies.Count == 0)
        {
            waveInProgress = false;
            StartNextWave();
        }
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
                bool normal = UnityEngine.Random.Range(0, 2) == 0;
                id = normal ? "special_grasp_normal" : "special_grasp_boss";
                name = normal ? "Agarre prolongado" : "Agarre contra bosses";
                increment = normal ? zombieGraspAbility.NormalDurationUpgrade : zombieGraspAbility.BossDurationUpgrade;
                description = normal ? "Inmovilización contra enemigos normales +" + increment.ToString("0.##") + " s" :
                    "Inmovilización contra bosses +" + increment.ToString("0.##") + " s";
                break;
            case ArmyType.Aire:
                bool moreMeteors = UnityEngine.Random.Range(0, 2) == 0;
                id = moreMeteors ? "special_meteor_count" : "special_meteor_damage";
                name = moreMeteors ? "Lluvia abundante" : "Meteoritos devastadores";
                increment = moreMeteors ? meteorShowerAbility.MeteorCountUpgrade : meteorShowerAbility.DamageUpgrade;
                description = moreMeteors ? "Meteoritos por activación +" + increment.ToString("0.##") :
                    "Daño por meteorito +" + increment.ToString("0.##");
                break;
            default:
                bool moreHits = UnityEngine.Random.Range(0, 2) == 0;
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
        HeroConfig config = GetHeroConfig(army);
        return config != null
            ? config.Damage * GetHeroUpgradeMultiplier(army) + GetHeroDamageFixedBonus(army)
            : 0f;
    }

    public float GetHeroAttackCooldown(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        return ApplyHeroAttackSpeedToCooldown(config.AttackCooldown, army);
    }

    public float GetSpecialAbilityCooldown(ArmyType army) =>
        ApplyHeroAttackSpeedToCooldown(GetBaseSpecialCooldown(army), army);

    public float GetSpecialAbilityCooldownRemaining(ArmyType army) =>
        IsValidArmy(army) ? Mathf.Max(0f, specialCooldownEndsAt[(int)army] - Time.time) : 0f;

    public bool CanActivateSpecial(ArmyType army) =>
        CanCombat && IsValidArmy(army) && defenders != null && defenders[(int)army] != null &&
        GetSpecialAbilityConfig(army) != null &&
        GetSpecialAbilityCooldownRemaining(army) <= 0f && HasLivingEnemies();

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
        if (remaining > 0f)
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
        if (hasRunningCooldown != specialCooldownsWereRunning)
        {
            specialCooldownsWereRunning = hasRunningCooldown;
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
            if (!defender.SpecialAnimationActive && defender.Animator != null && defender.Animator.runtimeAnimatorController != null)
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
            Army = config.Army,
            Category = damageCategory,
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
            for (int category = 0; category < 3; category++)
                finalDamageDealt[army, category] = damageDealt[army, category];
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
        if (projectileSprite != null)
            Destroy(projectileSprite);
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
