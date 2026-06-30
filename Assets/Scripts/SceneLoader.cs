using UnityEngine.SceneManagement;

// Utilitaire statique centralisé pour les transitions de scènes.
// Toutes les scènes du jeu sont référencées ici par leur nom pour éviter les constantes
// dispersées dans le code.
public static class SceneLoader
{
    public const string MainMenu = "MainMenu";
    public const string Dungeon  = "DungeonScene";

    public static void LoadMainMenu()    => SceneManager.LoadScene(MainMenu);
    public static void LoadDungeon()     => SceneManager.LoadScene(Dungeon);
    public static void RestartCurrent()  => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
