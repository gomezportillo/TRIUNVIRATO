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
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private Font damageFont;
    private readonly List<Enemy> enemies = new List<Enemy>();
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
    private readonly int[] heroUpgradeLevels = new int[3];

    public MatchStatus Status => matchStatus;
    public int StateRevision { get; private set; }
    public bool IsMatchInProgress => matchStatus == MatchStatus.InProgress;
    public int CurrentWave => wave;
    public bool IsWaveInProgress => waveInProgress;
    public bool IsEarlyWaveCallPending => earlyWaveCallPending;
    public int NextEarlyCallAvailableWave => lastEarlyCallWave + 2;
    public bool CanCallNextWaveEarly => CanCombat && !earlyWaveCallPending && wave >= lastEarlyCallWave + 2;

    private sealed class Enemy
    {
        public GameObject View;
        public float Health;
        public float Speed;
        public bool IsBoss;
        public bool RewardGranted;
    }

    private sealed class Defender
    {
        public GameObject View;
        public HeroConfig Config;
        public float AttackTimer;
    }

    private sealed class Projectile
    {
        public GameObject View;
        public Vector3 Destination;
        public Enemy Target;
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
            enemySpawner == null || castleTarget == null || enemySprite == null || damageFont == null)
        {
            Debug.LogError("Asigna los tres héroes y sus configuraciones, EnemySpawner, CastleTarget, el sprite del enemigo y la fuente en el Inspector.", this);
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
        UpdateEnemies();
        UpdateDefenders();
        UpdateProjectiles();
        UpdateFloatingDamage();
        RemoveDefeatedEnemies();

        if (castleHealth <= 0f)
            EndMatch(false);
    }

    private void ResetMatch(bool resumeMatch)
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy.View != null)
                Destroy(enemy.View);
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
                defender.AttackTimer = 0f;
        }
        for (int i = 0; i < heroUpgradeLevels.Length; i++)
            heroUpgradeLevels[i] = 0;
    }

    private Defender CreateDefender(Transform hero, HeroConfig config, ArmyType expectedArmy)
    {
        if (hero == null || config == null || config.Army != expectedArmy)
        {
            Debug.LogError("Falta el héroe de " + expectedArmy + " o su HeroConfig no coincide con el ejército.", this);
            return null;
        }
        return new Defender
        {
            View = hero.gameObject,
            Config = config
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
        IsValidArmy(army) ? heroUpgradeLevels[(int)army] : 0;

    public float GetHeroDamage(ArmyType army)
    {
        HeroConfig config = GetHeroConfig(army);
        return config != null ? config.GetDamageAtUpgradeLevel(GetHeroUpgradeLevel(army)) : 0f;
    }

    internal void SetHeroUpgradeLevel(ArmyType army, int level)
    {
        if (!IsValidArmy(army))
            return;
        heroUpgradeLevels[(int)army] = Mathf.Max(0, level);
        StateRevision++;
    }

    private void SpawnEnemy(bool isBoss)
    {
        GameObject view = new GameObject(isBoss ? "Jefe" : "Enemigo");
        view.transform.position = enemySpawner.position;
        view.transform.localScale = isBoss ? Vector3.one * 0.09f : Vector3.one * 0.05f;
        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = enemySprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;
        float health = (18f + wave * 7f) * Mathf.Pow(1.05f, wave - 1) * (isBoss ? 5f : 1f);
        float waveOneSpeed = isBoss ? 1.1f : 1.9f;
        float speed = waveOneSpeed * 0.5f * Mathf.Pow(1.05f, wave - 1);
        enemies.Add(new Enemy
        {
            View = view,
            Health = health,
            Speed = speed,
            IsBoss = isBoss
        });
    }

    private void UpdateEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (enemy.View == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            enemy.View.transform.position = Vector3.MoveTowards(
                enemy.View.transform.position,
                castleTarget.position,
                enemy.Speed * Time.deltaTime);
            if (Vector2.Distance(enemy.View.transform.position, castleTarget.position) <= 0.2f)
            {
                castleHealth -= enemy.IsBoss ? 35f : 12f;
                Destroy(enemy.View);
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
            defender.AttackTimer -= Time.deltaTime;
            if (defender.AttackTimer > 0f)
                continue;

            Enemy target = FindTarget(defender);
            if (target == null)
                continue;

            defender.AttackTimer = defender.Config.AttackCooldown;
            FireProjectile(defender, target);
        }
    }

    private void FireProjectile(Defender defender, Enemy target)
    {
        if (target.View == null)
            return;

        FireProjectile(defender.View, defender.Config.DisplayName, defender.Config,
            GetHeroDamage(defender.Config.Army), defender.Config.SplashRadius, target);
    }

    private void FireProjectile(GameObject source, string sourceName, HeroConfig config, float damage,
        float splashRadius, Enemy target)
    {
        if (source == null || config == null || target == null || target.View == null)
            return;

        if (projectileSprite == null)
            projectileSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        Vector3 origin = source.transform.position + Vector3.right * 0.2f;
        Vector3 destination = target.View.transform.position;
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
            if (projectile.Target != null && projectile.Target.View != null)
                projectile.Destination = projectile.Target.View.transform.position;

            Vector3 previousPosition = view.position;
            view.position = Vector3.MoveTowards(view.position, projectile.Destination, projectile.Speed * Time.deltaTime);
            projectile.RemainingLifetime -= Time.deltaTime;
            Enemy hit = FindProjectileImpact(previousPosition, view.position);
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

    private Enemy FindProjectileImpact(Vector3 start, Vector3 end)
    {
        Vector2 start2D = start;
        Vector2 segment = (Vector2)end - start2D;
        float segmentLengthSquared = segment.sqrMagnitude;
        float firstImpact = float.MaxValue;
        Enemy hit = null;

        foreach (Enemy enemy in enemies)
        {
            if (enemy.View == null)
                continue;

            Vector2 enemyPosition = enemy.View.transform.position;
            float alongSegment = segmentLengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(enemyPosition - start2D, segment) / segmentLengthSquared)
                : 0f;
            Vector2 closestPoint = start2D + segment * alongSegment;
            SpriteRenderer renderer = enemy.View.GetComponent<SpriteRenderer>();
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

    private void ApplyProjectileDamage(Projectile projectile, Enemy impactTarget)
    {
        if (impactTarget.View == null)
            return;

        if (projectile.SplashRadius > 0f)
        {
            Vector3 impactPosition = impactTarget.View.transform.position;
            foreach (Enemy enemy in enemies)
            {
                if (enemy.View != null && Vector2.Distance(impactPosition, enemy.View.transform.position) <= projectile.SplashRadius)
                    ApplyDamage(enemy, projectile.Damage, projectile.DamageColor);
            }
        }
        else
        {
            ApplyDamage(impactTarget, projectile.Damage, projectile.DamageColor);
        }
    }

    private void ApplyDamage(Enemy enemy, float amount, Color damageColor)
    {
        enemy.Health -= amount;
        ShowDamage(enemy, amount, damageColor);
    }

    private void ShowDamage(Enemy enemy, float amount, Color damageColor)
    {
        if (enemy.View == null)
            return;

        GameObject view = new GameObject("Daño - " + Mathf.RoundToInt(amount));
        view.transform.position = enemy.View.transform.position + Vector3.up * (enemy.IsBoss ? 0.75f : 0.55f);
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

    private Enemy FindTarget(Defender defender)
    {
        return defender == null || defender.View == null || defender.Config == null
            ? null : FindTarget(defender.View.transform.position, defender.Config.Range);
    }

    private Enemy FindTarget(Vector3 origin, float range)
    {
        Enemy best = null;
        float bestDistance = range;
        foreach (Enemy enemy in enemies)
        {
            if (enemy.View == null)
                continue;
            float distance = Vector2.Distance(origin, enemy.View.transform.position);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    internal bool CanCombat => matchStatus == MatchStatus.InProgress && !paused && !finished;

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

        Enemy target = FindTarget(tower.transform.position, tower.Range);
        if (target == null)
            return false;

        FireProjectile(tower.gameObject, tower.Config.DisplayName, tower.Config.Hero,
            tower.Damage, tower.SplashRadius, target);
        return true;
    }

    private void RemoveDefeatedEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i].Health <= 0f)
            {
                if (!enemies[i].RewardGranted)
                {
                    enemies[i].RewardGranted = true;
                    if (economy != null)
                        economy.RewardEnemyDefeated(enemies[i].IsBoss);
                }
                Destroy(enemies[i].View);
                enemies.RemoveAt(i);
            }
        }
    }

    private void EndMatch(bool didWin)
    {
        finished = true;
        victory = didWin;
        matchStatus = MatchStatus.Finished;
        StateRevision++;
        Debug.Log(didWin ? "Partida finalizada: victoria." : "Partida finalizada: derrota.", this);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(18f, 175f, 420f, 205f), GUI.skin.box);
        GUILayout.Label("TRIUNVIRATO  |  Defensa cooperativa");
        GUILayout.Label(matchStatus == MatchStatus.WaitingForPlayers ? "Sala de espera: elige ejército desde el móvil." :
            "Partida: " + matchStatus);
        GUILayout.Label("🌊 Oleada: " + Mathf.Max(1, wave) + " / ∞");
        GUILayout.Label("❤️ Castillo: " + Mathf.CeilToInt(Mathf.Max(0f, castleHealth)) + " / " + (int)CastleStartHealth);
        GUILayout.Label("👾 Enemigos: " + enemies.Count + (remainingToSpawn > 0 ? "  (" + remainingToSpawn + " por aparecer)" : ""));
        GUILayout.Label("🟢 Tierra (línea)  |  🔴 Aire (área)  |  🔵 Mar (objetivo prioritario)");

        if (finished)
            GUILayout.Label(victory ? "¡Victoria! El reino está a salvo." : "Derrota: el castillo ha caído.");
        else if (paused)
            GUILayout.Label("Pausa");
        else if (!waveInProgress && wave > 0)
            GUILayout.Label("Oleada despejada. Siguiente ataque en " + Mathf.CeilToInt(intermissionTimer) + " s.");

        if (earlyWaveCallPending)
            GUILayout.Label("Mar ha llamado antes a la siguiente oleada.");
        GUILayout.Label("P: pausa  |  R: reiniciar");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (projectileSprite != null)
            Destroy(projectileSprite);
    }

    private static bool IsValidArmy(ArmyType army) => (int)army >= 0 && (int)army < 3;
}
