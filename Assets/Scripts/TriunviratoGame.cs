using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the first playable TRIUNVIRATO prototype using scene-authored objects.</summary>
public sealed class TriunviratoGame : MonoBehaviour
{
    public enum MatchStatus { WaitingForPlayers, InProgress, Finished }
    private const float CastleStartHealth = 100f;
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
    [SerializeField] private ArmyUpgradeManager upgrades;
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
    private int remainingToSpawn;
    private float currentWaveRewardMultiplier = 1f;
    private bool waveInProgress;
    private bool earlyWaveCallPending;
    private bool paused;
    private bool finished;
    private bool victory;
    private MatchStatus matchStatus = MatchStatus.WaitingForPlayers;

    public MatchStatus Status => matchStatus;
    public float CastleHealth => Mathf.Max(0f, castleHealth);
    public float CastleMaximumHealth => CastleStartHealth;
    public int StateRevision { get; private set; }
    public bool IsMatchInProgress => matchStatus == MatchStatus.InProgress;
    public int CurrentWave => wave;
    public bool IsWaveInProgress => waveInProgress;
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
        public float AttackTimer;
        public bool ShotPending;
        public float PendingShotTimer;
        public SkeletonEnemy PendingTarget;
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
        if (upgrades == null)
            upgrades = FindAnyObjectByType<ArmyUpgradeManager>();
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
        if (economy != null)
            economy.ResetEconomy();
        TowerShop towerShop = FindAnyObjectByType<TowerShop>();
        if (towerShop != null)
            towerShop.ResetPurchasedTowers();
        ArmyUpgradeManager upgrades = FindAnyObjectByType<ArmyUpgradeManager>();
        if (upgrades != null)
            upgrades.ResetRunLevels();
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
            Animator = animator
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
                    spawnTimer = 0.85f;
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
                intermissionTimer = 3f;
                currentWaveRewardMultiplier = 1f;
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

    private void StartNextWave()
    {
        wave++;
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

    public int GetHeroUpgradeLevel(ArmyType army) =>
        IsValidArmy(army) && upgrades != null ? upgrades.GetLevel(army, ArmyUpgradeKind.Hero) : 0;

    public float GetHeroDamage(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        return config != null
            ? config.GetDamageAtUpgradeLevel(GetHeroUpgradeLevel(army)) *
              (upgrades != null ? upgrades.GetTeamHeroDamageMultiplier() : 1f)
            : 0f;
    }

    public float GetHeroAttackCooldown(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        float multiplier = army == ArmyType.Mar && upgrades != null
            ? upgrades.GetSpecialMultiplier("earth_mar_attack_speed") : 1f;
        return config.AttackCooldown / Mathf.Max(0.01f, multiplier);
    }

    public float GetHeroRange(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        float multiplier = army == ArmyType.Mar && upgrades != null
            ? upgrades.GetSpecialMultiplier("mar_range") : 1f;
        return config.Range * multiplier;
    }

    public float GetHeroSplashRadius(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        if (config == null) return 0f;
        float multiplier = army == ArmyType.Aire && upgrades != null
            ? upgrades.GetSpecialMultiplier("air_area") : 1f;
        return config.SplashRadius * multiplier;
    }

    private void SpawnEnemy(bool isBoss)
    {
        SkeletonEnemy enemy = Instantiate(enemyConfig.Prefab, enemySpawner.position, Quaternion.identity);
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
                    defender.ShotPending = false;
                    defender.PendingTarget = null;
                    if (IsValidPendingHeroShot(defender, pendingTarget))
                        FireProjectile(defender, pendingTarget);
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
                defender.Animator.Play("Attack", 0, 0f);
            defender.ShotPending = true;
            defender.PendingTarget = target;
            defender.PendingShotTimer = defender.Config.ProjectileDelay;
        }
    }

    private bool IsValidPendingHeroShot(Defender defender, SkeletonEnemy target)
    {
        // Range is checked when the attack begins. Once the animation has started, do not
        // discard its shot merely because the moving target crossed the range boundary.
        return CanCombat && defender != null && defender.View != null && target != null && !target.IsDead;
    }

    private void FireProjectile(Defender defender, SkeletonEnemy target)
    {
        if (target == null || target.IsDead)
            return;

        FireProjectile(defender.View, defender.Config.DisplayName, defender.Config,
            GetHeroDamage(defender.Config.Army), GetHeroSplashRadius(defender.Config.Army), target);
    }

    private void FireProjectile(GameObject source, string sourceName, HeroConfig config, float damage,
        float splashRadius, SkeletonEnemy target)
    {
        if (source == null || config == null || target == null || target.IsDead)
            return;

        if (projectileSprite == null)
            projectileSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        Vector3 origin = source.transform.position + Vector3.right * 0.2f;
        Vector3 destination = target.transform.position;
        GameObject view = new GameObject("Proyectil - " + sourceName);
        view.transform.position = origin;
        view.transform.localScale = Vector3.one * 0.14f;

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.color = config.ProjectileColor;
        renderer.sortingOrder = 15;

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

    internal bool CanCombat => matchStatus == MatchStatus.InProgress && !paused && !finished;

    internal void ApplyCastleDamage(float amount)
    {
        if (!CanCombat || amount <= 0f)
            return;
        float actualDamage = Mathf.Min(castleHealth, amount);
        castleHealth = Mathf.Max(0f, castleHealth - actualDamage);
        StateRevision++;
        if (economy != null)
            economy.RewardCastleDamaged(actualDamage);
        if (castleHealth <= 0f)
            EndMatch(false);
    }

    public void RestartFinishedMatch()
    {
        if (matchStatus != MatchStatus.Finished)
            return;

        ResetMatch(true);
    }

    internal void ConfigureTower(DefenseTower tower, TowerConfig config, int individualLevel, int armyLevel)
    {
        if (tower == null || config == null || config.Hero == null)
            return;

        tower.Configure(this, config, individualLevel, armyLevel);
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
    }

    private void OnDestroy()
    {
        if (projectileSprite != null)
            Destroy(projectileSprite);
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
