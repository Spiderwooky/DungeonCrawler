using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Détecte les murs entre la caméra et le joueur par test d'intersection de bounds
// (les murs n'ont pas de colliders dans ce jeu — la physique est gérée par la grille).
// Active "Debug Mode" dans l'Inspector pour les logs de diagnostic.
public class WallOcclusionFader : MonoBehaviour
{
    [Tooltip("Transform du joueur (sa position est la cible du test d'occlusion).")]
    [SerializeField] private Transform player;

    [Tooltip("Opacité des murs occultants (0 = invisible, 0.15 = légèrement visible).")]
    [SerializeField] [Range(0f, 0.5f)] private float fadedAlpha = 0.15f;

    [Tooltip("Vitesse du fondu (unités d'alpha par seconde).")]
    [SerializeField] private float fadeSpeed = 8f;

    [Tooltip("Tolérance de profondeur pour regrouper les murs d'une même rangée (environ la moitié du pas de grille, défaut 3 pour un pas de 5).")]
    [SerializeField] private float rowDepthTolerance = 3f;

    [Tooltip("Active les logs de diagnostic dans la console (une fois par seconde).")]
    [SerializeField] private bool debugMode = false;

    // ──────────────────────────────────────────────────────────────────────

    private class OccluderState
    {
        public WallOccluder occluder;
        public Material[][] originalMats;
        public Material[][] fadeMats;
        public float alpha = 1f;
        public bool occluding;
        public bool fadeMatsApplied;
    }

    private readonly Dictionary<WallOccluder, OccluderState> _states = new();
    private WallOccluder[] _allOccluders;
    private Camera _cam;
    private float _debugTimer;

    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;

        if (player == null)
        {
            Debug.LogError("[WallOcclusionFader] Player non assigné dans l'Inspector !");
            return;
        }

