using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Algorithme A* stateless sur la grille Case[][].
/// N'est pas un MonoBehaviour : s'utilise comme une classe utilitaire statique.
/// 
/// Usage :
///   List&lt;Vector2Int&gt; path = Pathfinder.FindPath(grid, start, goal);
/// Retourne null si aucun chemin n'existe.
/// </summary>
public static class Pathfinder
{
    // Nœud interne pour A*
    private class Node
    {
        public Vector2Int Position;
        public Node       Parent;
        public float      G; // Coût depuis le départ
        public float      H; // Heuristique (distance Manhattan vers le but)
        public float      F => G + H;

        public Node(Vector2Int pos, Node parent, float g, float h)
        {
            Position = pos;
            Parent   = parent;
            G        = g;
            H        = h;
        }
    }

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,
    };

    /// <summary>
    /// Cherche un chemin de <paramref name="start"/> à <paramref name="goal"/> dans la grille.
    /// </summary>
    /// <param name="grid">Grille de cases.</param>
    /// <param name="start">Case de départ (coordonnées grille).</param>
    /// <param name="goal">Case d'arrivée (coordonnées grille).</param>
    /// <param name="includeGoalEvenIfWall">
    ///   Si true, le chemin peut se terminer sur un mur (utile pour s'arrêter adjacent à un ennemi/joueur).
    /// </param>
    /// <param name="blockedCells">
    ///   Cases supplémentaires traitées comme infranchissables (ex: cases occupées par d'autres
    ///   ennemis), en plus des murs. Permet de chercher un chemin alternatif qui les évite.
    /// </param>
    /// <returns>Liste ordonnée de positions de cases, ou null si introuvable.</returns>
    public static List<Vector2Int> FindPath(
        Case[][] grid,
        Vector2Int start,
        Vector2Int goal,
        bool includeGoalEvenIfWall = false,
        ICollection<Vector2Int> blockedCells = null)
    {
        if (grid == null) return null;
        if (start == goal) return new List<Vector2Int> { start };

        var openSet   = new List<Node>();
        var closedSet = new HashSet<Vector2Int>();

        openSet.Add(new Node(start, null, 0f, Heuristic(start, goal)));

        while (openSet.Count > 0)
        {
            // Nœud avec le F le plus bas
            Node current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
                if (openSet[i].F < current.F) current = openSet[i];

            openSet.Remove(current);
            closedSet.Add(current.Position);

            if (current.Position == goal)
                return ReconstructPath(current);

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighborPos = current.Position + dir;

                if (closedSet.Contains(neighborPos)) continue;
                if (!IsInBounds(grid, neighborPos))  continue;
                if (blockedCells != null && blockedCells.Contains(neighborPos)) continue;

                bool isGoal    = neighborPos == goal;
                bool isWalkable = IsWalkable(grid, neighborPos);

                // On accepte la case goal même si c'est un mur (pour s'arrêter à côté)
                if (!isWalkable && !(isGoal && includeGoalEvenIfWall)) continue;

                float g = current.G + 1f;
                float h = Heuristic(neighborPos, goal);

                Node existing = openSet.Find(n => n.Position == neighborPos);
                if (existing != null)
                {
                    if (g < existing.G)
                    {
                        existing.G      = g;
                        existing.Parent = current;
                    }
                }
                else
                {
                    openSet.Add(new Node(neighborPos, current, g, h));
                }
            }
        }

        return null; // Aucun chemin trouvé
    }

    // ──────────────────────────────────────────
    // Utilitaires
    // ──────────────────────────────────────────

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        var path = new List<Vector2Int>();
        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    // Distance Manhattan (idéale pour une grille 4-directionnelle)
    private static float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static bool IsInBounds(Case[][] grid, Vector2Int pos)
        => pos.x >= 0 && pos.x < grid.Length
        && pos.y >= 0 && pos.y < grid[pos.x].Length;

    private static bool IsWalkable(Case[][] grid, Vector2Int pos)
    {
        Case cell = grid[pos.x][pos.y];
        return cell != null && !cell.IsWall();
    }

    /// <summary>
    /// Retourne toutes les cases praticables accessibles à distance &lt;= range depuis origin,
    /// utile pour la zone de patrouille.
    /// </summary>
    public static List<Vector2Int> GetReachableCells(Case[][] grid, Vector2Int origin, int range)
    {
        var result  = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue   = new Queue<(Vector2Int pos, int dist)>();

        queue.Enqueue((origin, 0));
        visited.Add(origin);

        while (queue.Count > 0)
        {
            var (pos, dist) = queue.Dequeue();
            result.Add(pos);

            if (dist >= range) continue;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = pos + dir;
                if (visited.Contains(next))       continue;
                if (!IsInBounds(grid, next))      continue;
                if (!IsWalkable(grid, next))       continue;

                visited.Add(next);
                queue.Enqueue((next, dist + 1));
            }
        }

        return result;
    }
}