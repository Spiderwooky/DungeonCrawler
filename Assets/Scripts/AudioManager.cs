using UnityEngine;
using System.Collections;

/// <summary>
/// AudioManager - Gestionnaire centralisé de toute l'audio du jeu (musiques + effets sonores)
/// 
/// Fonctionne en Singleton (une seule instance) et persiste entre les scènes.
/// 
/// MUSIQUES (en boucle) :
///   - Exploration : musique pendant les déplacements normaux
///   - Combat : déclenche quand un ennemi détecte le joueur
///   - Accueil : écran titre/menu
///   - Crédits : écran de fin
/// 
/// EFFETS SONORES (ponctuels) :
///   - Attaque joueur/ennemi
///   - Dégâts reçus (joueur et ennemi)
///   - Morts (joueur et ennemi)
///   - Collisions (mur, obstacles)
///   - Pas du joueur
/// </summary>

[DefaultExecutionOrder(-50)] // Exécuté AVANT les autres scripts (avant Start())
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public static AudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("AudioManager");
        return go.AddComponent<AudioManager>();
    }

    [Header("Musiques")]
    [SerializeField] private AudioClip musicExploration;   // Boucle pendant l'exploration
    [SerializeField] private AudioClip musicCombat;        // Déclenche lors du combat
    [SerializeField] private AudioClip musicAccueil;       // Écran titre (sera géré par MenuManager)
    [SerializeField] private AudioClip musicCredits;       // Écran crédits (sera géré par CreditsManager)

    [Header("Effets sonores - Joueur")]
    [SerializeField] private AudioClip sfxPlayerHit;       // Joueur prend un coup
    [SerializeField] private AudioClip sfxPlayerDeath;     // Mort du joueur

    [Header("Effets sonores - Ennemis")]
    [SerializeField] private AudioClip sfxEnemyDamage;     // Ennemi subit des dégâts
    [SerializeField] private AudioClip sfxEnemyDeath;      // Ennemi meurt

    [Header("Effets sonores - Actions")]
    [SerializeField] private AudioClip sfxAttack;          // Attaque (joueur ou ennemi)
    [SerializeField] private AudioClip sfxWallBump;        // Collision avec un mur/obstacle
    [SerializeField] private AudioClip sfxBarrelBreak;     // Bris d'un objet destructible (tonneau, caisse…)
    [SerializeField] private AudioClip[] sfxFootsteps;     // Tableau de bruits de pas (variation sonore)

    [Header("Effets sonores - Inventaire")]
    [SerializeField] private AudioClip sfxPickupItem;      // Son quand on prend un objet
    [SerializeField] private AudioClip sfxDropItem;        // Son quand on dépose un objet

    [Header("Contrôles de volume")]
    [SerializeField] private float musicVolume = 0.7f;     // Volume des musiques (0 à 1)
    [SerializeField] private float sfxVolume = 1f;         // Volume des effets (0 à 1)
    [SerializeField] private float musicFadeDuration = 0.5f; // Durée du transition entre musiques

    private AudioSource musicSource;    // Source pour les musiques (une seule à la fois)
    private AudioSource sfxSource;      // Source pour les SFX (overlapping autorisé)
    private AudioClip currentMusic;     // Musique actuellement jouée
    private Coroutine musicTransitionCoroutine;
    private bool inCombat = false;      // Flag pour savoir si on est en combat

    // ──────────────────────────────────────────
    // INITIALISATION SINGLETON
    // ──────────────────────────────────────────

    private void Awake()
    {
        // Vérifier s'il existe déjà une instance (ne créer que si nécessaire)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Devenir l'instance unique
        Instance = this;
        
        // Persister entre les changements de scène
        DontDestroyOnLoad(gameObject);

        // ===== Créer les deux AudioSource =====
        
        // AudioSource 1 : Musiques (loop, une seule à la fois)
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;           // Les musiques boucles en continu
        musicSource.volume = musicVolume;

        // AudioSource 2 : Effets sonores (pas de loop, peuvent se chevaucher)
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;            // Les SFX ne jouent qu'une fois
        sfxSource.volume = sfxVolume;

        Debug.Log("[AudioManager] Singleton initialisé et prêt.");
    }

    private void Start()
    {
        // Au lancement du jeu, si on n'est pas dans l'écran d'accueil ou de crédits,
        // on lance la musique d'exploration par défaut.
        if (musicExploration != null && musicSource != null && currentMusic != musicExploration)
        {
            PlayMusicExploration();
        }
    }

    // ──────────────────────────────────────────
    // GESTION DES MUSIQUES (Exploration, Combat, Accueil, Crédits)
    // ──────────────────────────────────────────

    /// <summary>Joue la musique d'exploration (normal gameplay)</summary>
    public void PlayMusicExploration()
    {
        PlayMusic(musicExploration, "Exploration");
        inCombat = false;
    }

    /// <summary>Joue la musique de combat (ennemi détecté)</summary>
    public void PlayMusicCombat()
    {
        PlayMusic(musicCombat, "Combat");
        inCombat = true;
    }

    /// <summary>Joue la musique d'accueil (écran titre/menu principal)</summary>
    public void PlayMusicAccueil()
    {
        PlayMusic(musicAccueil, "Accueil");
        inCombat = false;
    }

    /// <summary>Joue la musique des crédits (écran de fin)</summary>
    public void PlayMusicCredits()
    {
        PlayMusic(musicCredits, "Crédits");
        inCombat = false;
    }

    /// <summary>
    /// Méthode interne : change la musique avec une transition fade fluide
    /// Évite les clics sonores lors des changements
    /// </summary>
    private void PlayMusic(AudioClip newMusic, string contextName = "")
    {
        if (newMusic == null)
        {
            Debug.LogWarning($"[AudioManager] Clip musical null : {contextName}");
            return;
        }

        // Optimisation : ne pas changer si la même musique est déjà en cours
        if (currentMusic == newMusic) return;

        // Si une transition précédente est en cours, l'arrêter immédiatement
        if (musicTransitionCoroutine != null)
            StopCoroutine(musicTransitionCoroutine);

        musicTransitionCoroutine = StartCoroutine(FadeMusicTransition(newMusic, contextName));
    }

    /// <summary>
    /// Coroutine : transition fade-out/fade-in entre deux musiques
    /// Dure musicFadeDuration secondes pour un effet fluide
    /// </summary>
    private IEnumerator FadeMusicTransition(AudioClip newMusic, string contextName = "")
    {
        // === FADE OUT (réduction du volume) ===
        float startVolume = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < musicFadeDuration && musicSource.isPlaying)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0, elapsed / musicFadeDuration);
            yield return null;
        }

        // === CHANGEMENT DU CLIP ===
        musicSource.clip = newMusic;
        musicSource.Play();
        currentMusic = newMusic;
        
        Debug.Log($"[AudioManager] Musique changée : {contextName}");

        // === FADE IN (augmentation du volume) ===
        elapsed = 0f;
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0, musicVolume, elapsed / musicFadeDuration);
            yield return null;
        }

        // S'assurer que le volume final est exact
        musicSource.volume = musicVolume;
        musicTransitionCoroutine = null;
    }

    // ──────────────────────────────────────────
    // EFFETS SONORES - API PUBLIQUE
    // ──────────────────────────────────────────

    /// <summary>Joue : joueur prend un coup</summary>
    public void PlayPlayerHit()
    {
        PlaySFX(sfxPlayerHit, "Joueur touché");
    }

    /// <summary>Joue : mort du joueur</summary>
    public void PlayPlayerDeath()
    {
        PlaySFX(sfxPlayerDeath, "Mort joueur");
    }

    /// <summary>Joue : ennemi subit des dégâts</summary>
    public void PlayEnemyDamage()
    {
        PlaySFX(sfxEnemyDamage, "Dégâts ennemi");
    }

    /// <summary>Joue : ennemi meurt</summary>
    public void PlayEnemyDeath()
    {
        PlaySFX(sfxEnemyDeath, "Mort ennemi");
    }

    /// <summary>Joue : attaque (joueur ou ennemi)</summary>
    public void PlayAttack()
    {
        PlaySFX(sfxAttack, "Attaque");
    }

    /// <summary>Joue : collision avec un mur/obstacle</summary>
    public void PlayWallBump()
    {
        PlaySFX(sfxWallBump, "Collision mur");
    }

    /// <summary>Joue : bris d'un objet destructible (tonneau, caisse…)</summary>
    public void PlayBarrelBreak()
    {
        PlaySFX(sfxBarrelBreak, "Bris objet");
    }

    /// <summary>Joue : prise d'objet dans l'inventaire</summary>
    public void PlayPickupItem()
    {
        PlaySFX(sfxPickupItem, "Ramassage objet");
    }

    /// <summary>Joue : dépose d'objet depuis l'inventaire</summary>
    public void PlayDropItem()
    {
        PlaySFX(sfxDropItem, "Dépose objet");
    }

    /// <summary>
    /// Joue : un bruit de pas aléatoire du tableau sfxFootsteps
    /// Crée une variation sonore pour les déplacements répétitifs
    /// </summary>
    public void PlayFootstep()
    {
        // Vérifier que le tableau de pas n'est pas vide
        if (sfxFootsteps == null || sfxFootsteps.Length == 0)
        {
            Debug.LogWarning("[AudioManager] Aucun bruit de pas configuré dans l'Inspector");
            return;
        }

        // Sélectionner un pas aléatoire pour la variation
        AudioClip randomFootstep = sfxFootsteps[Random.Range(0, sfxFootsteps.Length)];
        PlaySFX(randomFootstep, "Pas");
    }

    /// <summary>
    /// Méthode interne : joue un effet sonore
    /// Utilise PlayOneShot() pour permettre plusieurs SFX simultanés
    /// </summary>
    private void PlaySFX(AudioClip clip, string contextName = "")
    {
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] SFX null : {contextName}");
            return;
        }

        // PlayOneShot = joue le clip sans bloquer d'autres sons
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ──────────────────────────────────────────
    // UTILITAIRES (accesseurs publics)
    // ──────────────────────────────────────────

    /// <summary>Retourne true si actuellement en musique de combat</summary>
    public bool IsInCombat => inCombat;

    /// <summary>Règle le volume des musiques (0 à 1)</summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    /// <summary>Règle le volume des effets sonores (0 à 1)</summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }
}
