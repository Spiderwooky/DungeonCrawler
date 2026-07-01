using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TorchSpawnEntry
{
    public GameObject prefab;
    [Tooltip("Probabilité (0–1) qu'une salle reçoive des torches de ce type.")]
    [Range(0f, 1f)] public float roomSpawnChance = 0.9f;
    public int minPerRoom = 1;
    public int maxPerRoom = 3;
    [Tooltip("Distance minimale en cases entre deux torches d'une même salle.")]
    public int minSpacingCells = 3;
    [Tooltip("Hauteur en unités monde à laquelle la torche est fixée au mur.")]
    public float wallHeight = 1.5f;
}

[System.Serializable]
public class TorchCorridorEntry
{
    public GameObject prefab;
    [Tooltip("Probabilité (0–1) qu'une case couloir valide (sol adjacent à un mur) reçoive une torche.")]
    [Range(0f, 1f)] public float spawnChancePerCell = 0.12f;
    [Tooltip("Distance minimale en cases entre deux torches de couloir.")]
    public int minSpacingCells = 4;
    [Tooltip("Hauteur en unités monde à laquelle la torche est fixée au mur.")]
    public float wallHeight = 1.5f;
}

// Pose automatiquement des torches dans les salles procédurales et les couloirs du donjon.
// Les salles Start et End (prefabs fixes) sont ignorées.
// Initialisé par GameManager.Awake() après la génération du terrain.
public class TorchSpawner : MonoBehaviour
{
    [Header("Salles")]
    [SerializeField] private TorchSpawnEntry[] entries;

    [Header("Couloirs")]
    [Tooltip("Laisser le prefab vide pour désactiver les torches de couloir.")]
    [SerializeField] private TorchCorridorEntry corridorEntry;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public void InitializeTorches(GameManager gm, List<RoomInfo> rooms, Vector2Int playerStart)
    {
        Case[][] grid = gm.GetGridDefinition();
        float step = gm.GetStep();

        // ── Salles ──────────────────────────────────────────
        if (entries != null)
        {
            foreach (RoomInfo room in rooms)
            {
                if (room.Type == RoomType.Start || room.Type == RoomType.End) continue;

                foreach (TorchSpawnEntry entry in entries)
                {
                    if (entry.prefab == null) continue;
                    PlaceTorchesInRoom(room, entry, grid, step);
                }
            }
        }

        // ── Couloirs ─────────────────────────────────────────
        if (corridorEntry != null && corridorEntry.prefab != null)
            PlaceTorchesInCorridors(rooms, corridorEntry, grid, step);
    }

    private void PlaceTorchesInRoom(RoomInfo room, TorchSpawnEntry entry, Case[][] grid, float step)
    {
        if (Random.value > entry.roomSpawnChance) return;

        // Cherche les cases sol de la salle qui ont au moins un voisin mur
        var candidates = new List<(Vector2Int cell, Vector2Int wallDir)>();
        foreach (Vector2Int cell in room.Cells)
        {
            foreach (Vector2Int dir in Directions)
            {
                if (IsWall(cell + dir, grid))
                {
                    candidates.Add((cell, dir));
                    break;
                }
            }
        }

        Shuffle(candidates);

        int count = Random.Range(entry.minPerRoom, entry.maxPerRoom + 1);
        var placed = new List<Vector2Int>();

        foreach (var (cell, wallDir) in candidates)
        {
            if (placed.Count >= count) break;

            // Vérifie la distance minimale avec les torches déjà posées dans cette salle
            bool tooClose = false;
            foreach (Vector2Int p in placed)
            {
                if (GridDistance(cell, p) < entry.minSpacingCells)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // Position : sur la face du mur (case sol + déplacement vers le mur), à hauteur de mur
            Vector3 groundWorldPos = GridUtils.GridToWorld(cell, step);
            Vector3 wallOffset     = new Vector3(wallDir.x, 0f, wallDir.y) * step * 0.45f;
            Vector3 torchPos       = groundWorldPos + wallOffset + Vector3.up * entry.wallHeight;

            // Orientation : face à l'intérieur de la salle (inverse de la direction mur)
            Vector3 facing   = new Vector3(-wallDir.x, 0f, -wallDir.y);
            Quaternion rot   = facing != Vector3.zero
                ? Quaternion.LookRotation(facing)
                : Quaternion.identity;

            Instantiate(entry.prefab, torchPos, rot, transform);
            placed.Add(cell);
        }
    }

    private void PlaceTorchesInCorridors(List<RoomInfo> rooms, TorchCorridorEntry entry, Case[][] grid, float step)
    {
        // Construit un ensemble de toutes les cases appartenant à une salle (pour les exclure)
        var roomCells = new HashSet<Vector2Int>();
        foreach (RoomInfo room in rooms)
            foreach (Vector2Int cell in room.Cells)
                roomCells.Add(cell);

        // Cherche les cases sol hors salle (= couloirs) adjacentes à un mur
        var candidates = new List<(Vector2Int cell, Vector2Int wallDir)>();
        for (int x = 0; x < grid.Length; x++)
        {
            for (int z = 0; z < grid[x].Length; z++)
            {
                var cell = new Vector2Int(x, z);
                if (grid[x][z] == null || grid[x][z].IsWall()) continue;
                if (roomCells.Contains(cell)) continue;

                foreach (Vector2Int dir in Directions)
                {
                    if (IsWall(cell + dir, grid))
                    {
                        candidates.Add((cell, dir));
                        break;
                    }
                }
            }
        }

        Shuffle(candidates);

        var placed = new List<Vector2Int>();
        foreach (var (cell, wallDir) in candidates)
        {
            if (Random.value > entry.spawnChancePerCell) continue;

            bool tooClose = false;
            foreach (Vector2Int p in placed)
            {
                if (GridDistance(cell, p) < entry.minSpacingCells)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Vector3 groundWorldPos = GridUtils.GridToWorld(cell, step);
            Vector3 wallOffset     = new Vector3(wallDir.x, 0f, wallDir.y) * step * 0.45f;
            Vector3 torchPos       = groundWorldPos + wallOffset + Vector3.up * entry.wallHeight;

            Vector3 facing = new Vector3(-wallDir.x, 0f, -wallDir.y);
            Quaternion rot = facing != Vector3.zero ? Quaternion.LookRotation(facing) : Quaternion.identity;

            Instantiate(entry.prefab, torchPos, rot, transform);
            placed.Add(cell);
        }
    }

    private static bool IsWall(Vector2Int cell, Case[][] grid)
    {
        if (cell.x < 0 || cell.x >= grid.Length) return true;
        if (cell.y < 0 || cell.y >= grid[cell.x].Length) return true;
        Case c = grid[cell.x][cell.y];
        return c == null || c.IsWall();
    }

    private static int GridDistance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
