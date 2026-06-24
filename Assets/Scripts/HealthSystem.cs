using UnityEngine;
using System;

/// <summary>
/// Composant partagé par le joueur ET les ennemis.
/// Gère les PV, les dégâts et la mort.
/// 
/// Ajouter ce composant sur le GameObject joueur et sur chaque prefab ennemi.
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;

    [Tooltip("Si vrai (par défaut), le GameObject est détruit après la mort (comportement des ennemis). Mettre à false sur le joueur : son GameObject doit rester en vie pour afficher l'écran de mort.")]
    [SerializeField] private bool destroyOnDeath = true;

    // Événements auxquels les autres scripts peuvent s'abonner
    public event Action<int, int> OnDamaged;   // (pvRestants, pvMax)
    public event Action<int, int> OnHealed;    // (pvRestants, pvMax)
    public event Action           OnDeath;

    public int CurrentHealth { get; private set; }
    public int MaxHealth     => maxHealth;
    public bool IsDead       => CurrentHealth <= 0;

    // ──────────────────────────────────────────
    // Initialisation
    // ──────────────────────────────────────────

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    // Permet à EnemyData d'initialiser les PV au moment du spawn.
    public void Initialize(int max)
    {
        maxHealth     = max;
        CurrentHealth = max;
    }

    // ──────────────────────────────────────────
    // API publique
    // ──────────────────────────────────────────

    /// <summary>Inflige des dégâts. Déclenche OnDeath si les PV tombent à 0.</summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        Debug.Log($"[HealthSystem] {gameObject.name} subit {amount} dégâts → {CurrentHealth}/{maxHealth} PV");

        OnDamaged?.Invoke(CurrentHealth, maxHealth);

        if (IsDead)
        {
            Debug.Log($"[HealthSystem] {gameObject.name} est mort.");
            OnDeath?.Invoke();
            HandleDeath();
        }
    }

    /// <summary>Soigne d'un certain nombre de PV (plafonné au maximum).</summary>
    public void Heal(int amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealed?.Invoke(CurrentHealth, maxHealth);
    }

    // ──────────────────────────────────────────
    // Mort
    // ──────────────────────────────────────────

    private void HandleDeath()
    {
        // Désinscrire l'ennemi du TurnManager s'il en est un
        EnemyController enemy = GetComponent<EnemyController>();
        if (enemy != null)
        {
            TurnManager.Instance?.UnregisterEnemy(enemy);
        }

        // Détruire le GameObject après un court délai (laisse le temps aux animations).
        // Désactivé pour le joueur (destroyOnDeath = false) : son écran de mort a besoin
        // que le GameObject reste en vie.
        if (destroyOnDeath)
        {
            Destroy(gameObject, 0.5f);
        }
    }
}