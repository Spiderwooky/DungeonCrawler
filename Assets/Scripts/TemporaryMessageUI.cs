using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Affiche un texte temporaire (panel + TextMeshProUGUI) pendant quelques secondes, puis le
/// masque automatiquement. Réutilisable pour n'importe quel message ponctuel (ex: "il faut
/// la clé pour franchir cette porte").
/// </summary>
public class TemporaryMessageUI : MonoBehaviour
{
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float defaultDuration = 2.5f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }

    public void Show(string message, float duration = -1f)
    {
        if (duration < 0f) duration = defaultDuration;

        if (messageText != null)
            messageText.text = message;

        if (messagePanel != null)
            messagePanel.SetActive(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay(duration));
    }

    private IEnumerator HideAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (messagePanel != null)
            messagePanel.SetActive(false);
        hideCoroutine = null;
    }
}
