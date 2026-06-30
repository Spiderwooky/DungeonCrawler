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

    [Tooltip("Gère la population des salles en ennemis (population initiale + repop).")]
    [SerializeField]
    private RoomManager roomManager;

    [Tooltip("Fait apparaître les marchandes dans les salles vides du donjon généré. Optionnel.")]
    [SerializeField]
    private MerchantSpawner merchantSpawner;

    [Tooltip("Pose des objets cassables (tonneaux, caisses…) dans les salles procédurales. Optionnel.")]
    [SerializeField]
    private BreakableSpawner breakableSpawner;

    // Case de départ du joueur, en coordonnées grille (x, z).
    // Recalculée automatiquement dans AssignStartPositionFromStartRoom() au lancement.
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

    [Header("Salles spéciales (pré-faites)")]
    [Tooltip("Prefab instancié à l'emplacement de la salle de départ (motif fixe StartRoomPattern dans BspDungeonGenerator). Inclut son propre sol/murs/porte : GridGenerator ne pose pas de tuile procédurale sur ses cases.")]
    [SerializeField]
    private GameObject startRoomPrefab;

    [Tooltip("Prefab instancié à l'emplacement de la salle de fin (motif fixe EndRoomPattern dans BspDungeonGenerator). Même principe que Start Room Prefab.")]
    [SerializeField]
    private GameObject endRoomPrefab;

    [Header("Clé de fin de donjon")]
    [Tooltip("Objet nécessaire pour franchir la porte de la salle de fin. Apparaît au sol dans la salle de départ au lancement. CopilotPlayerController lit cette même référence (GetKeyItemData) pour vérifier l'inventaire du joueur.")]
    [SerializeField]
    private ItemData keyItemData;

    private Case[][] gridDefinition;

    // Direction (case porte - case de départ) vers laquelle orienter le joueur au lancement,
    // calculée dans AssignStartPositionFromStartRoom(). Null si la salle de départ n'a pas de
    // porte connue (repli sur l'ancienne heuristique de placement).
    private Vector2Int? startFacing;
 
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
        // Le joueur démarre dans la salle de départ (jamais de monstres ni d'objets).
        AssignStartPositionFromStartRoom();

        if (roomManager != null)
        {
            roomManager.InitializeRooms(bspGenerator.GetRoomIds(), bspGenerator.GetRooms(), start);
        }
        else
        {
            Debug.LogWarning("[GameManager] RoomManager non assigné : les salles ne seront pas peuplées d'ennemis.");
        }

        if (merchantSpawner != null)
        {
            merchantSpawner.InitializeMerchants(this, bspGenerator.GetRooms(), start);
        }

        gridGenerator.GenerateGridAndTerrain(this);
        SpawnPresetRoomPrefabs();
        SpawnKeyPickup();

        if (breakableSpawner != null)
            breakableSpawner.InitializeBreakables(this, bspGenerator.GetRooms(), start);
    }

    // Fait apparaître la clé de fin de donjon au sol dans la salle de départ (sur une case
    // différente de celle où le joueur démarre, pour qu'il doive la ramasser explicitement).
    private void SpawnKeyPickup()
    {
        if (keyItemData == null) return;

        System.Collections.Generic.List<RoomInfo> rooms = bspGenerator.GetRooms();
        RoomInfo startRoom = rooms.Find(r => r.Type == RoomType.Start);
        if (startRoom == null || startRoom.Cells.Count == 0) return;

        Vector2Int cell = startRoom.Cells[0];
        foreach (Vector2Int c in startRoom.Cells)
        {
            if (c != start)
            {
                cell = c;
                break;
            }
        }

        WorldPickup.Spawn(keyItemData, 1, GridUtils.GridToWorld(cell, step), playDropSound: false);
    }

    // Instancie les prefabs de salles pré-faites (Start/End) à l'emplacement choisi par
    // BspDungeonGenerator. Pas d'effet si le prefab correspondant n'est pas assigné, ou si
    // la salle n'a pas pu être placée (grille trop petite : voir les avertissements de
    // BspDungeonGenerator).
    private void SpawnPresetRoomPrefabs()
    {
        System.Collections.Generic.List<RoomInfo> rooms = bspGenerator.GetRooms();
        SpawnPresetRoomPrefab(rooms, RoomType.Start, startRoomPrefab);
        SpawnPresetRoomPrefab(rooms, RoomType.End, endRoomPrefab);
    }

    private void SpawnPresetRoomPrefab(System.Collections.Generic.List<RoomInfo> rooms, RoomType type, GameObject prefab)
    {
        if (prefab == null) return;

        RoomInfo room = rooms.Find(r => r.Type == type);
        if (room == null) return;

        // Origine = coin sud-ouest de la salle (case xMin,yMin = case en bas à gauche du
        // motif ASCII) : le prefab doit avoir son pivot sur cette case, pas au centre.
        RectInt bounds = room.Bounds;
        Vector3 worldPos = new Vector3(bounds.xMin * step, 0f, bounds.yMin * step);

        Instantiate(prefab, worldPos, Quaternion.identity, terrain.transform);
    }

    // Place le point de départ du joueur dans la salle taguée RoomType.Start par le générateur,
    // juste devant sa porte (si le motif pré-fait en a une) en l'orientant vers l'intérieur
    // de la salle (dos à la porte).
    private void AssignStartPositionFromStartRoom()
    {
        if (gridDefinition == null) return;

        System.Collections.Generic.List<RoomInfo> rooms = bspGenerator.GetRooms();
        RoomInfo startRoom = rooms.Find(room => room.Type == RoomType.Start);

        if (startRoom == null || startRoom.Cells.Count == 0)
        {
            Debug.LogWarning("[GameManager] Aucune salle de départ trouvée : le joueur démarre en (1,1).");
            start = new Vector2Int(1, 1);
            startFacing = null;
            return;
        }

        if (startRoom.DoorAdjacentCell.HasValue && startRoom.DoorCell.HasValue)
        {
            start = startRoom.DoorAdjacentCell.Value;
            startFacing = startRoom.DoorAdjacentCell.Value - startRoom.DoorCell.Value;
            return;
        }

        Vector2 center = startRoom.Bounds.center;
        start = FindNearestGround(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y), startRoom.Cells);
        startFacing = null;
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
    public Vector2Int? GetStartFacing() => startFacing;
    public float GetStep() => step;
    public GameObject GetTerrain() => terrain;
    public GameObject GetFloorPrefab() => floorPrefab;
    public System.Collections.Generic.List<RoomInfo> GetRooms() => bspGenerator != null ? bspGenerator.GetRooms() : null;
    public ItemData GetKeyItemData() => keyItemData;

    // Case de la porte de la salle de fin, ou null si la salle de fin n'a pas pu être placée.
    public Vector2Int? GetEndDoorCell()
    {
        System.Collections.Generic.List<RoomInfo> rooms = GetRooms();
        RoomInfo endRoom = rooms?.Find(r => r.Type == RoomType.End);
        return endRoom?.DoorCell;
    }
}