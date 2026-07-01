using UnityEngine;

// Marqueur posé sur chaque enfant mur/coin/pilier d'un FloorTile.
// La direction de face est déduite automatiquement du centre des bounds du renderer
// par rapport à la position du FloorTile parent — aucun réglage Inspector nécessaire.
public class WallOccluder : MonoBehaviour
{
    private Renderer[] _renderers;
    private Vector3    _facingDirection;
    private bool       _facingComputed;

    // Lazy-init identique à l'original : fonctionne quel que soit l'ordre d'activation.
    public Renderer[] Renderers      => _renderers ??= GetComponentsInChildren<Renderer>(true);
    public Vector3   FacingDirection => _facingComputed ? _facingDirection : ComputeAndCache();

    private Vector3 ComputeAndCache()
    {
        _facingComputed = true;

        Transform floorTile = FindFloorTileAncestor();
        if (floorTile == null) { return _facingDirection = Vector3.zero; }

        // Le mesh du mur est positionné au bord de la tuile (Nord, Sud, Est ou Ouest).
        // Le centre des bounds est donc décalé dans cette direction par rapport au FloorTile.
        foreach (Renderer r in Renderers)
        {
            if (r == null) continue;

            Vector3 offset = r.bounds.center - floorTile.position;
            offset.y = 0f; // ignorer la hauteur

            float ax = Mathf.Abs(offset.x);
            float az = Mathf.Abs(offset.z);

            if (Mathf.Max(ax, az) < 0.3f) continue; // centre de tuile (pilier)

            // La normale sortante du mur est dans le même sens que l'offset
            // (wallNorth est en +Z → sa normale pointe vers +Z = Vector3.forward)
            _facingDirection = ax >= az
                ? (offset.x > 0f ? Vector3.right : Vector3.left)
                : (offset.z > 0f ? Vector3.forward : Vector3.back);
            return _facingDirection;
        }

        return _facingDirection = Vector3.zero; // coin/pilier centré → toujours masqué
    }

    private Transform FindFloorTileAncestor()
    {
        Transform t = transform.parent;
        while (t != null)
        {
            if (t.GetComponent<FloorTile>() != null) return t;
            t = t.parent;
        }
        return null;
    }
}
