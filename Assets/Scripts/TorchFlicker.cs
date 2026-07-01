using UnityEngine;

// Anime le scintillement d'une torche via du bruit de Perlin.
// Requiert un composant Light sur le même GameObject.
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Intensité")]
    [Tooltip("Intensité de base de la lumière au repos.")]
    [SerializeField] private float baseIntensity = 1.2f;
    [Tooltip("Amplitude du scintillement : 0 = stable, 1 = variation égale à l'intensité de base.")]
    [SerializeField] [Range(0f, 1f)] private float flickerAmplitude = 0.35f;
    [Tooltip("Vitesse du scintillement. Plus élevé = plus nerveux.")]
    [SerializeField] private float flickerSpeed = 3.5f;

    [Header("Couleur")]
    [Tooltip("Couleur de base de la flamme.")]
    [SerializeField] private Color baseColor = new Color(1f, 0.55f, 0.1f);
    [Tooltip("Amplitude de la variation de couleur (0 = couleur fixe).")]
    [SerializeField] [Range(0f, 1f)] private float colorVariation = 0.12f;
    [SerializeField] private Color secondaryColor = new Color(1f, 0.35f, 0.05f);

    private new Light light;
    private float timeOffset;

    private void Awake()
    {
        light = GetComponent<Light>();
        light.color = baseColor;
        // Décalage aléatoire pour que toutes les torches ne scintillent pas en synchrone
        timeOffset = Random.value * 137.5f;
    }

    private void Update()
    {
        float t = Time.time * flickerSpeed + timeOffset;

        // Bruit de Perlin sur deux axes pour un scintillement moins prévisible
        float noise = Mathf.PerlinNoise(t, t * 0.61f);
        light.intensity = baseIntensity + (noise - 0.5f) * 2f * baseIntensity * flickerAmplitude;

        if (colorVariation > 0f)
        {
            float colorNoise = Mathf.PerlinNoise(t * 0.4f, 53.7f);
            light.color = Color.Lerp(baseColor, secondaryColor, colorNoise * colorVariation);
        }
    }
}
