using System.Collections;
using UnityEngine;

// Bestiole d'ambiance qui erre librement sur le sol du donjon (mouvement continu,
// non snappé sur la grille, non lié au TurnManager).
// Initialiser via Initialize() juste après Instantiate() (CritterSpawner s'en charge).
public class CritterController : MonoBehaviour
{
    [Header("Mouvement")]
    [Tooltip("Vitesse de déplacement en unités/seconde.")]
    [SerializeField] private float moveSpeed = 2.5f;
    [Tooltip("Distance max des waypoints par rapport à la position courante (unités monde).")]
    [SerializeField] private float waypointRadius = 8f;
    [Tooltip("Vitesse de virage vers le waypoint (rad/s). Bas = courbes douces, haut = lignes droites.")]
    [SerializeField] private float steerSpeed = 2.5f;

    [Header("Pauses")]
    [Tooltip("Probabilité (0–1) de marquer une pause à l'arrivée à chaque waypoint.")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseProbability = 0.35f;
    [SerializeField] private float minPauseDuration = 0.3f;
    [SerializeField] private float maxPauseDuration = 1.8f;

    [Header("Particules")]
    [Tooltip("Particle System de traînée. Joue pendant le déplacement, s'arrête à l'arrivée.")]
    [SerializeField] private ParticleSystem moveParticles;

    private GameManager gameManager;
    private float step;

    // ──────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────

    public void Initialize(GameManager gm, Vector2Int startCell)
    {
        gameManager = gm;
        step = gm.GetStep();

        // Décalage aléatoire à l'intérieur de la case pour éviter que toutes les bestioles
        // démarrent exactement au centre de leur case.
        float halfStep = step * 0.35f;
        Vector3 cellCenter = GridUtils.GridToWorld(startCell, step);
        transform.position = cellCenter + new Vector3(
            Random.Range(-halfStep, halfStep), 0f,
            Random.Range(-halfStep, halfStep));
    }

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("[CritterController] GameManager non assigné.");
            return;
        }
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        StartCoroutine(Wander());
    }

    // ──────────────────────────────────────────
    // Errance
    // ──────────────────────────────────────────

    private IEnumerator Wander()
    {
        while (true)
        {
            Vector3 waypoint = FindRandomWaypoint();

            // Waypoint introuvable (cas rare : bestiole coincée) → courte attente puis retry.
            if (waypoint == transform.position)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            moveParticles?.Play();

            // Déplacement vers le waypoint avec steering doux.
            while (true)
            {
                Vector3 toTarget = waypoint - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist < 0.12f) break;

                // Rotation progressive vers le waypoint
                Vector3 steered = Vector3.RotateTowards(
                    transform.forward, toTarget.normalized,
                    steerSpeed * Time.deltaTime, 0f);
                transform.rotation = Quaternion.LookRotation(steered);

                // Avancer, puis vérifier qu'on reste sur du sol
                Vector3 nextPos = transform.position + transform.forward * moveSpeed * Time.deltaTime;
                if (!IsWalkable(nextPos))
                {
                    // Se retourner avant de chercher le prochain waypoint : garantit que
                    // FindRandomWaypoint explore une direction opposée au mur.
                    transform.rotation = Quaternion.Euler(0f,
                        transform.eulerAngles.y + Random.Range(130f, 230f), 0f);
                    break;
                }

                transform.position = nextPos;
                yield return null;
            }

            moveParticles?.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            // Pause optionnelle à l'arrivée
            if (Random.value < pauseProbability)
                yield return new WaitForSeconds(Random.Range(minPauseDuration, maxPauseDuration));
            else
                yield return null; // au moins une frame avant le prochain waypoint
        }
    }

    // ──────────────────────────────────────────
    // Recherche de waypoint
    // ──────────────────────────────────────────

    // Tente de trouver un point monde valide dans le rayon. Favorise légèrement la direction
    // courante pour un mouvement plus cohérent (moins de demi-tours brusques).
    private Vector3 FindRandomWaypoint()
    {
        for (int i = 0; i < 15; i++)
        {
            // Les premiers essais restent dans un cône devant la bestiole, les derniers
            // ouvrent l'angle pour garantir de trouver une sortie si coincée contre un mur.
            float maxAngle = i < 8 ? 90f : 180f;
            float angle = Random.Range(-maxAngle, maxAngle);
            float dist  = Random.Range(step * 0.5f, waypointRadius);

            Vector3 candidate = transform.position
                + Quaternion.Euler(0f, angle, 0f) * transform.forward * dist;
            candidate.y = transform.position.y;

            if (IsWalkable(candidate))
                return candidate;
        }

        // Dernier recours : scan à 45° sur tout le cercle à courte portée.
        // Garantit une sortie même si la bestiole est coincée dans un couloir étroit.
        for (int i = 0; i < 8; i++)
        {
            Vector3 candidate = transform.position
                + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * step * 0.4f;
            candidate.y = transform.position.y;
            if (IsWalkable(candidate))
                return candidate;
        }

        return transform.position;
    }

    private bool IsWalkable(Vector3 worldPos)
    {
        Case[][] grid = gameManager.GetGridDefinition();
        Vector2Int cell = GridUtils.WorldToGrid(worldPos, step);
        if (cell.x < 0 || cell.x >= grid.Length)          return false;
        if (cell.y < 0 || cell.y >= grid[cell.x].Length)  return false;
        return grid[cell.x][cell.y] != null && grid[cell.x][cell.y].IsGround();
    }
}
