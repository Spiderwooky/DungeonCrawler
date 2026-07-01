using UnityEngine;
using UnityEngine.InputSystem;

// Menu pause du donjon.
// Placez ce composant sur un GameObject TOUJOURS ACTIF dans la scène (pas sur le panel
// lui-même) pour que la touche Échap fonctionne même quand le panel est caché.
// Assignez pausePanel (le contenu visuel, inactif par défaut), settingsPanel et playerInput
// dans l'Inspector.
public class PauseMenu : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameObject    pausePanel;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private InventoryUI   inventoryUI;
    [SerializeField] private PlayerInput   playerInput;

    // Action Échap déclarée en code — pas besoin de la définir dans l'InputActionAsset.
    // UIInputModule et PlayerInput ne gèrent pas cette action : on la gère ici directement.
    private InputAction _escapeAction;

    private void Awake()
    {
        _escapeAction = new InputAction("Pause", binding: "<Keyboard>/escape");
        _escapeAction.performed += _ => HandleEscape();
    }

    private void Start()
    {
        // Réinitialisation défensive à chaque chargement de la scène donjon :
        // garantit qu'aucun état de pause résiduel de la session précédente ne subsiste.
        Time.timeScale = 1f;
        if (playerInput) playerInput.enabled = true;
        pausePanel?.SetActive(false);
    }

    private void OnEnable()  => _escapeAction.Enable();
    private void OnDisable() => _escapeAction.Disable();

    private void OnDestroy() => _escapeAction.Dispose();

    // ── Logique Échap ────────────────────────────────────────────────────────
    private void HandleEscape()
    {
        // Priorité : paramètres > inventaire > menu pause
        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
        {
            settingsPanel.Close();
            return;
        }

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            inventoryUI.SetInventoryOpen(false);
            return;
        }

        if (Merchant.IsAnyDialogueOpen) return;

        if (pausePanel != null && pausePanel.activeSelf)
            Close();
        else
            Open();
    }

    // ── API publique (boutons Inspector) ────────────────────────────────────

    public void Open()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        if (playerInput != null) playerInput.enabled = false;
    }

    public void Close()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        if (playerInput != null) playerInput.enabled = true;
    }

    public void OnResumeClicked()    => Close();
    public void OnSettingsClicked()  => settingsPanel?.Open();
    public void OnMainMenuClicked()
    {
        // Fermer les paramètres d'abord : leur OnDisable restaure _previousTimeScale (= 0
        // si ouverts depuis la pause). On écrase ensuite avec 1f pour éviter de charger
        // la nouvelle scène avec timeScale = 0.
        if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
            settingsPanel.Close();

        Time.timeScale = 1f;
        if (playerInput != null) playerInput.enabled = true;
        SceneLoader.LoadMainMenu();
    }
}
