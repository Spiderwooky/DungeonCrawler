using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Contrôleur d'ennemi tour-par-tour.
/// 
/// Machine à états :
///   Idle   → attend (aucun joueur détecté, pas encore bougé)
///   Wander → se déplace aléatoirement dans sa zone de patrouille
///   Chase  → suit le joueur case par case via A*
///   Attack → inflige des dégâts au joueur s'il est adjacent
///
/// Setup dans Unity :
///   1. Créer un prefab ennemi avec ce script + HealthSystem.
///   2. Assigner un EnemyData dans l'Inspector.
///   3. Assigner la référence GameManager et le Transform du joueur.
///   4. Définir patrolCenter + patrolRadius pour la zone de patrouille.
/// </summary>
public class EnemyController : MonoBehaviour, ITurnActor
{
    // ──────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────

    [Header("Data")]
    [Tooltip("ScriptableObject définissant les stats de cet ennemi.")]
    [SerializeField] private EnemyData data;

    [Header("Références scène")]
    [SerializeField] private GameManager  gameManager;
    [SerializeField] private Transform    playerTransform;

    [Header("Position de départ")]
    [Tooltip("Position initiale en coordonnées grille (comme le joueur).")]
    [SerializeField] private Vector2Int start = Vector2Int.zero;

    [Header("Zone de patrouille")]
    [Tooltip("Centre de la zone (coordonnées grille).")]
    [SerializeField] private Vector2Int patrolCenter = Vector2Int.zero;
    [Tooltip("Rayon en cases autour du centre.")]
    [SerializeField] private int patrolRadius = 3;

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.3f;
    [SerializeField] private float rotationDuration = 0.2f;

    // ──────────────────────────────────────────
    // État interne
    // ──────────────────────────────────────────

    private enum EnemyState { Idle, Wander, Chase, Attack }
    private EnemyState currentState = EnemyState.Idle;

    private Vector2Int gridPosition; // Position courante en coordonnées grille
    private HealthSystem healthSystem;
    private bool isActing; // Vrai pendant l'animation de déplacement

    // Cache des cases accessibles dans la zone de patrouille (calculé une fois)
    private List<Vector2Int> patrolCells;

