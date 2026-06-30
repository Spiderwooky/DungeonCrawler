using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BreakableSpawnEntry
{
    public GameObject prefab;
    [Range(0f, 1f)]
    [Tooltip("Probabilité que cet objet apparaisse dans une salle donnée.")]
    public float roomSpawnChance = 0.7f;
    [Tooltip("Nombre d'exemplaires par salle si la salle est sélectionnée (tirage aléatoire entre min et max).")]
    public int minPerRoom = 1;
    public int maxPerRoom = 2;
}

// Pose des objets cassables (tonneaux, caisses…) dans les salles procédurales du donjon.
// Suit le même pattern que MerchantSpawner : référencé par GameManager, appelé dans Awake().
// Ajouter des entrées dans l'Inspector pour supporter d'autres types d'objets cassables.
public class BreakableSpawner : MonoBehaviour
{
    [SerializeField] private BreakableSpawnEntry[] entries;

    public void InitializeBreakables(GameManager gameManager, List<RoomInfo> rooms, Vector2Int playerStart)
    {
        if (entries == null || entries.Length == 0) return;

        float step = gameManager.GetStep();
        Transform parent = gameManager.GetTerrain().transform;

        // Cases déjà occupées (joueur au départ) — étendues au fur et à mesure des poses.
        var blocked = new HashSet<Vector2Int> { playerStart };

        foreach (RoomInfo room in rooms)
        {
            // Salles pré-faites : exclues (elles ont leur propre disposition fixe).
            if (room.Type == RoomType.Start || room.Type == RoomType.End) continue;
            if (room.Cells == null || room.Cells.Count == 0) continue;

            // Copie des cases libres de la salle (modifiée localement à chaque pose).
            var available = new List<Vector2Int>();
            foreach (Vector2Int c in room.Cells)
                if (!blocked.Contains(c)) available.Add(c);

            foreach (BreakableSpawnEntry entry in entries)
            {
                if (entry.prefab == null) continue;
                if (Random.value > entry.roomSpawnChance) continue;

                int count = Random.Range(entry.minPerRoom, entry.maxPerRoom + 1);
                for (int i = 0; i < count && available.Count > 0; i++)
                {
                    int idx = Random.Range(0, available.Count);
                    Vector2Int cell = available[idx];
                    available.RemoveAt(idx);
                    blocked.Add(cell);

                    Vector3 worldPos = GridUtils.GridToWorld(cell, step);
                    // Rotation aléatoire sur Y (0/90/180/270°) pour éviter que tous les
                    // objets soient orientés de la même façon.
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
                    Instantiate(entry.prefab, worldPos, rot, parent);
                }
            }
        }
    }
}
