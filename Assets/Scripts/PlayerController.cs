using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// Contrôleur joueur actif (déplacement/rotation/attaque case par case via le New Input
// System). Implémente ITurnActor : termine son tour via TurnManager après un déplacement
// ou une attaque réussis. Un bump (mur ou rotation) ne consomme pas le tour.
[RequireComponent(typeof(PlayerInput), typeof(CharacterController), typeof(HealthSystem))]
public class PlayerController : MonoBehaviour, ITurnActor
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerWallet playerWallet;

    [Header("Animation")]
    [Tooltip("Animator du modèle joueur. Les paramètres attendus : bool IsMoving, trigger Attack, trigger PickUp, trigger Drop, trigger Death.")]
    [SerializeField] private Animator animator;
    [SerializeField] private float moveDuration              = 0.3f;
    [SerializeField] private float rotationDuration          = 0.2f;
    [SerializeField] private float rotationAngle             = 90f;
    [Tooltip("Multiplicateur de distance pour le bump contre un ennemi, un destructible ou un pilier (obstacle au centre de la case).")]
    [SerializeField] private float bumpDistanceMultiplier    = 0.40f;
    [Tooltip("Multiplicateur de distance pour le bump contre un mur classique (modèle en bord de tuile, plus proche).")]
    [SerializeField] private float bumpWallDistanceMultiplier = 0.25f;
    [SerializeField] private float bumpDuration              = 0.15f;
    [Tooltip("Durée totale (s) réservée à l'animation d'attaque. La différence avec bumpDuration est attendue après le bump.")]
    [SerializeField] private float animAttackDuration        = 0.4f;
    [Tooltip("Durée (s) du trigger PickUp joué après un déplacement sur un objet.")]
    [SerializeField] private float animPickupDuration        = 0.35f;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 2;

    [Header("Fin de donjon")]
    [SerializeField] private TemporaryMessageUI messageUI;
    [SerializeField] private LevelEndUI levelEndUI;
    [SerializeField] private string keyRequiredMessage = "Il faut trouver la clé pour franchir cette porte.";

    // Dégâts effectifs de l'attaque : base + somme des bonus de tout ce qui est équipé.
    private int CurrentAttackDamage =>
        attackDamage + (inventory != null ? inventory.GetEquipmentAttackBonus() : 0);

    private CharacterController characterController;

    private bool isMoving;
    private bool isRotating;
    private bool isMyTurn; // Vrai seulement quand le TurnManager a donné la main au joueur
    private bool isDead;   // Mis à true par OnDeath (HealthSystem) : bloque tout mouvement/rotation
    private bool levelCompleted; // Mis à true en franchissant la porte de fin avec la clé : bloque tout mouvement/rotation
 
    // ──────────────────────────────────────────
    // Initialisation
    // ──────────────────────────────────────────
 
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (playerWallet == null)
            playerWallet = GetComponent<PlayerWallet>();

        // ── Intégration audio ──
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
                isDead = true;
                animator?.SetTrigger("Death");

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
        Vector2Int startCell = gameManager.GetStart();

        // Désactiver le CharacterController le temps du snap pour éviter les conflits
        characterController.enabled = false;
        transform.position = GridUtils.GridToWorld(startCell, gameManager.GetStep());

        // Si la salle de départ a une porte, démarrer face à elle (case "devant la porte").
        Vector2Int? facing = gameManager.GetStartFacing();
        if (facing.HasValue && facing.Value != Vector2Int.zero)
        {
            Vector3 direction = new Vector3(facing.Value.x, 0f, facing.Value.y);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

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

    // Utilise l'objet du slot hotbar sélectionné (potion, équipement...). Comme une attaque,
    // ça consomme le tour du joueur si quelque chose a effectivement été utilisé.
    public void OnUseItem(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!isMyTurn || isMoving || isRotating || isDead || levelCompleted || Merchant.IsAnyDialogueOpen) return;
        if (inventory == null) return;

        if (inventory.UseSelectedItem(gameObject))
            EndMyTurn();
    }
 
    // ──────────────────────────────────────────
    // Logique de déplacement
    // ──────────────────────────────────────────
 
    public void TryMove(Vector3 direction)
    {
        // Si pas mon tour, déjà en cours d'animation, OU mort : on ignore
        if (!isMyTurn || isMoving || isRotating || isDead || levelCompleted || Merchant.IsAnyDialogueOpen) return;
 
        Vector3 targetPosition = transform.position + direction * gameManager.GetStep();

        // Si une marchande est sur la case cible → ouvrir le dialogue au lieu de se déplacer.
        // Ne consomme pas le tour (action gratuite, comme un bump de mur).
        Merchant merchant = GetMerchantAtPosition(targetPosition);
        if (merchant != null)
        {
            merchant.OpenDialogue(inventory, playerWallet);
            return;
        }

        // Si une entité ennemie est sur la case cible → attaquer au lieu de se déplacer
        EnemyController enemy = GetEnemyAtPosition(targetPosition);
        if (enemy != null)
        {
            StartCoroutine(AttackEnemy(enemy, direction));
            return;
        }

        // Si la case cible est la porte de la salle de fin → vérifier la clé au lieu de
        // traiter ça comme un simple mur.
        if (IsEndDoor(targetPosition))
        {
            TryOpenEndDoor(direction);
            return;
        }

        // Si un objet destructible occupe la case cible → le briser au lieu de s'y déplacer.
        BreakableObject breakable = GetBreakableAtPosition(targetPosition);
        if (breakable != null)
        {
            StartCoroutine(BreakObject(breakable, direction));
            return;
        }

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

    // Vrai si `targetPosition` correspond à la case de la porte de la salle de fin.
    private bool IsEndDoor(Vector3 targetPosition)
    {
        Vector2Int? doorCell = gameManager.GetEndDoorCell();
        if (!doorCell.HasValue) return false;

        return GridUtils.WorldToGrid(targetPosition, gameManager.GetStep()) == doorCell.Value;
    }

    // Avec la clé en inventaire : déclenche la fin de partie (écran de fin, musique, blocage
    // des mouvements). Sans la clé : bump normal + message temporaire.
    private void TryOpenEndDoor(Vector3 direction)
    {
        ItemData keyItemData = gameManager.GetKeyItemData();
        bool hasKey = keyItemData != null && inventory != null && inventory.CountItem(keyItemData) > 0;

        if (!hasKey)
        {
            StartCoroutine(BumpAgainstWall(direction));
            if (messageUI != null)
                messageUI.Show(keyRequiredMessage);
            return;
        }

        levelCompleted = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusicCredits();

        if (levelEndUI != null)
            levelEndUI.ShowEndScreen();
    }

    // Cherche un BreakableObject dont la position grille correspond à la case cible.
    private BreakableObject GetBreakableAtPosition(Vector3 targetPosition)
    {
        float step = gameManager.GetStep();
        Vector2Int targetCell = GridUtils.WorldToGrid(targetPosition, step);

        BreakableObject[] breakables = Object.FindObjectsByType<BreakableObject>(FindObjectsSortMode.None);
        foreach (var b in breakables)
        {
            if (b == null) continue;
            if (GridUtils.WorldToGrid(b.transform.position, step) == targetCell) return b;
        }
        return null;
    }

    // Brise un objet destructible : même animation de bump que pour un ennemi, puis appel
    // à Break() qui déclenche son, drops et destruction. Consomme le tour.
    private IEnumerator BreakObject(BreakableObject breakable, Vector3 direction)
    {
        isMoving = true;
        yield return StartCoroutine(DoBump(direction, bumpDistanceMultiplier));
        if (breakable != null)
            breakable.Break();
        isMoving = false;
        EndMyTurn();
    }

    // Cherche un Merchant dont la position monde correspond à la case cible.
    private Merchant GetMerchantAtPosition(Vector3 targetPosition)
    {
        float step = gameManager.GetStep();
        Vector2Int targetCell = GridUtils.WorldToGrid(targetPosition, step);

        Merchant[] merchants = Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None);
        foreach (var m in merchants)
        {
            if (m == null) continue;
            if (GridUtils.WorldToGrid(m.transform.position, step) == targetCell) return m;
        }

        return null;
    }

    // Cherche un EnemyController dont la position monde correspond à la case cible.
    private EnemyController GetEnemyAtPosition(Vector3 targetPosition)
    {
        // On compare les positions snapées sur la grille (les ennemis y sont toujours alignés)
        float step = gameManager.GetStep();
        Vector2Int targetCell = GridUtils.WorldToGrid(targetPosition, step);

        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (GridUtils.WorldToGrid(e.transform.position, step) == targetCell) return e;
        }

        return null;
    }

    // Anime un aller-retour vers `direction` (sans déplacer réellement le joueur).
    // Partagé par BumpAgainstWall (collision), AttackEnemy et BreakObject.
    private IEnumerator DoBump(Vector3 direction, float distanceMultiplier)
    {
        Vector3 start = transform.position;
        Vector3 bumpTarget = start + direction * gameManager.GetStep() * distanceMultiplier;
        float half = bumpDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            float t = Mathf.Clamp01(elapsed / half);
            characterController.Move(Vector3.Lerp(start, bumpTarget, t) - transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }

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
    }

    // Attaque une entité ennemie : même animation de bump que contre un mur, puis inflige des dégâts.
    private IEnumerator AttackEnemy(EnemyController enemy, Vector3 direction)
    {
        if (enemy == null) yield break;

        isMoving = true;
        animator?.SetTrigger("Attack");

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAttack();

        yield return StartCoroutine(DoBump(direction, bumpDistanceMultiplier));

        // L'ennemi peut avoir été détruit pendant l'animation (ex: tué par un coup précédent,
        // son délai de destruction s'écoule pendant ce bump) : revérifier avant GetComponent.
        if (enemy == null)
        {
            isMoving = false;
            EndMyTurn();
            yield break;
        }

        HealthSystem enemyHealth = enemy.GetComponent<HealthSystem>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(CurrentAttackDamage);
        }
        else
        {
            Debug.LogWarning("[Player] Ennemi sans HealthSystem trouvé lors de l'attaque.");
        }

        // Laisser l'animation d'attaque se terminer (le bump ne couvre que bumpDuration).
        float remaining = animAttackDuration - bumpDuration;
        if (animator != null && remaining > 0f)
            yield return new WaitForSeconds(remaining);

        isMoving = false;
        EndMyTurn();
    }
 
    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        isMoving = true;
        animator?.SetBool("IsMoving", true);

        // Vérifier s'il y a un objet à ramasser à destination (avant de se déplacer, pour
        // déclencher l'animation PickUp après l'arrivée si nécessaire).
        Vector2Int targetCell = GridUtils.WorldToGrid(targetPosition, gameManager.GetStep());
        bool willPickup = PickupManager.Instance?.HasPickupAt(targetCell) ?? false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFootstep();

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            Vector3 next = Vector3.Lerp(startPosition, targetPosition, t);
            characterController.Move(next - transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }

        characterController.enabled = false;
        transform.position = targetPosition;
        characterController.enabled = true;

        animator?.SetBool("IsMoving", false);
        isMoving = false;

        TryPickupAtCurrentCell();

        if (willPickup && animator != null)
        {
            animator.SetTrigger("PickUp");
            yield return new WaitForSeconds(animPickupDuration);
        }

        EndMyTurn();
    }

    private void TryPickupAtCurrentCell()
    {
        if (inventory == null || gameManager == null) return;

        Vector2Int grid = GridUtils.WorldToGrid(transform.position, gameManager.GetStep());
        PickupManager.Instance?.TryCollectAt(grid, inventory);
    }

    // Appelé par InventoryInputHandler quand le joueur jette un objet.
    public void TriggerDropAnimation()
    {
        animator?.SetTrigger("Drop");
    }
 
    // Bump contre un mur : même animation aller-retour que AttackEnemy, mais avec le son
    // de collision. Ne termine PAS le tour : le joueur peut réessayer immédiatement.
    // Utilise un multiplicateur plus petit pour les murs classiques (modèle en bord de tuile)
    // et le multiplicateur standard pour les piliers isolés (modèle au centre de leur case).
    private IEnumerator BumpAgainstWall(Vector3 direction)
    {
        isMoving = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWallBump();

        float mult = IsTargetIsolatedPillar(direction) ? bumpDistanceMultiplier : bumpWallDistanceMultiplier;
        yield return StartCoroutine(DoBump(direction, mult));

        isMoving = false;
    }

    // Vrai si la case dans `direction` est un pilier isolé (case mur entourée de sol sur 4 côtés).
    private bool IsTargetIsolatedPillar(Vector3 direction)
    {
        float step = gameManager.GetStep();
        Vector2Int target = GridUtils.WorldToGrid(transform.position + direction * step, step);
        Case[][] grid = gameManager.GetGridDefinition();

        if (target.x < 0 || target.x >= grid.Length) return false;
        if (target.y < 0 || target.y >= grid[target.x].Length) return false;
        if (!grid[target.x][target.y].IsWall()) return false;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int d in dirs)
        {
            Vector2Int n = target + d;
            if (n.x < 0 || n.x >= grid.Length || n.y < 0 || n.y >= grid[n.x].Length) return false;
            if (!grid[n.x][n.y].IsGround()) return false;
        }
        return true;
    }
 
    // ──────────────────────────────────────────
    // Rotation
    // ──────────────────────────────────────────
 
    public void TryRotate(float direction)
    {
        if (!isMyTurn || isMoving || isRotating || isDead || levelCompleted || Merchant.IsAnyDialogueOpen) return;
 
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
        Vector2Int cellPos = GridUtils.WorldToGrid(targetPosition, gameManager.GetStep());
        Case[][] grid = gameManager.GetGridDefinition();

        // Vérification des bornes avant l'accès au tableau
        if (cellPos.x < 0 || cellPos.x >= grid.Length)           return false;
        if (cellPos.y < 0 || cellPos.y >= grid[cellPos.x].Length) return false;

        Case cell = grid[cellPos.x][cellPos.y];
        return cell != null && !cell.IsWall();
    }
}