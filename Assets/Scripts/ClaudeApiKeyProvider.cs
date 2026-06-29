using System.IO;
using UnityEngine;

/// <summary>
/// Charge la clé API Claude depuis un fichier local situé hors du dépôt Git
/// (Application.persistentDataPath), jamais codée en dur ni stockée dans un asset versionné.
/// Si le fichier est absent ou vide, TryGetApiKey renvoie false : c'est le signal utilisé
/// par Merchant pour basculer en mode hors-ligne (boutons d'action classiques).
/// </summary>
public static class ClaudeApiKeyProvider
{
    private const string FileName = "claude_api_key.txt";

    private static string cachedKey;
    private static bool hasLoaded;

    public static bool TryGetApiKey(out string apiKey)
    {
        if (!hasLoaded)
        {
            cachedKey = LoadFromDisk();
            hasLoaded = true;
        }

        apiKey = cachedKey;
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    /// <summary>Force un nouveau chargement depuis le disque (utile après avoir créé le fichier en cours de partie).</summary>
    public static void Reload()
    {
        hasLoaded = false;
    }

    public static string GetConfigFilePath() => Path.Combine(Application.persistentDataPath, FileName);

    private static string LoadFromDisk()
    {
        Debug.Log($"[ClaudeApiKeyProvider] Chargement de la clé API depuis {GetConfigFilePath()}");
        string path = GetConfigFilePath();
        if (!File.Exists(path))
        {
            Debug.Log($"[ClaudeApiKeyProvider] Aucune clé API trouvée — mode hors-ligne pour les marchands. Pour activer le mode IA, crée un fichier texte contenant uniquement ta clé à cet emplacement : {path}");
            return null;
        }

        try
        {
            string content = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(content) ? null : content;
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[ClaudeApiKeyProvider] Impossible de lire la clé API ({path}) : {e.Message}");
            return null;
        }
    }
}
