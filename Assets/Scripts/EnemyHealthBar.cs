using UnityEngine;
using UnityEngine.UI;

// Barre de vie flottante au-dessus d'un ennemi.
// Placée sur le GameObject Canvas (World Space) enfant de l'ennemi.
// Initialisée par EnemyController.Awake() via Initialize().
public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("Image de remplissage (Image Type = Filled, Fill Method = Horizontal).")]
    [SerializeField] private Image fillImage;

    private Camera mainCam;

    // Appelé par EnemyController juste après que le HealthSystem est initialisé.
    public void Initialize(HealthSystem healthSystem, bool showAlways)
    {
        mainCam = Camera.main;
        gameObject.SetActive(showAlways);

        UpdateFill(healthSystem.CurrentHealth, healthSystem.MaxHealth);

        healthSystem.OnDamaged += (current, max) =>
        {
            gameObject.SetActive(true);
            UpdateFill(current, max);
        };

        healthSystem.OnHealed += (current, max) => UpdateFill(current, max);
    }

    private void LateUpdate()
    {
        if (mainCam != null)
            transform.rotation = mainCam.transform.rotation;
    }

    private void UpdateFill(int current, int max)
    {
        if (fillImage == null) return;
        float ratio = max > 0 ? (float)current / max : 0f;
        fillImage.fillAmount = ratio;
        fillImage.color = HealthColor(ratio);
    }

    private static Color HealthColor(float ratio)
    {
        if (ratio > 0.5f)
            return Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
        return Color.Lerp(Color.red, Color.yellow, ratio * 2f);
    }
}
