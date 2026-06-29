using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fait apparaître des marchandes dans des salles de type Empty du donjon généré
/// procéduralement, à une distance minimale du point de départ du joueur. Initialisé
/// par GameManager une fois la grille et les salles générées (même timing que
/// RoomManager.InitializeRooms) — un placement à la main dans la scène ne fonctionnerait
/// pas puisque le donjon change de forme à chaque partie.
/// </summary>
public class MerchantSpawner : MonoBehaviour
{
    [Tooltip("Prefabs de marchande pouvant être instanciés (un prefab choisi au hasard par spawn). Chaque prefab doit avoir un composant Merchant avec un MerchantData assigné.")]
    [SerializeField] private List<GameObject> merchantPrefabs;

    [Tooltip("Nombre de marchandes à faire apparaître dans le donjon.")]
    [SerializeField] private int merchantCount = 1;

    [Tooltip("Distance minimale (en cases) entre un point de spawn et le point de départ du joueur.")]
    [SerializeField] private int minSpawnDistanceFromStart = 3;

    // Appelé par GameManager une fois la grille et les salles générées.
    public void InitializeMerchants(GameManager gameManager, List<RoomInfo> rooms, Vector2Int playerStartCell)
    {
        if (merchantPrefabs == null || merchantPrefabs.Count == 0 || rooms == null) return;

        // emptyRoomChance (BspDungeonGenerator) n'est que de 15% par salle : un petit donjon
        // peut tout à fait n'avoir aucune salle Empty à une génération donnée. On retombe
        // alors sur les salles Monster plutôt que de ne jamais faire apparaître personne.
        List<RoomInfo> candidateRooms = GetCandidateRooms(rooms, RoomType.Empty);
        if (candidateRooms.Count == 0)
        {
            Debug.Log("[MerchantSpawner] Aucune salle 'Empty' dans ce donjon : repli sur les salles 'Monster'.");
            candidateRooms = GetCandidateRooms(rooms, RoomType.Monster);
        }

        if (candidateRooms.Count == 0)
        {
            Debug.LogWarning("[MerchantSpawner] Aucune salle disponible pour faire apparaître une marchande.");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < merchantCount && candidateRooms.Count > 0; i++)
        {
            int roomIndex = Random.Range(0, candidateRooms.Count);
            RoomInfo room = candidateRooms[roomIndex];
            candidateRooms.RemoveAt(roomIndex);

            Vector2Int? cell = FindSpawnCell(room, playerStartCell);
            if (cell == null) continue;

            GameObject prefab = merchantPrefabs[Random.Range(0, merchantPrefabs.Count)];
            Instantiate(prefab, GridUtils.GridToWorld(cell.Value, gameManager.GetStep()), Quaternion.identity, transform);
            spawned++;
        }

        if (spawned == 0)
            Debug.LogWarning("[MerchantSpawner] Aucune marchande placée : pas de case assez loin du départ dans les salles candidates.");
    }

    private static List<RoomInfo> GetCandidateRooms(List<RoomInfo> rooms, RoomType type)
    {
        var list = new List<RoomInfo>();
        foreach (RoomInfo room in rooms)
            if (room.Type == type && room.Cells.Count > 0)
                list.Add(room);
        return list;
    }

    private Vector2Int? FindSpawnCell(RoomInfo room, Vector2Int playerStartCell)
    {
        var candidates = new List<Vector2Int>();
        foreach (Vector2Int cell in room.Cells)
        {
            if (Vector2Int.Distance(cell, playerStartCell) < minSpawnDistanceFromStart) continue;
            candidates.Add(cell);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }
}
