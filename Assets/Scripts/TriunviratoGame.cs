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
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private Defender[] defenders;
    private Sprite enemySprite;
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
        public float RemainingLifetime;
    }

    private void Start()
    {
        if (tierraHero == null || aireHero == null || marHero == null || enemySpawner == null || castleTarget == null)
        {
            Debug.LogError("Asigna los tres héroes, EnemySpawner y CastleTarget en el Inspector.", this);
            enabled = false;
            return;
        }
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
        castleHealth = CastleStartHealth;
        spawnTimer = 1.5f;
        intermissionTimer = 0f;
        wave = 0;
        remainingToSpawn = 0;
        waveInProgress = false;
        paused = false;
        finished = false;
        victory = false;
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
        SpriteRenderer renderer = hero.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 1;
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
        view.transform.localScale = isBoss ? Vector3.one * 0.6f : Vector3.one * 0.3f;
        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        if (enemySprite == null)
            enemySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.sprite = enemySprite;
        renderer.color = isBoss ? new Color(0.38f, 0.08f, 0.2f) : new Color(0.82f, 0.12f, 0.16f);
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
            if (defender.SplashRadius > 0f)
            {
                Vector3 targetPosition = target.View.transform.position;
                foreach (Enemy enemy in enemies)
                {
                    if (enemy.View != null && Vector2.Distance(targetPosition, enemy.View.transform.position) <= defender.SplashRadius)
                        enemy.Health -= defender.Damage;
                }
            }
            else
            {
                target.Health -= defender.Damage;
            }
        }
    }

    private void FireProjectile(Defender defender, Enemy target)
    {
        if (enemySprite == null || target.View == null)
            return;

        Vector3 origin = defender.View.transform.position + Vector3.right * 0.2f;
        Vector3 destination = target.View.transform.position;
        GameObject view = new GameObject("Proyectil - " + defender.Name);
        view.transform.position = origin;
        view.transform.localScale = Vector3.one * 0.14f;

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = enemySprite;
        renderer.color = defender.ProjectileColor;
        renderer.sortingOrder = 5;

        projectiles.Add(new Projectile
        {
            View = view,
            Destination = destination,
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
            view.position = Vector3.MoveTowards(view.position, projectile.Destination, ProjectileSpeed * Time.deltaTime);
            projectile.RemainingLifetime -= Time.deltaTime;
            if (projectile.RemainingLifetime <= 0f || Vector3.Distance(view.position, projectile.Destination) <= 0.08f)
            {
                Destroy(projectile.View);
                projectiles.RemoveAt(i);
            }
        }
    }

    private Enemy FindTarget(Defender defender)
    {
        Enemy best = null;
        float bestDistance = defender.Range;
        foreach (Enemy enemy in enemies)
        {
            if (enemy.View == null)
                continue;
            float distance = Vector2.Distance(defender.View.transform.position, enemy.View.transform.position);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
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
        GUILayout.BeginArea(new Rect(18f, 18f, 360f, 165f), GUI.skin.box);
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
        if (enemySprite != null)
            Destroy(enemySprite);
    }
}
