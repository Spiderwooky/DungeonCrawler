using UnityEngine;
 
// Représente le type d'une case de la grille.
public enum CellType
{
    Ground, // Case praticable
    Wall,   // Mur
}
 
public class Case
{
    private CellType cellType;
    private GameObject model; // Le GameObject instancié dans la scène pour cette case
 
    public Case(CellType cellType)
    {
        this.cellType = cellType;
        this.model = null;
    }
 
    // Appelé par GridGenerator après l'Instantiate pour que la case connaisse son modèle
    public void SetModel(GameObject model)
    {
        this.model = model;
    }
 
    public GameObject GetModel()
    {
        return this.model;
    }
 
    public CellType GetCellType()
    {
        return this.cellType;
    }
 
    public bool IsWall()
    {
        return this.cellType == CellType.Wall;
    }
 
    public bool IsGround()
    {
        return this.cellType == CellType.Ground;
    }
}