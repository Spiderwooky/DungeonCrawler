using UnityEngine;

// Écran de mort : active un panel quand le HealthSystem référencé (le joueur) déclenche
// OnDeath. Le panel reste désactivé jusqu'à la mort ; le joueur ne peut plus bouger
// (CopilotPlayerController.isDead), ce script ne fait qu'afficher le retour visuel.
public class PlayerDeathUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private GameObject deathPanel;

    private void Awake()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);

        if (playerHealth != null)
            playerHealth.OnDeath += ShowDeathScreen;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= ShowDeathScreen;
    }

    private void ShowDeathScreen()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);
    }
}
