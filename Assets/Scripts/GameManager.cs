using UnityEngine;
 
public class GameManager : MonoBehaviour
{
    [SerializeField]
    private GridGenerator gridGenerator;
 
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
 
        GenerateGridDefinition();
        gridGenerator.GenerateGridAndTerrain(this);
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