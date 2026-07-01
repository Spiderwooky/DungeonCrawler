using UnityEngine;

/// <summary>
/// ScriptableObject définissant un type d'ennemi.
/// Créer via : clic droit dans le Project > Create > Enemy > Enemy Data
/// Exemples : Goblin.asset, Troll.asset, Squelette.asset…
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identité")]
    [Tooltip("Nom affiché en jeu.")]
    public string enemyName = "Ennemi";

    [Tooltip("Description courte (lore, journal du joueur…).")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("Prefab 3D instancié dans la scène pour cet ennemi.")]
    public GameObject model;

    [Header("Stats")]
    [Tooltip("Points de vie maximum.")]
    [Min(1)] public int maxHealth = 10;

    [Tooltip("Dégâts infligés par attaque.")]
    [Min(0)] public int attackDamage = 2;

    [Header("Déplacement")]
    [Tooltip("Nombre de cases parcourues par tour (1 = une case par tour).")]
    [Min(1)] public int moveRange = 1;

    [Header("Détection")]
    [Tooltip("Rayon en nombre de cases au-delà duquel l'ennemi ne voit pas le joueur.")]
    [Min(1)] public int detectionRadius = 4;

    [Tooltip("Rayon en nombre de cases à partir duquel l'ennemi peut attaquer.")]
    [Min(1)] public int attackRadius = 1;

    [Header("Type")]
    [Tooltip("Si vrai, la barre de vie est toujours visible dès le spawn (réservé aux ennemis spéciaux/boss).")]
    public bool isSpecialEnemy = false;

    [Header("Performance")]
    [Tooltip("Distance (en cases) au-delà de laquelle l'ennemi ignore complètement son tour (pas de calcul d'état, pas d'animation). Évite que les tours deviennent longs quand beaucoup d'ennemis sont présents ; seuls ceux proches du joueur bougent réellement.")]
    [Min(1)] public int turnActivationDistance = 12;
}