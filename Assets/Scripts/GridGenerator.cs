using System.Collections.Generic;
using UnityEngine;

// Instancie le terrain 3D à partir de la grille fournie par GameManager.
// Un seul prefab de sol (FloorTile) est utilisé pour toutes les cases praticables : ses
// décorations de bord (murs pleins / petits coins diagonaux / pilier) sont activées au cas
// par cas selon les voisins, pour donner un rendu organique plutôt qu'une grille de blocs.
// Les cases mur "normales" ne génèrent aucun GameObject.
public class GridGenerator : MonoBehaviour
{
    private Case[][] grid;
    private GameManager gameManager;

    public Case[][] GetGrid() => grid;

    public void GenerateGridAndTerrain(GameManager gm)
    {
        gameManager = gm;
        grid = gameManager.GetGridDefinition();
        GenerateTerrain();
    }

    private void GenerateTerrain()
    {
        if (gameManager == null)
        {
            Debug.LogError("GameManager reference is null in GridGenerator.");
            return;
        }

        if (grid == null)
        {
            Debug.LogError("Grid was not generated.");
            return;
        }

        float step = gameManager.GetStep();
        GameObject terrainParent = gameManager.GetTerrain();
        GameObject floorPrefab = gameManager.GetFloorPrefab();

        if (!ValidateFloorPrefab(floorPrefab)) return;

        HashSet<Vector2Int> presetCells = GetPresetRoomCells();

        for (int i = 0; i < grid.Length; i++)
        {
            for (int j = 0; j < grid[i].Length; j++)
            {
                Case cell = grid[i][j];
                if (cell == null) continue;

                // Les salles pré-faites (Start/End/Boss) ont leur propre modèle (murs/sol
                // inclus), instancié par GameManager : ne pas y poser de tuile procédurale.
                if (presetCells.Contains(new Vector2Int(i, j))) continue;

                // Une case mur isolée (entourée de sol) se reclasse en sol-avec-pilier ;
                // une case mur "normale" ne génère rien du tout.
                bool isPillarCell = !cell.IsGround() && IsIsolatedWallPillar(i, j);
                if (!cell.IsGround() && !isPillarCell) continue;

                Vector3 position = new Vector3(i * step, 0f, j * step);
                GameObject instance = Instantiate(floorPrefab, position, Quaternion.identity, terrainParent.transform);

                ConfigureTile(instance, i, j, isPillarCell);

                // La case connaît maintenant son modèle instancié dans la scène
                cell.SetModel(instance);
            }
        }
    }

    // Retourne l'ensemble exact des cases appartenant aux salles pré-faites (Start/End/Boss).
    // On utilise room.Cells (cases ground exactes) plutôt que room.Bounds (RectInt approximatif)
    // pour éviter tout clipping de texture là où le prefab pose déjà son propre sol.
    private HashSet<Vector2Int> GetPresetRoomCells()
    {
        var cells = new HashSet<Vector2Int>();
        List<RoomInfo> rooms = gameManager.GetRooms();
        if (rooms == null) return cells;

        foreach (RoomInfo room in rooms)
        {
            if (room.Type == RoomType.Start || room.Type == RoomType.End || room.Type == RoomType.Boss)
            {
                foreach (Vector2Int cell in room.Cells)
                    cells.Add(cell);
            }
        }
        return cells;
    }

    // Active les bonnes décorations de bord (murs/coins/pilier) sur la tuile instanciée.
    private void ConfigureTile(GameObject instance, int x, int z, bool hasPillar)
    {
        FloorTile floorTile = instance.GetComponent<FloorTile>();
        if (floorTile == null)
        {
            Debug.LogError($"[GridGenerator] Le prefab de sol n'a pas de composant FloorTile (case {x},{z}).");
            return;
        }

        bool north = IsBlockingWallAt(x, z + 1);
        bool south = IsBlockingWallAt(x, z - 1);
        bool east  = IsBlockingWallAt(x + 1, z);
        bool west  = IsBlockingWallAt(x - 1, z);

        // Un petit coin diagonal ne s'affiche que si aucun des deux murs cardinaux adjacents
        // à cette diagonale n'est déjà plein (sinon il couvre déjà l'angle).
        bool cornerNE = !north && !east && IsBlockingWallAt(x + 1, z + 1);
        bool cornerNW = !north && !west && IsBlockingWallAt(x - 1, z + 1);
        bool cornerSE = !south && !east && IsBlockingWallAt(x + 1, z - 1);
        bool cornerSW = !south && !west && IsBlockingWallAt(x - 1, z - 1);

        floorTile.Configure(north, south, east, west, cornerNE, cornerNW, cornerSE, cornerSW, hasPillar);
    }

    // Un mur "bloquant" doit être affiché chez ses voisins ; un mur isolé (pilier) ne doit
    // pas l'être, puisqu'il se reclasse lui-même en sol-avec-pilier.
    private bool IsBlockingWallAt(int x, int z) => IsWallAt(x, z) && !IsIsolatedWallPillar(x, z);

    // Vrai si la case (x,z) est un mur entouré de sol sur ses 4 côtés cardinaux
    // (même définition que BspDungeonGenerator.PlaceRoomPillars.surroundedByGround).
    private bool IsIsolatedWallPillar(int x, int z)
    {
        if (!IsWallAt(x, z)) return false;

        return IsGroundAt(x, z + 1) && IsGroundAt(x, z - 1)
            && IsGroundAt(x + 1, z) && IsGroundAt(x - 1, z);
    }

    private bool IsWallAt(int x, int z)
    {
        // Hors-grille = mur implicite : l'extérieur du donjon est toujours solide.
        if (x < 0 || z < 0 || x >= grid.Length || z >= grid[x].Length)
            return true;

        Case cell = grid[x][z];
        return cell != null && cell.IsWall();
    }

    // Une case hors-grille ne compte pas comme du sol (elle ne peut donc pas qualifier un mur comme isolé).
    private bool IsGroundAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= grid.Length || z >= grid[x].Length)
            return false;

        Case cell = grid[x][z];
        return cell != null && cell.IsGround();
    }

    private bool ValidateFloorPrefab(GameObject floorPrefab)
    {
        if (floorPrefab == null)
        {
            Debug.LogError("[GridGenerator] Floor prefab manquant sur GameManager.");
            return false;
        }

        if (floorPrefab.GetComponent<FloorTile>() == null)
        {
            Debug.LogError("[GridGenerator] Le floor prefab n'a pas de composant FloorTile.");
            return false;
        }

        return true;
    }
}
