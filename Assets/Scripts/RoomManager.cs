using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la population des salles en ennemis : population initiale des salles de type
/// Monster au lancement de la partie, puis repop des emplacements manquants après que le
/// joueur ait quitté une salle pendant un certain nombre de rounds.
///
/// Initialisé par GameManager (InitializeRooms) une fois la grille et les salles générées.
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Header("Références scène")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform playerTransform;

    [Header("Population des salles")]
    [Tooltip("Prefabs d'ennemis pouvant être instanciés dans les salles de type Monster (un prefab choisi au hasard par spawn).")]
    [SerializeField] private List<GameObject> enemyPrefabs;

    [Tooltip("Distance minimale (en cases) entre un point de spawn et le joueur.")]
    [SerializeField] private int minSpawnDistanceFromPlayer = 2;

    [Tooltip("Marge autour de l'écran (0–0.2) utilisée pour la zone d'exclusion de spawn : " +
             "0 = exactement l'écran, 0.1 = 10 % hors-champ exclus aussi.")]
    [SerializeField] private float cameraSpawnMargin = 0.1f;

    [Header("Respawn")]
    [Tooltip("Nombre de rounds pendant lesquels le joueur doit être absent d'une salle avant qu'elle ne complète ses emplacements manquants jusqu'au maximum.")]
    [SerializeField] private int respawnDelayRounds = 5;

    private int[][] roomIds;
    private List<RoomInfo> rooms;

    private void Start()
    {
        // S'abonner ici (pas Awake) garantit que TurnManager.Instance est déjà assigné,
        // comme le fait CopilotPlayerController pour RegisterPlayer.
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnRoundStarted += HandleRoundStarted;
        else
            Debug.LogWarning("[RoomManager] TurnManager introuvable : le repop par round est désactivé.");
    }

    // Appelé par GameManager une fois la grille et les salles générées. Effectue la
    // population initiale de toutes les salles de type Monster.
    public void InitializeRooms(int[][] dungeonRoomIds, List<RoomInfo> roomList, Vector2Int playerStartCell)
    {
        roomIds = dungeonRoomIds;
        rooms = roomList;

        foreach (RoomInfo room in rooms)
        {
            // Le round de départ vaut 1 (TurnManager.StartGame() l'initialise ainsi).
            room.LastRoundPlayerPresent = 1;

            if (room.Type == RoomType.Monster)
            {
                SpawnMissingEnemies(room, playerStartCell);
            }
        }
    }

    private void HandleRoundStarted(int roundNumber)
    {
        if (rooms == null || playerTransform == null || gameManager == null) return;

        Vector2Int playerCell = GridUtils.WorldToGrid(playerTransform.position, gameManager.GetStep());
        int currentRoomId = GetRoomIdAt(playerCell);

        foreach (RoomInfo room in rooms)
        {
            if (room.Type != RoomType.Monster) continue;

            if (room.Id == currentRoomId)
            {
                room.LastRoundPlayerPresent = roundNumber;
                continue;
            }

            if (roundNumber - room.LastRoundPlayerPresent >= respawnDelayRounds)
            {
                SpawnMissingEnemies(room, playerCell);
            }
        }
    }

    // Complète les emplacements manquants d'une salle (jusqu'à room.MaxEnemies), sans
    // toucher aux ennemis déjà en vie.
    private void SpawnMissingEnemies(RoomInfo room, Vector2Int playerCell)
    {
        room.AliveEnemies.RemoveAll(enemy => enemy == null);

        int deficit = room.MaxEnemies - room.AliveEnemies.Count;
        if (deficit <= 0 || enemyPrefabs == null || enemyPrefabs.Count == 0) return;

        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (Vector2Int cell in room.Cells)
        {
            if (Vector2Int.Distance(cell, playerCell) < minSpawnDistanceFromPlayer) continue;
            if (IsCellOccupied(cell, room)) continue;
            if (IsCellVisibleToCamera(cell)) continue;
            candidates.Add(cell);
        }

        for (int i = 0; i < deficit && candidates.Count > 0; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[index];
            candidates.RemoveAt(index);
            SpawnEnemyAt(cell, room);
        }
    }

    // Retourne true si la case est dans le champ de vision de la caméra (+ marge de sécurité).
    // Utilisé pour empêcher les spawns visibles par le joueur.
    private bool IsCellVisibleToCamera(Vector2Int cell)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 worldPos = GridUtils.GridToWorld(cell, gameManager.GetStep());
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        float m = cameraSpawnMargin;
        return vp.z > 0f
            && vp.x >= -m && vp.x <= 1f + m
            && vp.y >= -m && vp.y <= 1f + m;
    }

    private bool IsCellOccupied(Vector2Int cell, RoomInfo room)
    {
        foreach (EnemyController enemy in room.AliveEnemies)
        {
            if (enemy == null) continue;
            if (GridUtils.WorldToGrid(enemy.transform.position, gameManager.GetStep()) == cell) return true;
        }
        return false;
    }

    private void SpawnEnemyAt(Vector2Int cell, RoomInfo room)
    {
        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        GameObject instance = Instantiate(prefab, GridUtils.GridToWorld(cell, gameManager.GetStep()), Quaternion.identity, transform);

        EnemyController controller = instance.GetComponent<EnemyController>();
        if (controller == null)
        {
            Debug.LogWarning($"[RoomManager] Le prefab '{prefab.name}' n'a pas de composant EnemyController.");
            Destroy(instance);
            return;
        }

        controller.Configure(gameManager, playerTransform);
        controller.SetStartAndPatrol(cell, cell);
        room.AliveEnemies.Add(controller);
    }

    private int GetRoomIdAt(Vector2Int cell)
    {
        if (roomIds == null) return -1;
        if (cell.x < 0 || cell.x >= roomIds.Length) return -1;
        if (cell.y < 0 || cell.y >= roomIds[cell.x].Length) return -1;
        return roomIds[cell.x][cell.y];
    }
}
