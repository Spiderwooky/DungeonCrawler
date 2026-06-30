using UnityEngine;

// Composant commun aux ennemis et aux objets de la scène (coffres, barils…).
// S'il trouve un HealthSystem sur son GameObject, se branche automatiquement sur OnDeath.
// Pour les objets sans HealthSystem, appeler Drop() manuellement.
public class LootDropper : MonoBehaviour
{
    [SerializeField] private LootTable lootTable;

    private static readonly Vector2Int[] SpreadOrder =
    {
        Vector2Int.zero,
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    private void Awake()
    {
        HealthSystem health = GetComponent<HealthSystem>();
        if (health != null)
            health.OnDeath += () => Drop(transform.position);
    }

    // playDropSound = false pour les sources qui ont déjà un son propre (ex: bris de tonneau).
    public void Drop(Vector3 worldPosition, bool playDropSound = true)
    {
        if (lootTable == null) return;

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        float step = gm != null ? gm.GetStep() : 5f;
        Case[][] grid = gm?.GetGridDefinition();
        Vector2Int origin = GridUtils.WorldToGrid(worldPosition, step);

        foreach (var (item, amount) in lootTable.Roll())
        {
            Vector2Int cell = FindFreeCell(origin, grid);
            WorldPickup.Spawn(item, amount, GridUtils.GridToWorld(cell, step), playDropSound, launchFrom: worldPosition);
        }
    }

    // Retourne la première case libre (walkable + sans pickup) en partant de l'origine,
    // puis les 4 voisins cardinaux. Chaque Spawn() enregistre immédiatement dans
    // PickupManager, donc les itérations suivantes voient les cases déjà prises.
    private static Vector2Int FindFreeCell(Vector2Int origin, Case[][] grid)
    {
        foreach (Vector2Int offset in SpreadOrder)
        {
            Vector2Int cell = origin + offset;

            if (grid != null)
            {
                if (cell.x < 0 || cell.x >= grid.Length) continue;
                if (cell.y < 0 || cell.y >= grid[cell.x].Length) continue;
                if (grid[cell.x][cell.y] == null || grid[cell.x][cell.y].IsWall()) continue;
            }

            if (PickupManager.Instance == null || !PickupManager.Instance.HasPickupAt(cell))
                return cell;
        }

        return origin;
    }
}
