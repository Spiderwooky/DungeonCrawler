using UnityEngine;

// Pièce posée sur le prefab de sol unique : contient les 9 décorations de bord possibles
// (4 murs pleins, 4 petits coins diagonaux, 1 pilier central), toutes désactivées par défaut
// dans le prefab. GridGenerator appelle Configure() après l'instanciation pour n'activer que
// les pièces pertinentes selon les voisins de la case dans la grille.
public class FloorTile : MonoBehaviour
{
    [Header("Murs pleins")]
    [SerializeField] private GameObject wallNorth;
    [SerializeField] private GameObject wallSouth;
    [SerializeField] private GameObject wallEast;
    [SerializeField] private GameObject wallWest;

    [Header("Petits coins diagonaux")]
    [SerializeField] private GameObject cornerNE;
    [SerializeField] private GameObject cornerNW;
    [SerializeField] private GameObject cornerSE;
    [SerializeField] private GameObject cornerSW;

    [Header("Pilier")]
    [SerializeField] private GameObject pillar;

    public void Configure(
        bool north, bool south, bool east, bool west,
        bool cornerNorthEast, bool cornerNorthWest, bool cornerSouthEast, bool cornerSouthWest,
        bool hasPillar)
    {
        SetActive(wallNorth, north);
        SetActive(wallSouth, south);
        SetActive(wallEast, east);
        SetActive(wallWest, west);

        SetActive(cornerNE, cornerNorthEast);
        SetActive(cornerNW, cornerNorthWest);
        SetActive(cornerSE, cornerSouthEast);
        SetActive(cornerSW, cornerSouthWest);

        SetActive(pillar, hasPillar);
    }

    private static void SetActive(GameObject piece, bool active)
    {
        if (piece != null) piece.SetActive(active);
    }
}
