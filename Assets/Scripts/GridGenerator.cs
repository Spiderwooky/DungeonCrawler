using UnityEngine;
 
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
        WallModels models = gameManager.GetModels();
 
        ValidateModels(models);
 
        for (int i = 0; i < grid.Length; i++)
        {
            for (int j = 0; j < grid[i].Length; j++)
            {
                Case cell = grid[i][j];
                if (cell == null) continue;
 
                GameObject prefab = GetPrefabForCell(cell, i, j, models);
                if (prefab == null)
                {
                    Debug.LogWarning($"Aucun prefab trouvé pour la case ({i},{j}). Utilisation du sol par défaut.");
                    prefab = models.ground;
                }
 
                Vector3 position = new Vector3(i * step, 0f, j * step);
                GameObject instance = Instantiate(prefab, position, Quaternion.identity, terrainParent.transform);
 
                // La case connaît maintenant son modèle instancié dans la scène
                cell.SetModel(instance);
            }
        }
    }
 
    // Choisit le prefab approprié selon le type de cellule et ses voisins murs.
    private GameObject GetPrefabForCell(Case cell, int x, int z, WallModels models)
    {
        if (!cell.IsWall())
        {
            return models.ground;
        }
 
        bool north = IsWallAt(x, z + 1);
        bool south = IsWallAt(x, z - 1);
        bool east  = IsWallAt(x + 1, z);
        bool west  = IsWallAt(x - 1, z);
 
        int neighborCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
 
        switch (neighborCount)
        {
            case 0:
                return models.pillar;
 
            case 1:
                if (north) return models.endNorth;
                if (south) return models.endSouth;
                if (east)  return models.endEast;
                return models.endWest;
 
            case 2:
                if (north && south) return models.straightNS;
                if (east  && west)  return models.straightEW;
                if (north && east)  return models.cornerNE;
                if (east  && south) return models.cornerSE;
                if (south && west)  return models.cornerSW;
                if (west  && north) return models.cornerNW;
                return models.pillar; // fallback explicite (ne devrait pas arriver)
 
            case 3:
                if (!north) return models.tNoNorth;
                if (!east)  return models.tNoEast;
                if (!south) return models.tNoSouth;
                return models.tNoWest; // !west implicite, return explicite
 
            case 4:
                return models.cross;
 
            default:
                return models.pillar;
        }
    }
 
    private bool IsWallAt(int x, int z)
    {
        if (x < 0 || z < 0 || x >= grid.Length || z >= grid[x].Length)
            return false;
 
        Case cell = grid[x][z];
        return cell != null && cell.IsWall();
    }
 
    // Vérifie en amont que tous les prefabs obligatoires sont assignés dans l'Inspector.
    private void ValidateModels(WallModels models)
    {
        if (models.ground     == null) Debug.LogError("[GridGenerator] Prefab 'ground' manquant !");
        if (models.pillar     == null) Debug.LogError("[GridGenerator] Prefab 'pillar' manquant !");
        if (models.straightNS == null) Debug.LogError("[GridGenerator] Prefab 'straightNS' manquant !");
        if (models.straightEW == null) Debug.LogError("[GridGenerator] Prefab 'straightEW' manquant !");
        if (models.cornerNE   == null) Debug.LogError("[GridGenerator] Prefab 'cornerNE' manquant !");
        if (models.cornerNW   == null) Debug.LogError("[GridGenerator] Prefab 'cornerNW' manquant !");
        if (models.cornerSE   == null) Debug.LogError("[GridGenerator] Prefab 'cornerSE' manquant !");
        if (models.cornerSW   == null) Debug.LogError("[GridGenerator] Prefab 'cornerSW' manquant !");
        if (models.tNoNorth   == null) Debug.LogError("[GridGenerator] Prefab 'tNoNorth' manquant !");
        if (models.tNoEast    == null) Debug.LogError("[GridGenerator] Prefab 'tNoEast' manquant !");
        if (models.tNoSouth   == null) Debug.LogError("[GridGenerator] Prefab 'tNoSouth' manquant !");
        if (models.tNoWest    == null) Debug.LogError("[GridGenerator] Prefab 'tNoWest' manquant !");
        if (models.cross      == null) Debug.LogError("[GridGenerator] Prefab 'cross' manquant !");
        if (models.endNorth   == null) Debug.LogError("[GridGenerator] Prefab 'endNorth' manquant !");
        if (models.endEast    == null) Debug.LogError("[GridGenerator] Prefab 'endEast' manquant !");
        if (models.endSouth   == null) Debug.LogError("[GridGenerator] Prefab 'endSouth' manquant !");
        if (models.endWest    == null) Debug.LogError("[GridGenerator] Prefab 'endWest' manquant !");
    }
}