    // ──────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────

    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem == null)
            Debug.LogError($"[EnemyController] {gameObject.name} : HealthSystem manquant !");

        if (data == null)
        {
            Debug.LogError($"[EnemyController] {gameObject.name} : EnemyData non assigné !");
            return;
        }

        healthSystem?.Initialize(data.maxHealth);

        // ========== INTÉGRATION AUDIO ENNEMI ==========
        // S'abonner aux événements de santé de cet ennemi
        // Permet de jouer les sons d'impact et de mort automatiquement
        if (healthSystem != null)
        {
            // OnDamaged : appelé chaque fois que l'ennemi reçoit des dégâts
            healthSystem.OnDamaged += (currentHealth, maxHealth) => 
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayEnemyDamage();
            };

            // OnDeath : appelé quand les PV de l'ennemi tombent à 0
            healthSystem.OnDeath += () => 
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayEnemyDeath();

                // ========== VÉRIFICATION MUSIQUE À LA MORT ==========
                // Si cet ennemi était en état de combat, vérifier s'il y a d'autres ennemis
                // Si aucun ennemi n'est plus en chasse/attaque, revenir à l'exploration
                if (AudioManager.Instance != null && currentState == EnemyState.Chase || currentState == EnemyState.Attack)
                {
                    AudioManager.Instance.PlayMusicExploration();
                }
            };
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"[EnemyController] {gameObject.name} : EnemyData non assigné !");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogError($"[EnemyController] {gameObject.name} : GameManager non assigné !");
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogError($"[EnemyController] {gameObject.name} : PlayerTransform non assigné !");
            return;
        }

        // Enregistrement TurnManager
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterEnemy(this);
        else
            Debug.LogError($"[EnemyController] {gameObject.name} : TurnManager introuvable !");

        // Instanciation du modèle 3D depuis EnemyData
        if (data.model != null)
            Instantiate(data.model, transform.position, transform.rotation, transform);
        else
            Debug.LogWarning($"[{data.enemyName}] Aucun modèle 3D assigné dans EnemyData.");

        // Positionnement sur la case de départ
        gridPosition = start;
        SnapToGrid();

        Debug.Log($"[{data.enemyName}] Spawné en position grille {gridPosition}");

        // Pré-calcul des cases de patrouille
        Case[][] grid = gameManager.GetGridDefinition();
        patrolCells = Pathfinder.GetReachableCells(grid, patrolCenter, patrolRadius);
        Debug.Log($"[{data.enemyName}] Zone de patrouille : {patrolCells.Count} cases autour de {patrolCenter}");
    }

    // Permet à un gestionnaire externe (GameManager) d'initialiser
    // la position de départ et le centre de patrouille avant Start().
    public void SetStartAndPatrol(Vector2Int startPos, Vector2Int patrolCenterPos)
    {
        start = startPos;
        patrolCenter = patrolCenterPos;
    }

    // ──────────────────────────────────────────
    // ITurnActor
    // ──────────────────────────────────────────

    public void OnTurnStart()
    {
        if (data == null || healthSystem == null || healthSystem.IsDead)
        {
            TurnManager.Instance?.EndTurn();
            return;
        }
        StartCoroutine(TakeTurn());
    }

    // ──────────────────────────────────────────
    // Tour principal
    // ──────────────────────────────────────────

    private IEnumerator TakeTurn()
    {
        isActing = true;

        Vector2Int playerGrid = WorldToGrid(playerTransform.position);

        // ── 1. Déterminer l'état ────────────────
        bool inRadius  = IsPlayerInRadius(playerGrid);
        bool inSight   = inRadius && HasLineOfSight(playerGrid);
        bool canAttack = GridDistance(gridPosition, playerGrid) <= data.attackRadius;

        if (canAttack && inSight)
            currentState = EnemyState.Attack;
        else if (inSight)
            currentState = EnemyState.Chase;
        else
            currentState = EnemyState.Wander;

        Debug.Log($"[{data.enemyName}] État : {currentState} | Distance joueur : {GridDistance(gridPosition, playerGrid)}");

        // ========== TRANSITION MUSICALE ==========
        // Quand l'ennemi détecte le joueur → musique de combat
        // Quand l'ennemi perd le joueur → retour à l'exploration
        if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
        {
            // Passer en mode combat (musique change)
            if (AudioManager.Instance != null && !AudioManager.Instance.IsInCombat)
            {
                AudioManager.Instance.PlayMusicCombat();
            }
        }
        else if (currentState == EnemyState.Wander)
        {
            // Retour au mode exploration (ennemi a perdu le joueur)
            if (AudioManager.Instance != null && AudioManager.Instance.IsInCombat)
            {
                AudioManager.Instance.PlayMusicExploration();
            }
        }

        // ── 2. Agir selon l'état ─────────────────
        switch (currentState)
        {
            case EnemyState.Attack:
                yield return StartCoroutine(DoAttack(playerGrid));
                break;

            case EnemyState.Chase:
                yield return StartCoroutine(DoChase(playerGrid));
                break;

            case EnemyState.Wander:
                yield return StartCoroutine(DoWander());
                break;

            default:
                break; // Idle : ne fait rien
        }

        isActing = false;
        TurnManager.Instance?.EndTurn();
    }

    // ──────────────────────────────────────────
    // Actions
    // ──────────────────────────────────────────

    private IEnumerator DoAttack(Vector2Int playerGrid)
    {
        // ========== SON D'ATTAQUE ==========
        // Jouer un son d'attaque avant le bump visuel
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAttack();

        Debug.Log($"[{data.enemyName}] Attaque le joueur pour {data.attackDamage} dégâts !");

        // Petit bump visuel vers le joueur
        Vector3 direction = (GridToWorld(playerGrid) - transform.position).normalized;
        Vector3 bumpTarget = transform.position + direction * (gameManager.GetStep() * 0.3f);
        yield return StartCoroutine(AnimateBump(transform.position, bumpTarget, 0.15f));

        // Infliger les dégâts via HealthSystem
        // Les sons de dégâts/mort sont gérés automatiquement par HealthSystem.OnDamaged/OnDeath
        HealthSystem playerHealth = playerTransform.GetComponent<HealthSystem>();
        if (playerHealth != null)
            playerHealth.TakeDamage(data.attackDamage);
        else
            Debug.LogWarning("[EnemyController] Le joueur n'a pas de HealthSystem !");
    }

    private IEnumerator DoChase(Vector2Int playerGrid)
    {
        Case[][] grid = gameManager.GetGridDefinition();

        // Trouver le chemin vers le joueur (s'arrêter à côté, pas sur lui)
        List<Vector2Int> path = Pathfinder.FindPath(grid, gridPosition, playerGrid, includeGoalEvenIfWall: true);

        if (path == null || path.Count <= 1)
        {
            Debug.Log($"[{data.enemyName}] Aucun chemin vers le joueur.");
            yield break;
        }

        // Avancer d'autant de cases que le permet moveRange (sans jamais marcher sur le joueur)
        int steps = Mathf.Min(data.moveRange, path.Count - 2); // -2 : ignorer départ et case joueur
        for (int i = 0; i < steps; i++)
        {
            Vector2Int next = path[i + 1];
            yield return StartCoroutine(AnimateMove(GridToWorld(next)));
            gridPosition = next;
        }
    }

    private IEnumerator DoWander()
    {
        if (patrolCells == null || patrolCells.Count == 0) yield break;

        // Choisir une case aléatoire accessible dans la zone (différente de la position actuelle)
        List<Vector2Int> candidates = patrolCells.FindAll(c => c != gridPosition);
        if (candidates.Count == 0) yield break;

        Vector2Int target = candidates[Random.Range(0, candidates.Count)];
        Case[][] grid = gameManager.GetGridDefinition();
        List<Vector2Int> path = Pathfinder.FindPath(grid, gridPosition, target);

        if (path == null || path.Count <= 1) yield break;

        // Avancer d'au plus moveRange cases vers la cible aléatoire
        int steps = Mathf.Min(data.moveRange, path.Count - 1);
        for (int i = 0; i < steps; i++)
        {
            Vector2Int next = path[i + 1];
            yield return StartCoroutine(AnimateMove(GridToWorld(next)));
            gridPosition = next;
        }
    }

    // ──────────────────────────────────────────
    // Détection
    // ──────────────────────────────────────────

    private bool IsPlayerInRadius(Vector2Int playerGrid)
        => GridDistance(gridPosition, playerGrid) <= data.detectionRadius;

    /// <summary>
    /// Vérifie qu'aucun mur ne bloque la ligne entre l'ennemi et le joueur
    /// en testant les cases sur le segment (algorithme de Bresenham).
    /// </summary>
    private bool HasLineOfSight(Vector2Int playerGrid)
    {
        Case[][] grid = gameManager.GetGridDefinition();
        List<Vector2Int> line = BresenhamLine(gridPosition, playerGrid);

        foreach (Vector2Int cell in line)
        {
            // Ignorer les extrémités (position ennemi et position joueur)
            if (cell == gridPosition || cell == playerGrid) continue;

            if (cell.x < 0 || cell.x >= grid.Length)         return false;
            if (cell.y < 0 || cell.y >= grid[cell.x].Length) return false;

            Case c = grid[cell.x][cell.y];
            if (c != null && c.IsWall()) return false;
        }

        return true;
    }

    // Algorithme de Bresenham pour tracer une ligne entre deux points sur la grille
    private List<Vector2Int> BresenhamLine(Vector2Int from, Vector2Int to)
    {
        var cells = new List<Vector2Int>();

        int x0 = from.x, y0 = from.y;
        int x1 = to.x,   y1 = to.y;

        int dx =  Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            cells.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }

        return cells;
    }

    // ──────────────────────────────────────────
    // Animations
    // ──────────────────────────────────────────

    private IEnumerator AnimateMove(Vector3 targetWorldPos)
    {
        Vector3 start   = transform.position;
        float   elapsed = 0f;

        // Calculer la rotation cible vers la destination
        Vector3 direction = (targetWorldPos - start);
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion startRotation  = transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Rotation et déplacement en parallèle
            while (elapsed < moveDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                transform.position = Vector3.Lerp(start, targetWorldPos, t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Pas de direction (cas improbable) : juste le déplacement
            while (elapsed < moveDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                transform.position = Vector3.Lerp(start, targetWorldPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        transform.position = targetWorldPos;
    }

    private IEnumerator AnimateBump(Vector3 start, Vector3 bumpTarget, float duration)
    {
        float half = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            transform.position = Vector3.Lerp(start, bumpTarget, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            transform.position = Vector3.Lerp(bumpTarget, start, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = start;
    }

    // ──────────────────────────────────────────
    // Utilitaires de coordonnées
    // ──────────────────────────────────────────

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float step = gameManager.GetStep();
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / step),
            Mathf.RoundToInt(worldPos.z / step)
        );
    }

    private Vector3 GridToWorld(Vector2Int gridPos)
    {
        float step = gameManager.GetStep();
        return new Vector3(gridPos.x * step, 0f, gridPos.y * step);
    }

    private void SnapToGrid()
    {
        transform.position = GridToWorld(gridPosition);
    }

    private static int GridDistance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}