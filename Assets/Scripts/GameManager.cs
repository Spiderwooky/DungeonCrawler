using UnityEngine;

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
 
    [SerializeField]
    private int[] start = new int[3] { 1, 1, 0 }; // ligne, colonne, étage
 
    [SerializeField]
    private float step = 5f;
 
    [SerializeField]
    private GameObject terrain;
 
    [Header("Ground")]
    [SerializeField]
    private GameObject groundModel;

    [Header("Spawning")]
    [Tooltip("Minimum allowed distance (in grid cells) between player and enemies.")]
    [SerializeField]
    private int minEnemyDistance = 5;
 
    [Header("Wall Variants")]
    [SerializeField] private GameObject wallPillar;
    [SerializeField] private GameObject wallStraightNS;
    [SerializeField] private GameObject wallStraightEW;
    [SerializeField] private GameObject wallCornerNE;
    [SerializeField] private GameObject wallCornerNW;
    [SerializeField] private GameObject wallCornerSE;
    [SerializeField] private GameObject wallCornerSW;
    [SerializeField] private GameObject wallTNoNorth;
    [SerializeField] private GameObject wallTNoEast;
    [SerializeField] private GameObject wallTNoSouth;
    [SerializeField] private GameObject wallTNoWest;
    [SerializeField] private GameObject wallCross;
    [SerializeField] private GameObject wallEndNorth;
    [SerializeField] private GameObject wallEndEast;
    [SerializeField] private GameObject wallEndSouth;
    [SerializeField] private GameObject wallEndWest;
 
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
 
        if (groundModel == null)
        {
            Debug.LogError("Ground model is missing on GameManager.");
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
        start[0] = playerCell.x;
        start[1] = playerCell.y;

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
    public void GenerateGridDefinition()
    {
        if (gridDefinition != null) return;

        // If a BSP generator is provided, use its procedurally generated grid.
        if (bspGenerator != null)
        {
            // Force generation (safe even if bspGenerator already ran in Awake)
            bspGenerator.GenerateDungeon();
            gridDefinition = bspGenerator.GetDungeonGrid();
            ValidateStartPosition();
            return;
        }
 
        Case g = new Case(CellType.Ground);
        Case w = new Case(CellType.Wall);
 
        // gridDefinition = new Case[9][]
        // {
        //     new Case[5] { w, w, g, w, w },
        //     new Case[5] { w, g, g, g, w },
        //     new Case[5] { w, g, w, g, w },
        //     new Case[5] { w, g, g, g, w },
        //     new Case[5] { w, w, g, w, w },
        //     new Case[5] { w, w, g, g, w },
        //     new Case[5] { w, g, w, g, w },
        //     new Case[5] { w, g, g, g, w },
        //     new Case[5] { w, w, w, w, w }
        // };

        //    0  1  2  3  4  5  6  7  8  9 10 11 12 13 14
        //  0 W  W  W  W  W  W  W  W  W  W  W  W  W  W  W
        //  1 W  G  G  G  W  W  W  G  G  G  W  W  W  W  W
        //  2 W  G  W  G  W  W  W  G  W  G  W  W  W  W  W
        //  3 W  G  G  G  G  G  G  G  G  G  W  W  W  W  W
        //  4 W  W  W  G  W  W  W  W  W  G  W  G  G  G  W
        //  5 W  W  W  G  W  G  G  G  W  G  G  G  W  G  W
        //  6 W  W  W  G  G  G  W  G  W  W  W  G  W  G  W
        //  7 W  W  W  W  W  G  W  G  G  G  G  G  G  G  W
        //  8 W  G  G  G  W  G  W  W  W  W  W  G  W  W  W
        //  9 W  G  W  G  W  G  G  G  W  W  W  G  G  G  W
        // 10 W  G  G  G  G  G  W  G  W  G  G  G  W  G  W
        // 11 W  W  W  W  W  G  W  G  W  G  W  W  W  G  W
        // 12 W  W  W  W  W  G  G  G  W  G  G  G  G  G  W
        // 13 W  W  W  W  W  W  W  W  W  W  W  W  W  W  W

        gridDefinition = new Case[15][]
        {
            // col 0  — bord ouest
            new Case[14] { w, w, w, w, w, w, w, w, w, w, w, w, w, w },
            // col 1  — salle A (nord-ouest)
            new Case[14] { w, g, g, g, w, w, w, w, g, g, w, w, w, w },
            // col 2
            new Case[14] { w, g, w, g, w, w, w, w, g, g, w, w, w, w },
            // col 3  — couloir horizontal + salle A
            new Case[14] { w, g, g, g, g, g, g, w, g, g, w, w, w, w },
            // col 4  — jonction couloirs
            new Case[14] { w, w, w, g, w, g, w, w, w, g, w, w, w, w },
            // col 5  — couloir vertical central
            new Case[14] { w, w, w, g, w, g, g, g, g, g, g, g, g, w },
            // col 6  — salle centrale
            new Case[14] { w, w, w, g, g, g, w, w, w, w, w, g, g, w },
            // col 7  — salle centrale
            new Case[14] { w, w, w, w, w, g, w, g, g, g, g, g, g, w },
            // col 8  — couloir est + salle B
            new Case[14] { w, g, g, g, w, g, w, w, w, w, w, g, w, w },
            // col 9  — salle nord-est
            new Case[14] { w, g, g, g, g, g, g, g, w, w, w, g, g, w },
            // col 10 — salle nord-est + salle sud-est
            new Case[14] { w, w, w, w, w, w, w, g, w, g, g, g, g, w },
            // col 11 — couloir sud
            new Case[14] { w, w, w, w, w, g, g, g, g, g, w, w, g, w },
            // col 12 — salle sud-est
            new Case[14] { w, w, w, w, w, w, g, g, w, g, w, g, g, w },
            // col 13 — salle sud-est
            new Case[14] { w, w, w, w, w, w, w, g, w, g, g, g, g, w },
            // col 14 — bord est
            new Case[14] { w, w, w, w, w, w, w, w, w, w, w, w, w, w },
        };
 
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
 
        int row = start[0];
        int col = start[1];
 
        bool rowValid = row >= 0 && row < gridDefinition.Length;
        bool colValid = rowValid && col >= 0 && col < gridDefinition[row].Length;
 
        if (!rowValid || !colValid)
        {
            Debug.LogError($"[GameManager] La position de départ ({row},{col}) est hors des limites de la grille ({gridDefinition.Length}x{gridDefinition[0].Length}). Remise à (1,1).");
            start[0] = 1;
            start[1] = 1;
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
 
    public int[] GetStart() => start;
    public float GetStep() => step;
    public GameObject GetTerrain() => terrain;
 
    public WallModels GetModels()
    {
        return new WallModels
        {
            ground     = groundModel,
            pillar     = wallPillar,
            straightNS = wallStraightNS,
            straightEW = wallStraightEW,
            cornerNE   = wallCornerNE,
            cornerNW   = wallCornerNW,
            cornerSE   = wallCornerSE,
            cornerSW   = wallCornerSW,
            tNoNorth   = wallTNoNorth,
            tNoEast    = wallTNoEast,
            tNoSouth   = wallTNoSouth,
            tNoWest    = wallTNoWest,
            cross      = wallCross,
            endNorth   = wallEndNorth,
            endEast    = wallEndEast,
            endSouth   = wallEndSouth,
            endWest    = wallEndWest
        };
    }
}
 
// ──────────────────────────────────────────
// Struct de données pour les modèles de murs
// (déplacé dans son propre fichier idéalement, mais gardé ici pour la simplicité)
// ──────────────────────────────────────────
public class WallModels
{
    public GameObject ground;
    public GameObject pillar;
    public GameObject straightNS;
    public GameObject straightEW;
    public GameObject cornerNE;
    public GameObject cornerNW;
    public GameObject cornerSE;
    public GameObject cornerSW;
    public GameObject tNoNorth;
    public GameObject tNoEast;
    public GameObject tNoSouth;
    public GameObject tNoWest;
    public GameObject cross;
    public GameObject endNorth;
    public GameObject endEast;
    public GameObject endSouth;
    public GameObject endWest;
}