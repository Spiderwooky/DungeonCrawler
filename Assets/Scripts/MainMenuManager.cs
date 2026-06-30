using UnityEngine;

// Contrôleur de la scène Menu Principal.
// Les boutons de la scène câblent leurs OnClick vers les méthodes publiques de ce composant.
public class MainMenuManager : MonoBehaviour
{
    private void Start()
    {
        // L'AudioManager persiste entre les scènes (DontDestroyOnLoad) : on lui demande
        // explicitement de jouer la musique d'accueil à chaque fois qu'on revient ici.
        AudioManager.EnsureInstance().PlayMusicAccueil();
    }

    public void OnPlayClicked()  => SceneLoader.LoadDungeon();
    public void OnQuitClicked()  => Application.Quit();
}
