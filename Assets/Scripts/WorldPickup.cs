using UnityEngine;

/// <summary>
/// Objet ramassable posé sur une case de la grille.
/// Utilise start (coordonnées grille) comme le joueur et les ennemis.
/// </summary>
public class WorldPickup : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    [Header("Références scène")]
    [SerializeField] private GameManager gameManager;

    [Header("Position de départ")]
    [Tooltip("Position initiale en coordonnées grille (x = colonne, y = ligne).")]
    [SerializeField] private Vector2Int start;

    private Vector2Int gridPosition;
    private bool registered;

    public ItemData ItemData => itemData;
    public int Amount => amount;

    private void Start()
    {
        if (itemData == null)
        {
            Debug.LogError($"[WorldPickup] {name} : ItemData non assigné.");
            enabled = false;
            return;
        }

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        gridPosition = start;
        SnapToGrid();
        EnsureVisual();
        EnsurePickupManagerExists();
        RegisterAtGridPosition();
    }

    private void OnDestroy()
    {
        if (registered)
            PickupManager.Instance?.Unregister(gridPosition, this);
    }

    public void Collect()
    {
        if (registered)
        {
            PickupManager.Instance?.Unregister(gridPosition, this);
            registered = false;
        }
        Destroy(gameObject);
    }

    public static WorldPickup Spawn(ItemData data, int spawnAmount, Vector3 worldPosition, bool playDropSound = true)
    {
        if (data == null) return null;

        GameObject go;
        if (data.worldPickupPrefab != null)
        {
            // Conserver la rotation d'origine du prefab (ex: objet incliné pour paraître posé
            // au sol) plutôt que de la réinitialiser à plat.
            go = Instantiate(data.worldPickupPrefab, worldPosition, data.worldPickupPrefab.transform.rotation);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Pickup_{data.itemName}";
            go.transform.position = worldPosition + Vector3.up * 0.5f;
            go.transform.localScale = Vector3.one * 0.6f;
            Object.Destroy(go.GetComponent<Collider>());
        }

        WorldPickup pickup = go.GetComponent<WorldPickup>();
        if (pickup == null)
            pickup = go.AddComponent<WorldPickup>();

        pickup.itemData = data;
        pickup.amount = Mathf.Max(1, spawnAmount);

        if (pickup.gameManager == null)
            pickup.gameManager = Object.FindFirstObjectByType<GameManager>();

        pickup.gridPosition = pickup.WorldToGrid(worldPosition);
        pickup.start = pickup.gridPosition;
        pickup.SnapToGrid();
        WorldPickup.EnsurePickupManagerExists();
        pickup.RegisterAtGridPosition();
        // Jouer un son de dépôt quand un objet est spawn dans le monde (mais pas pour les
        // objets placés par la génération de niveau, ex: la clé dans la salle de départ).
        if (playDropSound)
            AudioManager.EnsureInstance()?.PlayDropItem();
        return pickup;
    }

    private void RegisterAtGridPosition()
    {
        if (itemData == null) return;

        EnsurePickupManagerExists();
        PickupManager.Instance.Register(this, gridPosition);
        registered = true;
    }

    private void SnapToGrid()
    {
        if (gameManager == null) return;
        transform.position = GridToWorld(gridPosition);
    }

    private Vector3 GridToWorld(Vector2Int gridPos) => GridUtils.GridToWorld(gridPos, gameManager.GetStep());

    private Vector2Int WorldToGrid(Vector3 worldPos) => GridUtils.WorldToGrid(worldPos, gameManager.GetStep());

    private void EnsureVisual()
    {
        if (GetComponentInChildren<Renderer>() != null) return;

        if (itemData != null && itemData.worldPickupPrefab != null)
        {
            GameObject visual = Instantiate(itemData.worldPickupPrefab, transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            return;
        }

        GameObject fallbackVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallbackVisual.name = "Visual";
        fallbackVisual.transform.SetParent(transform, false);
        fallbackVisual.transform.localPosition = Vector3.up * 0.5f;
        fallbackVisual.transform.localScale = Vector3.one * 0.6f;
        Destroy(fallbackVisual.GetComponent<Collider>());
    }

    private static void EnsurePickupManagerExists()
    {
        if (PickupManager.Instance != null) return;
        new GameObject("PickupManager").AddComponent<PickupManager>();
    }
}