        // Les murs sont tous instanciés dans GameManager.Awake() (avant ce Start())
        _allOccluders = FindObjectsByType<WallOccluder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (debugMode)
            Debug.Log($"[WallOcclusionFader] {_allOccluders.Length} WallOccluders actifs trouvés dans la scène.");
    }

    private void LateUpdate()
    {
        if (player == null || _cam == null || _allOccluders == null) return;

        Vector3 camPos    = _cam.transform.position;
        Vector3 playerPos = player.position + Vector3.up * 0.5f;
        Vector3 dir     = playerPos - camPos;
        float   dist    = dir.magnitude;
        Vector3 forward = dir / dist;
        var     ray     = new Ray(camPos, forward);

        // ── Passe 1 : profondeur de la rangée la plus proche ────────────
        float minDepth = float.MaxValue;
        foreach (WallOccluder w in _allOccluders)
        {
            if (w == null || !w.gameObject.activeInHierarchy) continue;
            foreach (Renderer r in w.Renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r.bounds.IntersectRay(ray, out float t) && t >= 0f && t <= dist)
                    minDepth = Mathf.Min(minDepth, t);
            }
        }

        // ── Passe 2 : tous les murs à cette profondeur (ligne entière) ───
        var nowOccluding = new HashSet<WallOccluder>();
        if (minDepth < float.MaxValue)
        {
            foreach (WallOccluder w in _allOccluders)
            {
                if (w == null || !w.gameObject.activeInHierarchy) continue;
                foreach (Renderer r in w.Renderers)
                {
                    if (r == null || !r.enabled) continue;
                    float wallDepth = Vector3.Dot(r.bounds.center - camPos, forward);
                    if (wallDepth >= 0f && wallDepth <= dist &&
                        Mathf.Abs(wallDepth - minDepth) <= rowDepthTolerance &&
                        IsFacingCamera(w, forward))
                    {
                        nowOccluding.Add(w);
                        break;
                    }
                }
            }
        }

        // ── Debug ────────────────────────────────────────────────────────
        if (debugMode)
        {
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= 1f)
            {
                _debugTimer = 0f;
                Debug.Log($"[WallOcclusionFader] WallOccluders total : {_allOccluders.Length}  |  occultants : {nowOccluding.Count}");
            }
        }

        // ── Mise à jour des états ────────────────────────────────────────
        foreach (WallOccluder w in nowOccluding)
        {
            if (!_states.TryGetValue(w, out OccluderState s))
            {
                s = CreateState(w);
                _states[w] = s;
                if (debugMode)
                    Debug.Log($"[WallOcclusionFader] Nouveau mur occultant : {w.gameObject.name}  shader : {s.fadeMats[0][0].shader.name}");
            }
            s.occluding = true;
        }
        foreach (var kv in _states)
            if (!nowOccluding.Contains(kv.Key))
                kv.Value.occluding = false;

        // ── Animation ───────────────────────────────────────────────────
        var done = new List<WallOccluder>();
        foreach (var kv in _states)
        {
            OccluderState s = kv.Value;
            float target = s.occluding ? fadedAlpha : 1f;
            s.alpha = Mathf.MoveTowards(s.alpha, target, fadeSpeed * Time.deltaTime);

            if (s.alpha < 1f)
            {
                if (!s.fadeMatsApplied) ApplyFadeMaterials(s);
                SetAlpha(s, s.alpha);
            }

            if (!s.occluding && Mathf.Approximately(s.alpha, 1f))
            {
                RestoreOriginalMaterials(s);
                done.Add(kv.Key);
            }
        }
        foreach (WallOccluder w in done) _states.Remove(w);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Orientation
    // ──────────────────────────────────────────────────────────────────────

    // Vrai si le mur fait face à la caméra.
    // Utilise la FacingDirection calculée depuis la localPosition du WallOccluder :
    //   un mur "face caméra" a sa direction de face grossièrement opposée à cameraForward.
    //   Seuil −0.1 : exclut les murs dos à la caméra (dot > 0) et les murs latéraux
    //   (dot ≈ 0), tout en tolérant les caméras jusqu'à ~84° d'angle.
    //   Vector3.zero (pilier/coin) → toujours masqué.
    private static bool IsFacingCamera(WallOccluder w, Vector3 cameraForward)
    {
        if (w.FacingDirection == Vector3.zero) return true;
        return Vector3.Dot(w.FacingDirection, cameraForward) < -0.1f;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Matériaux
    // ──────────────────────────────────────────────────────────────────────

    private static OccluderState CreateState(WallOccluder w)
    {
        Renderer[] renderers = w.Renderers;
        var s = new OccluderState
        {
            occluder     = w,
            originalMats = new Material[renderers.Length][],
            fadeMats     = new Material[renderers.Length][]
        };
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] orig = renderers[i].sharedMaterials;
            s.originalMats[i] = orig;
            s.fadeMats[i] = new Material[orig.Length];
            for (int j = 0; j < orig.Length; j++)
                s.fadeMats[i][j] = MakeFadeMaterial(orig[j]);
        }
        return s;
    }

    private static Material MakeFadeMaterial(Material src)
    {
        var m = new Material(src);
        string shader = src.shader.name;

        if (shader.Contains("Universal Render Pipeline") || shader.Contains("Lit") || shader.Contains("Unlit"))
        {
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_ZWrite", 0);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
        }
        else if (shader.Contains("Standard"))
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        return m;
    }

    private static void ApplyFadeMaterials(OccluderState s)
    {
        Renderer[] renderers = s.occluder.Renderers;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterials = s.fadeMats[i];
        s.fadeMatsApplied = true;
    }

    private static void SetAlpha(OccluderState s, float alpha)
    {
        foreach (Material[] mats in s.fadeMats)
            foreach (Material m in mats)
            {
                if (m.HasProperty("_BaseColor"))
                {
                    Color c = m.GetColor("_BaseColor");
                    m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
                }
                else if (m.HasProperty("_Color"))
                {
                    Color c = m.GetColor("_Color");
                    m.SetColor("_Color", new Color(c.r, c.g, c.b, alpha));
                }
            }
    }

    private static void RestoreOriginalMaterials(OccluderState s)
    {
        Renderer[] renderers = s.occluder.Renderers;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterials = s.originalMats[i];
        s.fadeMatsApplied = false;

        foreach (Material[] mats in s.fadeMats)
            foreach (Material m in mats)
                Destroy(m);
    }
}
