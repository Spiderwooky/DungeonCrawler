using UnityEngine;

// Point d'entrée de la partie : construit la grille (via BspDungeonGenerator), positionne
// joueur/ennemis, instancie le terrain, puis démarre le TurnManager.
// S'exécute après les scripts par défaut (DefaultExecutionOrder 100) pour garantir que la
// grille existe déjà quand les Start() des autres scripts (joueur, ennemis) s'exécutent.
[DefaultExecutionOrder(100)]
public class GameManager : MonoBehaviour
{
    [SerializeField]
    private GridGenerator gridGenerator;
 
    [Header("Procedural Generation")]
    [Tooltip("Optional reference to a BspDungeonGenerator to generate the grid procedurally.")]
    [SerializeField]
    private BspDungeonGenerator bspGenerator;

    [SerializeField]
    private TurnManager turnManager;
 
    // Case de départ du joueur, en coordonnées grille (x, z).
    // Recalculée automatiquement dans AssignStartPositionsFromGrid() au lancement.
    [SerializeField]
    private Vector2Int start = new Vector2Int(1, 1);
 
    [SerializeField]
    private float step = 5f;
 
    [SerializeField]
    private GameObject terrain;
 
    [Header("Floor")]
    [Tooltip("Prefab de sol unique (composant FloorTile) : ses murs/coins/pilier sont activés au cas par cas par GridGenerator.")]
    [SerializeField]
    private GameObject floorPrefab;

    [Header("Spawning")]
    [Tooltip("Minimum allowed distance (in grid cells) between player and enemies.")]
    [SerializeField]
    private int minEnemyDistance = 5;

    private Case[][] gridDefinition;
 
    // ──────────────────────────────────────────
    // Awake : initialisation de la grille
    // Awake est garanti de s'exécuter avant tous les Start(),
    // ce qui évite le bug de race condition avec le PlayerController.
    // ──────────────────────────────────────────
    private void Awake()
    {
        if (gridGenerator == null)
        {
            Debug.LogError("GridGenerator reference is missing on GameManager.");
            return;
        }
 
        if (terrain == null)
        {
            Debug.LogError("Terrain parent is missing on GameManager.");
            return;
        }
 
        if (floorPrefab == null)
        {
            Debug.LogError("Floor prefab is missing on GameManager.");
            return;
        }

        // Auto-create or auto-assign a BspDungeonGenerator if none provided
        if (bspGenerator == null)
        {
            // Try to find an existing generator in the scene
            var found = Object.FindObjectsByType<BspDungeonGenerator>(FindObjectsSortMode.None);
            if (found != null && found.Length > 0)
            {
                bspGenerator = found[0];
                Debug.Log("[GameManager] Assigned existing BspDungeonGenerator found in scene.");
            }
            else
            {
                // Create a dedicated child GameObject to hold the generator
                GameObject go = new GameObject("BspDungeonGenerator");
                go.transform.SetParent(this.transform);
                bspGenerator = go.AddComponent<BspDungeonGenerator>();
                Debug.Log("[GameManager] Created new BspDungeonGenerator GameObject and assigned it.");
            }
        }
 
        GenerateGridDefinition();
        // Ensure player and enemies have coherent start positions based on the generated grid
        AssignStartPositionsFromGrid();

        gridGenerator.GenerateGridAndTerrain(this);
    }

    // Assign player start and enemy starts/patrol centers based on available ground cells
    private void AssignStartPositionsFromGrid()
    {
        if (gridDefinition == null) return;

        // Collect all ground cells
        var groundCells = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < gridDefinition.Length; x++)
        {
            for (int z = 0; z < gridDefinition[x].Length; z++)
            {
                if (gridDefinition[x][z] != null && gridDefinition[x][z].IsGround())
                    groundCells.Add(new Vector2Int(x, z));
            }
        }

        if (groundCells.Count == 0)
        {
            Debug.LogWarning("[GameManager] No ground cells found to place player/enemies.");
            return;
        }

        // Choose player start near the center of the grid if possible
        int centerX = gridDefinition.Length / 2;
        int centerZ = gridDefinition[0].Length / 2;
        Vector2Int playerCell = FindNearestGround(centerX, centerZ, groundCells);
        start = playerCell;

