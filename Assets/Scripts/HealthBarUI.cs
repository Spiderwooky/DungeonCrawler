using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Barre de vie HUD : reflète les PV d'un HealthSystem (typiquement celui du joueur) via
// une Image en mode Filled, plus un texte optionnel "7/10".
public class HealthBarUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueText;

    private void Awake()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDamaged += HandleHealthChanged;
            healthSystem.OnHealed += HandleHealthChanged;
        }
    }

    private void Start()
    {
        // Refresh() ici plutôt qu'en Awake() : HealthSystem.Awake() (qui initialise
        // CurrentHealth) n'est pas garanti de s'exécuter avant le notre, mais tous les
        // Awake() sont garantis terminés avant le premier Start().
        Refresh();
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
        {
            healthSystem.OnDamaged -= HandleHealthChanged;
            healthSystem.OnHealed -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        Refresh();
    }

    private static Color HealthColor(float ratio)
    {
        if (ratio > 0.5f)
            return Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
        return Color.Lerp(Color.red, Color.yellow, ratio * 2f);
    }

    private void Refresh()
    {
        if (healthSystem == null) return;

        float ratio = healthSystem.MaxHealth > 0
            ? (float)healthSystem.CurrentHealth / healthSystem.MaxHealth
            : 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
            fillImage.color = HealthColor(ratio);
        }

        if (valueText != null)
            valueText.text = $"{healthSystem.CurrentHealth}/{healthSystem.MaxHealth}";
    }
}
