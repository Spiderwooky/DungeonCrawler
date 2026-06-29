using UnityEngine;

// Écran de fin de niveau : affiche un panel quand le joueur franchit la porte de la salle de
// fin avec la clé. Le panel reste désactivé jusque-là ; le joueur ne peut plus bouger
// (CopilotPlayerController.levelCompleted), ce script ne fait qu'afficher le retour visuel.
public class LevelEndUI : MonoBehaviour
{
    [SerializeField] private GameObject endPanel;

    private void Awake()
    {
        if (endPanel != null)
            endPanel.SetActive(false);
    }

    public void ShowEndScreen()
    {
        if (endPanel != null)
            endPanel.SetActive(true);
    }
}