        // Assign enemies to random distinct ground cells (unique, not overlapping player)
        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        var rnd = new System.Random();
        var usedCells = new System.Collections.Generic.HashSet<Vector2Int>();
        usedCells.Add(playerCell);

        foreach (var enemy in enemies)
        {
            Vector2Int chosen = playerCell;

            // Build list of candidates that are unused and satisfy minimum distance from player
            var validCandidates = new System.Collections.Generic.List<Vector2Int>();
            foreach (var c in groundCells)
            {
                if (usedCells.Contains(c)) continue;
                if (Vector2Int.Distance(c, playerCell) >= minEnemyDistance)
                {
                    validCandidates.Add(c);
                }
            }

            if (validCandidates.Count > 0)
            {
                chosen = validCandidates[rnd.Next(validCandidates.Count)];
            }
            else
            {
                // Fallback: pick any unused ground cell if no candidate meets the distance
                var unused = new System.Collections.Generic.List<Vector2Int>();
                foreach (var c in groundCells) if (!usedCells.Contains(c)) unused.Add(c);
                if (unused.Count > 0)
                {
                    chosen = unused[rnd.Next(unused.Count)];
                }
            }

            usedCells.Add(chosen);
            enemy.SetStartAndPatrol(chosen, chosen);
        }
    }

    private Vector2Int FindNearestGround(int x, int z, System.Collections.Generic.List<Vector2Int> groundCells)
    {
        Vector2Int best = groundCells[0];
        float bestDist = Vector2Int.Distance(best, new Vector2Int(x, z));
        foreach (var c in groundCells)
        {
            float d = Vector2Int.Distance(c, new Vector2Int(x, z));
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }
 
    // ──────────────────────────────────────────
    // Start : démarrage du jeu (après que tous les Awake ont tourné)
    // ──────────────────────────────────────────
    private void Start()
    {
        if (turnManager != null)
        {
            turnManager.StartGame();
        }
        else
        {
            Debug.LogWarning("TurnManager reference is missing on GameManager. Tour-par-tour désactivé.");
        }
    }
 
    // ──────────────────────────────────────────
    // Définition de la grille
    // ──────────────────────────────────────────
 
    // Génère la définition de la grille (appelé dans Awake).
    // Note : Awake() garantit toujours qu'un BspDungeonGenerator existe avant cet appel
    // (assigné depuis la scène ou créé automatiquement), donc la génération procédurale
    // est le seul chemin réellement utilisé ici.
    public void GenerateGridDefinition()
    {
        if (gridDefinition != null) return;

        if (bspGenerator == null)
        {
            Debug.LogError("[GameManager] Aucun BspDungeonGenerator disponible : impossible de générer la grille.");
            return;
        }

        // Force la génération (sans danger si bspGenerator a déjà tourné dans son propre Awake)
        bspGenerator.GenerateDungeon();
        gridDefinition = bspGenerator.GetDungeonGrid();
        ValidateStartPosition();
    }
 
    // Retourne la grille (la génère si nécessaire).
    public Case[][] GetGridDefinition()
    {
        if (gridDefinition == null) GenerateGridDefinition();
        return gridDefinition;
    }
 
    // ──────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────
 
    private void ValidateStartPosition()
    {
        if (gridDefinition == null) return;
 
        int row = start.x;
        int col = start.y;
 
        bool rowValid = row >= 0 && row < gridDefinition.Length;
        bool colValid = rowValid && col >= 0 && col < gridDefinition[row].Length;
 
        if (!rowValid || !colValid)
        {
            Debug.LogError($"[GameManager] La position de départ ({row},{col}) est hors des limites de la grille ({gridDefinition.Length}x{gridDefinition[0].Length}). Remise à (1,1).");
            start = new Vector2Int(1, 1);
            return;
        }
 
        if (gridDefinition[row][col].IsWall())
        {
            Debug.LogWarning($"[GameManager] La position de départ ({row},{col}) est un mur !");
        }
    }
 
    // ──────────────────────────────────────────
    // Accesseurs publics
    // ──────────────────────────────────────────
 
    public Vector2Int GetStart() => start;
    public float GetStep() => step;
    public GameObject GetTerrain() => terrain;
    public GameObject GetFloorPrefab() => floorPrefab;
}