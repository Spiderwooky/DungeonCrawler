using UnityEngine;

// Conversion entre coordonnées grille (Vector2Int) et coordonnées monde (Vector3).
// Centralise la formule utilisée par GameManager, EnemyController, PlayerController,
// Inventory et WorldPickup (auparavant dupliquée dans chacun de ces scripts).
public static class GridUtils
{
    public static Vector3 GridToWorld(Vector2Int gridPos, float step)
    {
        return new Vector3(gridPos.x * step, 0f, gridPos.y * step);
    }

    public static Vector2Int WorldToGrid(Vector3 worldPos, float step)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / step),
            Mathf.RoundToInt(worldPos.z / step));
    }
}
