using UnityEngine;

// Écran de fin de niveau : affiche un panel quand le joueur franchit la porte de la salle de
// fin avec la clé. Le panel reste désactivé jusque-là ; le joueur ne peut plus bouger
// (PlayerController.levelCompleted), ce script ne fait qu'afficher le retour visuel.
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

    // Appelé par le bouton "Rejouer" du endPanel.
    public void OnRestartClicked()  => SceneLoader.LoadDungeon();

    // Appelé par le bouton "Menu Principal" du endPanel.
    public void OnMainMenuClicked() => SceneLoader.LoadMainMenu();
}
