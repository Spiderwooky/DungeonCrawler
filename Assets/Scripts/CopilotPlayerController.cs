using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
 
[RequireComponent(typeof(PlayerInput), typeof(CharacterController), typeof(HealthSystem))]
public class CopilotPlayerController : MonoBehaviour, ITurnActor
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Inventory inventory;
 
    [Header("Animation")]
    [SerializeField] private float moveDuration         = 0.3f;
    [SerializeField] private float rotationDuration     = 0.2f;
    [SerializeField] private float rotationAngle        = 90f;
    [SerializeField] private float bumpDistanceMultiplier = 0.25f;
    [SerializeField] private float bumpDuration         = 0.15f;
 
    [Header("Audio")]
    [SerializeField] private AudioClip bumpClip;
 
    private AudioSource      audioSource;
    private CharacterController characterController;
 
    private bool isMoving;
    private bool isRotating;
    private bool isMyTurn; // Vrai seulement quand le TurnManager a donné la main au joueur
 
    // ──────────────────────────────────────────
    // Initialisation
    // ──────────────────────────────────────────
 
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        audioSource         = GetComponent<AudioSource>();
 
        if (audioSource == null)
            Debug.LogWarning("[PlayerController] Pas d'AudioSource : le son de bump ne jouera pas.");

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        // ========== INTÉGRATION AUDIO ==========
        // S'abonner aux événements de santé du joueur
        // Quand le joueur prend des dégâts → joue un son d'impact
        // Quand le joueur meurt → joue un son de mort
        HealthSystem healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            // OnDamaged est appelé chaque fois que le joueur reçoit des dégâts
            healthSystem.OnDamaged += (currentHealth, maxHealth) => 
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerHit();
            };

            // OnDeath est appelé quand les PV du joueur tombent à 0
            healthSystem.OnDeath += () => 
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerDeath();
            };
        }
        else
        {
            Debug.LogWarning("[PlayerController] HealthSystem manquant sur le joueur !");
        }
    }
 
    private void Start()
    {
        // Enregistrement après tous les Awake() (TurnManager.Instance est alors disponible).
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterPlayer(this);
        else
            Debug.LogWarning("[PlayerController] TurnManager introuvable : mode tour-par-tour désactivé.");

        // La grille est garantie initialisée ici car GameManager.Awake() s'est exécuté en premier.
        int[] s = gameManager.GetStart();
        float step = gameManager.GetStep();
 
        // Désactiver le CharacterController le temps du snap pour éviter les conflits
        characterController.enabled = false;
        transform.position = new Vector3(s[0] * step, 0f, s[1] * step);
        characterController.enabled = true;
    }
 
    // ──────────────────────────────────────────
    // ITurnActor — appelé par le TurnManager
    // ──────────────────────────────────────────
 
    public void OnTurnStart()
    {
        isMyTurn = true;
        // Ici, on pourrait afficher un indicateur UI "Au tour du joueur"
    }
 
    // Termine le tour du joueur et rend la main au TurnManager.
    private void EndMyTurn()
    {
        isMyTurn = false;
        TurnManager.Instance?.EndTurn();
    }
 
    // ──────────────────────────────────────────
    // Inputs (New Input System)
    // ──────────────────────────────────────────
 
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
 
        Vector2 input = context.ReadValue<Vector2>();
        if (input == Vector2.zero) return;
 
        // Direction relative à la rotation du joueur (snap sur les 4 axes cardinaux)
        Vector3 direction = Mathf.Abs(input.x) > Mathf.Abs(input.y)
            ? (input.x > 0 ? transform.right : -transform.right)
            : (input.y > 0 ? transform.forward : -transform.forward);
 
        TryMove(direction.normalized);
    }
 
    public void OnRotate(InputAction.CallbackContext context)
    {
        if (!context.performed || isMoving || isRotating) return;
 
        float input = context.ReadValue<float>();
        if (Mathf.Approximately(input, 0f)) return;
 
        TryRotate(input > 0 ? 1f : -1f);
    }
 
    // ──────────────────────────────────────────
    // Logique de déplacement
    // ──────────────────────────────────────────
 
    public void TryMove(Vector3 direction)
    {
        // Si pas mon tour OU déjà en cours d'animation : on ignore
        if (!isMyTurn || isMoving || isRotating) return;
 
        Vector3 targetPosition = transform.position + direction * gameManager.GetStep();
 
        if (IsCellWalkable(targetPosition))
        {
            StartCoroutine(MoveToPosition(targetPosition));
        }
        else
        {
            StartCoroutine(BumpAgainstWall(direction));
            // Le bump ne consomme pas de tour
        }
    }
 
    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        isMoving = true;

        // ========== SON DE PAS ==========
        // Jouer un bruit de pas dès que le joueur commence à se déplacer
        // PlayFootstep() choisit aléatoirement dans le tableau sfxFootsteps
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFootstep();
 
        Vector3 startPosition = transform.position;
        float elapsed = 0f;
 
        while (elapsed < moveDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration); // easing pour + de fluidité
            Vector3 next = Vector3.Lerp(startPosition, targetPosition, t);
            characterController.Move(next - transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }
 
        // Snap final pour éviter le drift de position flottante
        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;
 
        isMoving = false;
        TryPickupAtCurrentCell();
        EndMyTurn(); // ← Fin de tour après un déplacement réussi
    }

    private void TryPickupAtCurrentCell()
    {
        if (inventory == null || gameManager == null) return;

        float step = gameManager.GetStep();
        var grid = new Vector2Int(
            Mathf.RoundToInt(transform.position.x / step),
            Mathf.RoundToInt(transform.position.z / step));

        PickupManager.Instance?.TryCollectAt(grid, inventory);
    }
 
    private IEnumerator BumpAgainstWall(Vector3 direction)
    {
        isMoving = true;
 
        // ========== SON DE COLLISION ==========
        // Utiliser l'AudioManager centralisé au lieu d'un AudioSource local
        // Permet une meilleure gestion des volumes et des transitions sonores
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWallBump();
 
        Vector3 start      = transform.position;
        Vector3 bumpTarget = start + direction * gameManager.GetStep() * bumpDistanceMultiplier;
        float   half       = bumpDuration * 0.5f;
        float   elapsed    = 0f;
 
        // Aller vers le mur
        while (elapsed < half)
        {
            float t = Mathf.Clamp01(elapsed / half);
            characterController.Move(Vector3.Lerp(start, bumpTarget, t) - transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }
 
        // Revenir à la position de départ
        elapsed = 0f;
        while (elapsed < half)
        {
            float t = Mathf.Clamp01(elapsed / half);
            characterController.Move(Vector3.Lerp(bumpTarget, start, t) - transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }
 
        // Snap de sécurité
        characterController.enabled = false;
        transform.position = start;
        characterController.enabled = true;
 
        isMoving = false;
        // Le bump ne termine PAS le tour : le joueur peut réessayer
    }
 
    // ──────────────────────────────────────────
    // Rotation
    // ──────────────────────────────────────────
 
    public void TryRotate(float direction)
    {
        if (!isMyTurn || isMoving || isRotating) return;
 
        float deltaAngle = direction > 0 ? rotationAngle : -rotationAngle;
        StartCoroutine(RotateSmoothly(deltaAngle));
    }
 
    private IEnumerator RotateSmoothly(float deltaAngle)
    {
        isRotating = true;
 
        float startYaw  = transform.eulerAngles.y;
        float targetYaw = startYaw + deltaAngle;
        float elapsed   = 0f;
 
        while (elapsed < rotationDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / rotationDuration);
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(startYaw, targetYaw, t), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
 
        // Snap sur un angle exact (multiple de 90°) pour éviter les erreurs d'arrondi
        transform.rotation = Quaternion.Euler(0f, Mathf.Round(targetYaw / 90f) * 90f, 0f);
 
        isRotating = false;
        // La rotation ne consomme pas de tour (choix de design — à ajuster si besoin)
    }
 
    // ──────────────────────────────────────────
    // Vérification de déplacement
    // ──────────────────────────────────────────
 
    private bool IsCellWalkable(Vector3 targetPosition)
    {
        float step = gameManager.GetStep();
        int x = Mathf.RoundToInt(targetPosition.x / step);
        int z = Mathf.RoundToInt(targetPosition.z / step);
 
        Case[][] grid = gameManager.GetGridDefinition();
 
        // Vérification des bornes avant l'accès au tableau
        if (x < 0 || x >= grid.Length)        return false;
        if (z < 0 || z >= grid[x].Length)     return false;
 
        Case cell = grid[x][z];
        return cell != null && !cell.IsWall();
    }
}