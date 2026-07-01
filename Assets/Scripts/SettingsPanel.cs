using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Panneau de réglages audio (musique + SFX).
// Placez ce composant sur le GameObject du panneau (inactif par défaut).
// OnEnable synchronise les sliders et met le jeu en pause (Time.timeScale = 0).
// OnDisable restaure le temps. Les changements sont appliqués et sauvegardés en temps réel.
public class SettingsPanel : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Labels (optionnels)")]
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    private float _previousTimeScale;
    private InputAction _escapeAction;

    private void Awake()
    {
        _escapeAction = new InputAction("CloseSettings", binding: "<Keyboard>/escape");
        _escapeAction.performed += _ => Close();
    }

    private void OnDestroy() => _escapeAction.Dispose();

    private void OnEnable()
    {
        _escapeAction.Enable();
        AudioManager am = AudioManager.EnsureInstance();

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(am.MusicVolume);
            RefreshLabel(musicValueText, am.MusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(am.SFXVolume);
            RefreshLabel(sfxValueText, am.SFXVolume);
        }

        // Mémorise le timeScale actuel : si on est déjà en pause (menu pause ouvert),
        // fermer les paramètres ne doit pas relancer le jeu.
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        _escapeAction.Disable();
        Time.timeScale = _previousTimeScale;
    }

    // ── Appelé par le bouton "Paramètres" ──────────────────────────────────
    public void Open()  => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    // ── Câbler sur Slider.OnValueChanged dans l'Inspector ─────────────────
    public void OnMusicVolumeChanged(float value)
    {
        AudioManager.EnsureInstance().SetMusicVolume(value);
        RefreshLabel(musicValueText, value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        AudioManager.EnsureInstance().SetSFXVolume(value);
        RefreshLabel(sfxValueText, value);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static void RefreshLabel(TextMeshProUGUI label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100f) + " %";
    }
}
