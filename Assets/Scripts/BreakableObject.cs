using UnityEngine;

// Objet destructible posé sur la grille (tonneau, caisse…).
// Un bump du joueur le brise : son de bris, drops via LootDropper, destruction du GO.
// Pas de HealthSystem : le joueur le brise en un coup, pas de points de vie.
[RequireComponent(typeof(LootDropper))]
public class BreakableObject : MonoBehaviour
{
    private LootDropper lootDropper;

    private void Awake()
    {
        lootDropper = GetComponent<LootDropper>();
    }

    public void Break()
    {
        AudioManager.Instance?.PlayBarrelBreak();
        lootDropper.Drop(transform.position, playDropSound: false);
        Destroy(gameObject);
    }
}
