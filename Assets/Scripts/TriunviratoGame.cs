using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the first playable TRIUNVIRATO prototype using scene-authored objects.</summary>
public sealed class TriunviratoGame : MonoBehaviour
{
    private const int FinalWave = 3;
    private const float CastleStartHealth = 100f;
    private const float ProjectileSpeed = 10f;
    [SerializeField] private Transform tierraHero;
    [SerializeField] private Transform aireHero;
    [SerializeField] private Transform marHero;
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
    private int remainingToSpawn;
    private bool waveInProgress;
    private bool paused;
    private bool finished;
    private bool victory;

    private sealed class Enemy
    {
        public GameObject View;
        public float Health;
        public float Speed;
        public bool IsBoss;
    }

    private sealed class Defender
    {
        public GameObject View;
        public string Name;
        public Color ProjectileColor;
        public float Range;
        public float Damage;
        public float Cooldown;
        public float SplashRadius;
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
        public float RemainingLifetime;
    }

    private sealed class FloatingDamage
    {
        public GameObject View;
        public float RemainingLifetime;
    }

    private void Start()
    {
        if (tierraHero == null || aireHero == null || marHero == null || enemySpawner == null || castleTarget == null || enemySprite == null || damageFont == null)
        {
            Debug.LogError("Asigna los tres héroes, EnemySpawner, CastleTarget, el sprite Assets/Enemies/orc.png y la fuente en el Inspector.", this);
            enabled = false;
            return;
        }
        damageFont.RequestCharactersInTexture("-0123456789", 48, FontStyle.Normal);
        ResetMatch();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetMatch();
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame && !finished)
                paused = !paused;
        }

        if (paused || finished)
            return;

        UpdateWaveFlow();
        UpdateEnemies();
        UpdateDefenders();
        UpdateProjectiles();
        UpdateFloatingDamage();
        RemoveDefeatedEnemies();

        if (castleHealth <= 0f)
            EndMatch(false);
        else if (wave >= FinalWave && !waveInProgress && remainingToSpawn == 0 && enemies.Count == 0)
            EndMatch(true);
    }

    private void ResetMatch()
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
        remainingToSpawn = 0;
        waveInProgress = false;
        paused = false;
        finished = false;
        victory = false;
        ArmyEconomy economy = FindAnyObjectByType<ArmyEconomy>();
        if (economy != null)
            economy.ResetEconomy();
        TowerShop towerShop = FindAnyObjectByType<TowerShop>();
        if (towerShop != null)
            towerShop.ResetPurchasedTowers();
        if (defenders == null)
        {
            defenders = new[]
            {
                CreateDefender("Tierra", tierraHero, new Color(0.25f, 0.72f, 0.3f), 5.3f, 11f, 0.9f, 0f),
                CreateDefender("Aire", aireHero, new Color(1f, 0.78f, 0.18f), 7.5f, 6f, 0.75f, 1.8f),
                CreateDefender("Mar", marHero, new Color(0.2f, 0.65f, 1f), 9f, 22f, 1.5f, 0f)
            };
        }
        foreach (Defender defender in defenders)
        {
            if (defender != null)
                defender.AttackTimer = 0f;
        }
    }

    private Defender CreateDefender(string name, Transform hero, Color color, float range, float damage, float cooldown, float splashRadius)
    {
        if (hero == null)
        {
            Debug.LogError("Falta asignar el héroe de " + name + " en la escena.", this);
            return null;
        }
        return new Defender
        {
            View = hero.gameObject,
            Name = name,
            ProjectileColor = color,
            Range = range,
            Damage = damage,
            Cooldown = cooldown,
            SplashRadius = splashRadius
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
                    SpawnEnemy(wave == FinalWave && remainingToSpawn == 1);
                    remainingToSpawn--;
                    spawnTimer = 0.85f;
                }
            }
            else if (enemies.Count == 0)
            {
                waveInProgress = false;
                intermissionTimer = 3f;
            }
        }
        else if (wave < FinalWave)
        {
            intermissionTimer -= Time.deltaTime;
            if (intermissionTimer <= 0f)
            {
                wave++;
                remainingToSpawn = 4 + wave * 2;
                waveInProgress = true;
                spawnTimer = 0f;
            }
        }
    }

    private void SpawnEnemy(bool isBoss)
    {
        GameObject view = new GameObject(isBoss ? "Jefe" : "Enemigo");
        view.transform.position = enemySpawner.position;
        view.transform.localScale = isBoss ? Vector3.one * 0.09f : Vector3.one * 0.05f;
        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = enemySprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 2;
        float health = (18f + wave * 7f) * (isBoss ? 5f : 1f);
        enemies.Add(new Enemy
        {
            View = view,
            Health = health,
            Speed = isBoss ? 1.1f : 1.8f + wave * 0.1f,
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

            defender.AttackTimer = defender.Cooldown;
            FireProjectile(defender, target);
        }
    }

    private void FireProjectile(Defender defender, Enemy target)
    {
        if (target.View == null)
            return;

        FireProjectile(defender.View, defender.Name, defender.ProjectileColor, defender.Damage,
            defender.SplashRadius, target);
    }

    private void FireProjectile(GameObject source, string sourceName, Color projectileColor, float damage,
        float splashRadius, Enemy target)
    {
        if (source == null || target == null || target.View == null)
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
        renderer.color = projectileColor;
        renderer.sortingOrder = 5;

        projectiles.Add(new Projectile
        {
            View = view,
            Destination = destination,
            Target = target,
            Damage = damage,
            DamageColor = projectileColor,
            SplashRadius = splashRadius,
            RemainingLifetime = 1.5f
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
            view.position = Vector3.MoveTowards(view.position, projectile.Destination, ProjectileSpeed * Time.deltaTime);
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
        renderer.sortingOrder = 8;
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
        return defender == null || defender.View == null ? null : FindTarget(defender.View.transform.position, defender.Range);
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

    internal bool CanCombat => !paused && !finished;

    internal void ConfigureTower(DefenseTower tower, ArmyType army)
    {
        if (tower == null || defenders == null)
            return;

        int index = (int)army;
        if (index < 0 || index >= defenders.Length || defenders[index] == null)
            return;

        Defender hero = defenders[index];
        tower.Configure(this, army, hero.Range, hero.Damage * 0.2f, hero.Cooldown, hero.SplashRadius,
            hero.ProjectileColor);
    }

    internal float GetBaseTowerDamage(ArmyType army)
    {
        int index = (int)army;
        if (defenders == null || index < 0 || index >= defenders.Length || defenders[index] == null)
            return 0f;

        return defenders[index].Damage * 0.2f;
    }

    internal bool TryTowerAttack(DefenseTower tower)
    {
        if (!CanCombat || tower == null)
            return false;

        Enemy target = FindTarget(tower.transform.position, tower.Range);
        if (target == null)
            return false;

        FireProjectile(tower.gameObject, tower.Army.ToString(), tower.ProjectileColor,
            tower.Damage, tower.SplashRadius, target);
        return true;
    }

    private void RemoveDefeatedEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i].Health <= 0f)
            {
                Destroy(enemies[i].View);
                enemies.RemoveAt(i);
            }
        }
    }

    private void EndMatch(bool didWin)
    {
        finished = true;
        victory = didWin;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(18f, 175f, 360f, 165f), GUI.skin.box);
        GUILayout.Label("TRIUNVIRATO  |  Defensa cooperativa");
        GUILayout.Label("Oleada: " + Mathf.Max(1, wave) + " / " + FinalWave);
        GUILayout.Label("Castillo: " + Mathf.CeilToInt(Mathf.Max(0f, castleHealth)) + " / " + (int)CastleStartHealth);
        GUILayout.Label("Enemigos: " + enemies.Count + (remainingToSpawn > 0 ? "  (" + remainingToSpawn + " por aparecer)" : ""));
        GUILayout.Label("Ejércitos: Tierra (línea)  |  Aire (área)  |  Mar (objetivo prioritario)");

        if (finished)
            GUILayout.Label(victory ? "¡Victoria! El reino está a salvo." : "Derrota: el castillo ha caído.");
        else if (paused)
            GUILayout.Label("Pausa");
        else if (!waveInProgress && wave > 0)
            GUILayout.Label("Oleada despejada. Siguiente ataque en " + Mathf.CeilToInt(intermissionTimer) + " s.");

        GUILayout.Label("P: pausa  |  R: reiniciar");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (projectileSprite != null)
            Destroy(projectileSprite);
    }
}
