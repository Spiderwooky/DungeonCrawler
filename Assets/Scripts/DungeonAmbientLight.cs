using UnityEngine;
using UnityEngine.Rendering;

// Contrôle l'ambiance lumineuse de la scène du donjon par code.
// Ajoute ce composant sur n'importe quel GameObject de la scène (ex. GameManager).
// Les valeurs sont dosables dans l'Inspector sans toucher aux Lighting Settings Unity.
public class DungeonAmbientLight : MonoBehaviour
{
    [Header("Lumière ambiante")]
    [Tooltip("Couleur de remplissage global. Noir pur = obscurité totale entre les torches.")]
    [SerializeField] private Color ambientColor = new Color(0.03f, 0.03f, 0.05f);

    [Header("Fond de caméra")]
    [Tooltip("Couleur du fond de la caméra principale (visible quand aucune géométrie n'est devant).")]
    [SerializeField] private Color cameraBackground = Color.black;

    [Header("Brouillard de profondeur")]
    [SerializeField] private bool enableFog = true;
    [Tooltip("Couleur du brouillard — idéalement très proche du fond de caméra.")]
    [SerializeField] private Color fogColor = new Color(0.01f, 0.01f, 0.02f);
    [Tooltip("Densité exponentielle : 0.02 = brume légère, 0.08 = donjon dense.")]
    [SerializeField] [Range(0f, 0.2f)] private float fogDensity = 0.04f;

    private void Awake()
    {
        // Supprime le skybox pour obtenir un fond plein
        RenderSettings.skybox      = null;
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;

        // Brouillard de profondeur (vérifie que le URP renderer l'a activé aussi)
        RenderSettings.fog        = enableFog;
        RenderSettings.fogMode    = FogMode.Exponential;
        RenderSettings.fogColor   = fogColor;
        RenderSettings.fogDensity = fogDensity;

        // Fond caméra en couleur pleine
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = cameraBackground;
        }
    }
}
