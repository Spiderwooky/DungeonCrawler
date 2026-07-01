using System.Collections.Generic;
using UnityEngine;

// Pose des bestioles d'ambiance (CritterController) sur des cases sol aléatoires du donjon.
// Appelé par GameManager.Awake() après la génération de la grille.
// Les salles Start, End et Boss sont exclues pour ne pas polluer les zones de gameplay clé.
public class CritterSpawner : MonoBehaviour
{
    [Tooltip("Prefab de la bestiole (doit avoir un composant CritterController).")]
    [SerializeField] private GameObject critterPrefab;

    [Tooltip("Nombre total de bestioles à spawner dans le donjon.")]
    [SerializeField] private int totalCritters = 12;

    public void InitializeCritters(GameManager gm, List<RoomInfo> rooms, Vector2Int playerStart)
    {
        if (critterPrefab == null)
        {
            Debug.LogWarning("[CritterSpawner] Critter prefab non assigné.");
            return;
        }

        Case[][] grid = gm.GetGridDefinition();
        float step = gm.GetStep();
        GameObject terrain = gm.GetTerrain();

        // Cases interdites : salle start/end/boss + case du joueur
        var excluded = new HashSet<Vector2Int> { playerStart };
        foreach (RoomInfo room in rooms)
        {
            if (room.Type == RoomType.Start || room.Type == RoomType.End || room.Type == RoomType.Boss)
            {
                foreach (Vector2Int cell in room.Cells)
                    excluded.Add(cell);
            }
        }

        // Liste des cases sol éligibles
        var eligible = new List<Vector2Int>();
        for (int x = 0; x < grid.Length; x++)
        {
            for (int z = 0; z < grid[x].Length; z++)
            {
                var cell = new Vector2Int(x, z);
                if (!excluded.Contains(cell) && grid[x][z] != null && grid[x][z].IsGround())
                    eligible.Add(cell);
            }
        }

        if (eligible.Count == 0) return;

        int count = Mathf.Min(totalCritters, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, eligible.Count);
            Vector2Int cell = eligible[idx];
            eligible.RemoveAt(idx); // évite deux bestioles sur la même case au spawn

            Vector3 worldPos = GridUtils.GridToWorld(cell, step);
            Quaternion rot   = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject obj   = Instantiate(critterPrefab, worldPos, rot, terrain.transform);

            obj.GetComponent<CritterController>()?.Initialize(gm, cell);
        }
    }
}
