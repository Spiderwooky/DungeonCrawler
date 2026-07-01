using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Générateur de donjon simple inspiré du partitionnement BSP.
// Il crée des salles avec des formes variées et des piliers intérieurs.
public class BspDungeonGenerator : MonoBehaviour
{
    public enum RoomShape
    {
        Rectangle,
        Ellipse,
        Union
        ,Random
    }

    [Header("Dimensions de la grille")]
    [Tooltip("Largeur de la grille en cases (axe X)")]
    public int width = 15;

    [Tooltip("Hauteur de la grille en cases (axe Z)")]
    public int height = 14;

    [Header("Paramètres de la génération")]
    [Tooltip("Taille minimale d'un bloc BSP pour pouvoir le diviser")]
    public int minLeafSize = 6;

    [Tooltip("Taille minimale d'une salle à l'intérieur d'une feuille BSP")]
    public int minRoomSize = 3;

    [Tooltip("Taille maximale d'une salle à l'intérieur d'une feuille BSP")]
    public int maxRoomSize = 8;

    [Tooltip("Forme des salles générées dans chaque feuille BSP")]
    public RoomShape roomShape = RoomShape.Union;

    [Tooltip("Chance de placer un pilier (une case mur isolée) dans les salles")]
    [Range(0f, 1f)]
    public float pillarChance = 0.08f;

    [Header("Room Shape Weights (used when RoomShape.Random is selected)")]
    [Tooltip("Relative weight for selecting Union shapes")]
    public float unionWeight = 0.5f;

    [Tooltip("Relative weight for selecting Ellipse shapes")]
    public float ellipseWeight = 0.25f;

    [Tooltip("Relative weight for selecting Rectangle shapes")]
    public float rectangleWeight = 0.25f;

    [Tooltip("Clé de génération fixe si useRandomSeed est false")]
    public int seed = 0;

    [Tooltip("Utiliser une graine aléatoire à chaque exécution")]
    public bool useRandomSeed = true;

    [Header("Couloirs")]
    [Tooltip("Chance qu'une arête supplémentaire soit ajoutée au graphe des couloirs")]
    [Range(0f, 1f)]
    public float extraEdgeChance = 0.25f;

    [Tooltip("Nombre minimum de liaisons supplémentaires créées au-delà du MST")]
    public int minExtraEdges = 1;

    [Tooltip("Nombre maximum de liaisons supplémentaires créées au-delà du MST")]
    public int maxExtraEdges = 5;

    [Header("Salles")]
    [Tooltip("Nombre minimum d'ennemis pouvant apparaître dans une salle de type Monster.")]
    public int minEnemiesPerRoom = 1;

    [Tooltip("Nombre maximum d'ennemis pouvant apparaître dans une salle de type Monster.")]
    public int maxEnemiesPerRoom = 3;

    [Tooltip("Probabilité qu'une salle (hors salle de départ) soit vide de monstres.")]
    [Range(0f, 1f)]
    public float emptyRoomChance = 0.15f;

    private Case[][] grid;
    private int[][] roomIds;
    private List<RoomInfo> rooms;
    private System.Random random;

    // Cases strictement interdites au carving de couloirs (tout le pourtour des salles
    // pré-faites — murs et porte —, sauf leur unique case de connexion). Sans ça, le A* des
    // couloirs peut tunneliser à travers n'importe quel mur (coût plus élevé mais pas interdit).
    private readonly HashSet<Vector2Int> corridorBlockedCells = new HashSet<Vector2Int>();
    private readonly Case wallCase = new Case(CellType.Wall);
    private readonly Case groundCase = new Case(CellType.Ground);
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,
    };

    // Motifs des salles pré-faites (Start/End), dessinés à la main : W = mur, G = sol,
    // P = porte (visuelle uniquement pour l'instant, infranchissable comme un mur).
    // Convention : ligne 0 du tableau = nord (z max), dernière ligne = sud (z min) ;
    // colonne 0 = ouest (x min), dernière colonne = est (x max). Le prefab de salle est
    // instancié sans rotation : si l'orientation ne correspond pas au modèle, ajuster la
    // rotation du prefab dans l'Inspector (StartRoomPrefab/EndRoomPrefab sur GameManager).
    private static readonly string[] StartRoomPattern =
    {
        "WWGWWW",
        "WGGGWW",
        "WGGGGW",
        "WGGGGW",
        "WWGGGW",
        "WWWPWW",
    };

    private static readonly string[] EndRoomPattern =
    {
        "WWWPWWW",
        "WWGGGWW",
        "WGWGWGW",
        "WGGGGGW",
        "WGWGWGW",
        "WWGGGWW",
        "WWWGWWW",
    };

    // Salle du boss (13 × 10). Le connecteur est au sud (bord bas, col 6).
    // Nécessite des feuilles BSP ≥ 13 × 10 : augmenter width/height (~30×22) et
    // minLeafSize (~7) dans l'Inspector de BspDungeonGenerator pour une placement fiable.
    private static readonly string[] BossRoomPattern =
    {
        "WWWWWWWWWWWWW",
        "WWWGGGGGGGWWW",
        "WWGGGGGGGGGWW",
        "WWGGWGGGWGGWW",
        "WGGGGGGGGGGGW",
        "WGGGWGGGWGGGW",
        "WWGGWGGGWGGWW",
        "WWGGGGGGGGGWW",
        "WWWGGGGGGGWWW",
        "WWWWWWGWWWWWW",
    };

    private void Awake()
    {
        GenerateDungeon();
        LogGridToConsole();
    }

    public Case[][] GetDungeonGrid()
    {
        if (grid == null)
        {
            GenerateDungeon();
        }
        return grid;
    }

    // Identifiant de salle (Leaf.Id) par case, ou -1 pour les couloirs/murs.
    public int[][] GetRoomIds()
    {
        if (roomIds == null)
        {
            GenerateDungeon();
        }
        return roomIds;
    }

    // Métadonnées de chaque salle (type, cases, capacité d'ennemis...).
    public List<RoomInfo> GetRooms()
    {
        if (rooms == null)
        {
            GenerateDungeon();
        }
        return rooms;
    }

    [Header("Gizmos")]
    [Tooltip("Taille en unités monde d'une case pour le dessin des gizmos (doit correspondre à GameManager.step)")]
    public float gizmoCellSize = 5f;

    [Tooltip("Dessiner les gizmos en mode édition (OnDrawGizmos). Si false, les gizmos ne sont dessinés qu'en PlayMode.")]
    public bool drawGizmosInEditor = true;

    private void OnDrawGizmos()
    {
        if (!drawGizmosInEditor && !Application.isPlaying) return;

        if (grid == null)
        {
            // Générer une grille de prévisualisation si nécessaire
            GenerateDungeon();
        }

        if (grid == null) return;

        // Dessiner chaque case sol en vert clair et les murs en gris léger
        for (int x = 0; x < grid.Length; x++)
        {
            for (int z = 0; z < grid[x].Length; z++)
            {
                Vector3 pos = new Vector3(x * gizmoCellSize, 0.1f, z * gizmoCellSize);
                if (grid[x][z] != null && grid[x][z].IsGround())
                {
                    Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.6f);
                    Gizmos.DrawCube(pos, new Vector3(gizmoCellSize, 0.1f, gizmoCellSize));
                }
                else
                {
                    // Optionnel : dessiner les murs plus faiblement
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.08f);
                    Gizmos.DrawCube(pos, new Vector3(gizmoCellSize, 0.02f, gizmoCellSize));
                }
            }
        }
    }

    public void GenerateDungeon()
    {
        if (width < minLeafSize || height < minLeafSize)
        {
            Debug.LogError("La grille est trop petite pour la génération BSP. Vérifiez width/height et minLeafSize.");
            return;
        }

        random = useRandomSeed ? new System.Random() : new System.Random(seed);
        InitializeGrid();

        Leaf root = new Leaf(0, 0, width, height);
        List<Leaf> leaves = SplitLeaves(root);
        ReservePresetRooms(leaves);
        CreateRooms(leaves);
        CarveRooms(leaves);
        ConnectRooms(leaves);
        BuildRoomInfos(leaves);
    }

    // Réserve deux feuilles BSP pour les salles pré-faites Start/End (motifs fixes ci-dessus)
    // avant la génération aléatoire des autres salles : on choisit, parmi toutes les paires de
    // feuilles assez grandes pour chaque motif, celle qui maximise la distance entre les deux —
    // ça les pousse chacune vers une extrémité du donjon plutôt que de figer Start au centre.
    // Si aucune paire valide n'existe (grille trop petite), on se contente d'un avertissement :
    // CreateRooms/BuildRoomInfos retombent alors sur le comportement aléatoire habituel.
    private void ReservePresetRooms(List<Leaf> leaves)
    {
        var startCandidates = FindCandidatesWithRotations(leaves, StartRoomPattern);
        var endCandidates   = FindCandidatesWithRotations(leaves, EndRoomPattern);

        Leaf startLeaf = null;
        Leaf endLeaf   = null;
        float bestDistance = -1f;

        // Étape 1 : trouver la paire de feuilles qui maximise la distance (indépendamment de
        // la rotation — les rotations valides sont évaluées dans l'étape suivante).
        foreach ((Leaf a, _) in startCandidates)
        {
            Vector2 centerA = new Vector2(a.x + a.width / 2f, a.z + a.height / 2f);
            foreach ((Leaf b, _) in endCandidates)
            {
                if (a == b) continue;
                float distance = LeafCenterDistance(b, centerA);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    startLeaf = a;
                    endLeaf = b;
                }
            }
        }

        if (startLeaf != null)
        {
            // Étape 2 : parmi les rotations valides de chaque feuille, choisir celle dont le
            // connecteur pointe le plus vers l'autre salle (couloirs plus directs).
            List<int> startRots = startCandidates.Find(c => c.leaf == startLeaf).validRotations;
            List<int> endRots   = endCandidates  .Find(c => c.leaf == endLeaf)  .validRotations;

            int startRot = ChooseBestRotation(startLeaf, startRots, endLeaf,   StartRoomPattern);
            int endRot   = ChooseBestRotation(endLeaf,   endRots,   startLeaf, EndRoomPattern);

            ApplyPreset(startLeaf, StartRoomPattern, RoomType.Start, startRot);
            ApplyPreset(endLeaf,   EndRoomPattern,   RoomType.End,   endRot);
        }
        else
        {
            Debug.LogWarning($"[BspDungeonGenerator] Aucune feuille BSP assez grande ({StartRoomPattern[0].Length}x{StartRoomPattern.Length} requis) pour la salle de départ pré-faite : repli sur une salle générée aléatoirement. Augmentez width/height si besoin.");
        }

        if (endLeaf == null)
        {
            Debug.LogWarning($"[BspDungeonGenerator] Aucune paire de feuilles BSP assez grandes et assez éloignées ({EndRoomPattern[0].Length}x{EndRoomPattern.Length} requis pour la fin) : pas de salle de fin cette partie. Augmentez width/height si besoin.");
        }

        // ── Salle du boss : loin du Start, différente de Start et End ──────────────
        if (startLeaf != null)
        {
            Vector2 startCenter = new Vector2(startLeaf.x + startLeaf.width / 2f, startLeaf.z + startLeaf.height / 2f);
            var bossCandidates = FindCandidatesWithRotations(leaves, BossRoomPattern);
            bossCandidates.RemoveAll(c => c.leaf == startLeaf || c.leaf == endLeaf);

            Leaf bossLeaf = null;
            float bestBossDist = -1f;
            foreach ((Leaf b, _) in bossCandidates)
            {
                float dist = LeafCenterDistance(b, startCenter);
                if (dist > bestBossDist) { bestBossDist = dist; bossLeaf = b; }
            }

            if (bossLeaf != null)
            {
                List<int> bossRots = bossCandidates.Find(c => c.leaf == bossLeaf).validRotations;
                // Connecteur pointé vers le Start : le joueur entre par le côté « départ ».
                int bossRot = ChooseBestRotation(bossLeaf, bossRots, startLeaf, BossRoomPattern);
                ApplyPreset(bossLeaf, BossRoomPattern, RoomType.Boss, bossRot);
            }
            else
            {
                Debug.LogWarning($"[BspDungeonGenerator] Aucune feuille disponible pour la salle du boss ({BossRoomPattern[0].Length}×{BossRoomPattern.Length} requis). " +
                    "Augmentez width/height (~30×22) et minLeafSize (~7) dans l'Inspector de BspDungeonGenerator.");
            }
        }

        // Bloquer les footprints APRÈS tous les ApplyPreset (Clear() doit précéder tous les Block).
        corridorBlockedCells.Clear();
        foreach (Leaf leaf in leaves)
        {
            if (leaf.IsPreset) BlockFootprintForCorridors(leaf);
        }
    }

    // Pour chaque feuille, liste les rotations (0-3) pour lesquelles le motif tient dans la
    // feuille ET dont le connecteur a de la place à l'extérieur de la grille.
    private List<(Leaf leaf, List<int> validRotations)> FindCandidatesWithRotations(
        List<Leaf> leaves, string[] originalPattern)
    {
        var result = new List<(Leaf, List<int>)>();
        foreach (Leaf leaf in leaves)
        {
            if (leaf.IsPreset) continue;

            var validRots = new List<int>();
            string[] current = originalPattern;
            for (int rot = 0; rot < 4; rot++)
            {
                if (rot > 0) current = RotatePatternCW(current);
                if (leaf.width  < current[0].Length) continue;
                if (leaf.height < current.Length)    continue;

                (int connRow, int connCol) = FindConnectorPosition(current);
                if (!HasRoomForConnectorOutside(leaf, current, connRow, connCol)) continue;

                validRots.Add(rot);
            }
            if (validRots.Count > 0) result.Add((leaf, validRots));
        }
        return result;
    }

    // Parmi les rotations valides, choisit celle dont la direction du connecteur pointe le
    // plus vers la feuille cible (maximise le produit scalaire avec le vecteur inter-centres).
    private int ChooseBestRotation(Leaf leaf, List<int> validRotations, Leaf target, string[] originalPattern)
    {
        if (validRotations.Count == 1) return validRotations[0];

        Vector2 toTarget = (new Vector2(target.x + target.width  / 2f, target.z + target.height / 2f)
                          - new Vector2(leaf.x   + leaf.width    / 2f, leaf.z   + leaf.height   / 2f)).normalized;

        (int connRow, int connCol) = FindConnectorPosition(originalPattern);
        Vector2Int baseDir = GetOutwardDirection(connRow, connCol, originalPattern.Length);
        Vector2 baseDirV   = new Vector2(baseDir.x, baseDir.y);

        int bestRot = validRotations[0];
        float bestDot = float.MinValue;
        foreach (int rot in validRotations)
        {
            float dot = Vector2.Dot(toTarget, RotateVec90CW(baseDirV, rot));
            if (dot > bestDot) { bestDot = dot; bestRot = rot; }
        }
        return bestRot;
    }

    // Rotation d'un vecteur 2D de `times` × 90° horaire dans le plan XZ.
    private static Vector2 RotateVec90CW(Vector2 v, int times)
    {
        for (int i = 0; i < times; i++)
            v = new Vector2(v.y, -v.x);
        return v;
    }

    // Retourne le motif tourné de 90° dans le sens horaire (lignes → colonnes, sens inverse).
    // new[newRow][newCol] = original[H-1-newCol][newRow]
    private static string[] RotatePatternCW(string[] pattern)
    {
        int H = pattern.Length;
        int W = pattern[0].Length;
        char[][] result = new char[W][];
        for (int newRow = 0; newRow < W; newRow++)
        {
            result[newRow] = new char[H];
            for (int newCol = 0; newCol < H; newCol++)
                result[newRow][newCol] = pattern[H - 1 - newCol][newRow];
        }
        return System.Array.ConvertAll(result, row => new string(row));
    }

    // Trouve la position (ligne, colonne) de l'unique case de sol en bord de motif (hors
    // porte) — le connecteur utilisé par ConnectRooms pour relier la salle au reste du donjon.
    private static (int row, int col) FindConnectorPosition(string[] pattern)
    {
        int patternWidth = pattern[0].Length;
        int patternHeight = pattern.Length;

        for (int row = 0; row < patternHeight; row++)
        {
            for (int col = 0; col < patternWidth; col++)
            {
                if (pattern[row][col] == 'G' && IsBoundaryCell(row, col, patternWidth, patternHeight))
                    return (row, col);
            }
        }
        return (-1, -1);
    }

    // Direction vers laquelle le connecteur s'ouvre (vers l'extérieur du motif), déduite du
    // bord du motif sur lequel il se trouve.
    private static Vector2Int GetOutwardDirection(int row, int col, int patternHeight)
    {
        if (row == 0) return new Vector2Int(0, 1);                 // nord (ligne 0 = z max)
        if (row == patternHeight - 1) return new Vector2Int(0, -1); // sud
        if (col == 0) return new Vector2Int(-1, 0);                 // ouest
        return new Vector2Int(1, 0);                                // est
    }

    private void ComputePresetOrigin(Leaf leaf, int patternWidth, int patternHeight, out int originX, out int originZ)
    {
        originX = leaf.x + (leaf.width - patternWidth) / 2;
        originZ = leaf.z + (leaf.height - patternHeight) / 2;
    }

    private bool HasRoomForConnectorOutside(Leaf leaf, string[] pattern, int connectorRow, int connectorCol)
    {
        if (connectorRow < 0) return true; // motif sans connecteur (ne devrait pas arriver ici)

        int patternWidth = pattern[0].Length;
        int patternHeight = pattern.Length;

        ComputePresetOrigin(leaf, patternWidth, patternHeight, out int originX, out int originZ);

        int connectorX = originX + connectorCol;
        int connectorZ = originZ + patternHeight - 1 - connectorRow;

        Vector2Int outward = GetOutwardDirection(connectorRow, connectorCol, patternHeight);
        int outsideX = connectorX + outward.x;
        int outsideZ = connectorZ + outward.y;

        return outsideX >= 0 && outsideX < width && outsideZ >= 0 && outsideZ < height;
    }

    private static float LeafCenterDistance(Leaf leaf, Vector2 point)
    {
        Vector2 center = new Vector2(leaf.x + leaf.width / 2f, leaf.z + leaf.height / 2f);
        return Vector2.Distance(center, point);
    }

    // Interdit au carving de couloirs tout le pourtour (murs + porte) d'une salle pré-faite,
    // sauf son unique case de connexion.
    private void BlockFootprintForCorridors(Leaf leaf)
    {
        RectInt bounds = leaf.FullFootprint;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int z = bounds.yMin; z < bounds.yMax; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                if (cell != leaf.ConnectorCell)
                    corridorBlockedCells.Add(cell);
            }
        }
    }

    // Calcule la position du pivot du prefab 3D pour la rotation N d'un motif original W×H.
    // Quand on tourne le prefab de N×90° CW autour de ce pivot, ses cellules s'alignent
    // exactement sur les cases de la grille inscrites par ApplyPreset.
    private Vector2Int ComputePivotCell(Leaf leaf, string[] originalPattern, int rotation)
    {
        int W = originalPattern[0].Length;
        int H = originalPattern.Length;

        // Offset du coin intérieur sud-ouest dans le motif NON tourné
        // (dMinX = colonnes depuis l'origine, dMinZ = lignes depuis le bas)
        int dMinX = int.MaxValue, dMinZ = int.MaxValue;
        for (int row = 0; row < H; row++)
        {
            int zOff = H - 1 - row;
            for (int col = 0; col < W; col++)
            {
                if (originalPattern[row][col] == 'G' && !IsBoundaryCell(row, col, W, H))
                {
                    if (col  < dMinX) dMinX = col;
                    if (zOff < dMinZ) dMinZ = zOff;
                }
            }
        }

        int ox = leaf.x + (leaf.width  - W) / 2;
        int oz = leaf.z + (leaf.height - H) / 2;
        // Pour 90°/270° les dimensions du motif s'inversent, donc l'origine aussi.
        int newOx = (rotation % 2 == 0) ? ox : leaf.x + (leaf.width  - H) / 2;
        int newOz = (rotation % 2 == 0) ? oz : leaf.z + (leaf.height - W) / 2;

        return (rotation % 4) switch
        {
            1 => new Vector2Int(newOx + dMinZ,         newOz + W - 1 - dMinX),
            2 => new Vector2Int(ox   + W - 1 - dMinX,  oz   + H - 1 - dMinZ),
            3 => new Vector2Int(newOx + H - 1 - dMinZ, newOz + dMinX),
            _ => new Vector2Int(ox   + dMinX,           oz   + dMinZ),
        };
    }

    // Inscrit le motif pré-fait (potentiellement tourné) dans la grille et renseigne la feuille :
    // `room` = bounding box des cases sol intérieures (hors connexion/bordure, utilisée par le
    // prefab 3D), `FullFootprint` = empreinte complète (pour bloquer les couloirs), `PivotCell`
    // = point d'ancrage du prefab calculé par ComputePivotCell, et porte (case + case adjacente).
    private void ApplyPreset(Leaf leaf, string[] originalPattern, RoomType type, int rotation = 0)
    {
        // Tourner le motif avant de l'inscrire dans la grille
        string[] pattern = originalPattern;
        for (int i = 0; i < rotation; i++)
            pattern = RotatePatternCW(pattern);

        int patternWidth  = pattern[0].Length;
        int patternHeight = pattern.Length;

        ComputePresetOrigin(leaf, patternWidth, patternHeight, out int originX, out int originZ);

        leaf.IsPreset       = true;
        leaf.PresetType     = type;
        leaf.PresetRotation = rotation;
        leaf.PivotCell      = ComputePivotCell(leaf, originalPattern, rotation);
        leaf.FullFootprint  = new RectInt(originX, originZ, patternWidth, patternHeight);

        int innerMinX = int.MaxValue, innerMinZ = int.MaxValue;
        int innerMaxX = int.MinValue, innerMaxZ = int.MinValue;

        for (int row = 0; row < patternHeight; row++)
        {
            // Ligne 0 du motif = nord = z le plus grand du rectangle.
            int z = originZ + patternHeight - 1 - row;
            string line = pattern[row];

            for (int col = 0; col < patternWidth; col++)
            {
                int x = originX + col;
                char c = line[col];
                bool boundary = IsBoundaryCell(row, col, patternWidth, patternHeight);

                if (c == 'G')
                {
                    grid[x][z] = groundCase;

                    if (boundary)
                    {
                        leaf.ConnectorCell = new Vector2Int(x, z);
                    }
                    else
                    {
                        if (x < innerMinX) innerMinX = x;
                        if (x > innerMaxX) innerMaxX = x;
                        if (z < innerMinZ) innerMinZ = z;
                        if (z > innerMaxZ) innerMaxZ = z;
                    }
                }
                else
                {
                    grid[x][z] = wallCase;

                    if (c == 'P')
                    {
                        leaf.HasDoor = true;
                        leaf.DoorCell = new Vector2Int(x, z);
                    }
                }
            }
        }

        leaf.room = new RectInt(innerMinX, innerMinZ, innerMaxX - innerMinX + 1, innerMaxZ - innerMinZ + 1);
        leaf.roomA = leaf.room;
        leaf.roomB = null;

        if (leaf.HasDoor)
        {
            leaf.DoorAdjacentCell = FindGroundNeighbor(leaf.DoorCell);
        }
    }

    private static bool IsBoundaryCell(int row, int col, int patternWidth, int patternHeight) =>
        row == 0 || row == patternHeight - 1 || col == 0 || col == patternWidth - 1;

    private Vector2Int FindGroundNeighbor(Vector2Int cell)
    {
        foreach (Vector2Int dir in Directions)
        {
            Vector2Int neighbor = cell + dir;
            if (IsInBounds(grid, neighbor) && grid[neighbor.x][neighbor.y].IsGround())
                return neighbor;
        }
        return cell;
    }

    // Tague roomIds pour les cases de sol d'une salle pré-faite, une fois leaf.Id assigné
    // par CreateRooms (le motif lui-même a déjà été carvé dans grid par ApplyPreset).
    private void StampPresetRoomIds(Leaf leaf)
    {
        if (!leaf.room.HasValue) return;

        RectInt bounds = leaf.room.Value;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int z = bounds.yMin; z < bounds.yMax; z++)
            {
                if (grid[x][z].IsGround())
                    roomIds[x][z] = leaf.Id;
            }
        }
    }

    private void InitializeGrid()
    {
        grid = new Case[width][];
        roomIds = new int[width][];
        for (int x = 0; x < width; x++)
        {
            grid[x] = new Case[height];
            roomIds[x] = new int[height];
            for (int z = 0; z < height; z++)
            {
                grid[x][z] = wallCase;
                roomIds[x][z] = -1;
            }
        }
    }

    // Construit les RoomInfo (type, cases, capacité) pour chaque salle carvée.
    // La salle dont le centre est le plus proche du centre de la grille devient la salle
    // de départ (Start) ; les autres sont Monster, avec une chance d'être vides (Empty).
    private void BuildRoomInfos(List<Leaf> leaves)
    {
        var cellsByRoomId = new Dictionary<int, List<Vector2Int>>();
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                int id = roomIds[x][z];
                if (id < 0) continue;

                if (!cellsByRoomId.TryGetValue(id, out List<Vector2Int> cells))
                {
                    cells = new List<Vector2Int>();
                    cellsByRoomId[id] = cells;
                }
                cells.Add(new Vector2Int(x, z));
            }
        }

        // Salle de départ/fin : la feuille réservée par ReservePresetRooms si elle a pu être
        // placée, sinon (grille trop petite) on retombe sur l'ancienne heuristique pour Start
        // et il n'y a simplement pas de salle End.
        Leaf startLeaf = leaves.Find(l => l.IsPreset && l.PresetType == RoomType.Start) ?? FindStartLeaf(leaves);
        Leaf endLeaf   = leaves.Find(l => l.IsPreset && l.PresetType == RoomType.End);
        Leaf bossLeaf  = leaves.Find(l => l.IsPreset && l.PresetType == RoomType.Boss);

        rooms = new List<RoomInfo>();
        foreach (Leaf leaf in leaves)
        {
            if (!leaf.room.HasValue) continue;
            if (!cellsByRoomId.TryGetValue(leaf.Id, out List<Vector2Int> roomCells) || roomCells.Count == 0) continue;

            RoomType type;
            if (leaf == startLeaf)     type = RoomType.Start;
            else if (leaf == endLeaf)  type = RoomType.End;
            else if (leaf == bossLeaf) type = RoomType.Boss;
            else type = random.NextDouble() < emptyRoomChance ? RoomType.Empty : RoomType.Monster;

            int maxEnemies = type == RoomType.Monster
                ? random.Next(minEnemiesPerRoom, maxEnemiesPerRoom + 1)
                : 0;

            Vector2Int? doorCell = leaf.HasDoor ? leaf.DoorCell : (Vector2Int?)null;
            Vector2Int? doorAdjacentCell = leaf.HasDoor ? leaf.DoorAdjacentCell : (Vector2Int?)null;

            Vector2Int pivotCell = leaf.IsPreset
                ? leaf.PivotCell
                : new Vector2Int(leaf.room.Value.xMin, leaf.room.Value.yMin);

            rooms.Add(new RoomInfo(leaf.Id, type, roomCells, leaf.room.Value, maxEnemies,
                doorCell, doorAdjacentCell, pivotCell, leaf.IsPreset ? leaf.PresetRotation : 0));
        }
    }

    private Leaf FindStartLeaf(List<Leaf> leaves)
    {
        Leaf best = null;
        float bestDistance = float.MaxValue;
        Vector2 gridCenter = new Vector2(width / 2f, height / 2f);

        foreach (Leaf leaf in leaves)
        {
            if (!leaf.room.HasValue) continue;

            float distance = Vector2.Distance(leaf.room.Value.center, gridCenter);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = leaf;
            }
        }

        return best;
    }

    private List<Leaf> SplitLeaves(Leaf root)
    {
        List<Leaf> leaves = new List<Leaf> { root };
        bool didSplit = true;

        while (didSplit)
        {
            didSplit = false;
            for (int i = 0; i < leaves.Count; i++)
            {
                // Ne traiter que les feuilles terminales
                if (leaves[i].IsSplit) continue;

                // Si la feuille peut être divisée selon la taille minimale, on la divise.
                // On retire la condition aléatoire pour garantir que la partition
                // progresse jusqu'à ce que toutes les feuilles soient trop petites.
                if (leaves[i].CanSplit(minLeafSize))
                {
                    if (leaves[i].Split(random, minLeafSize))
                    {
                        leaves.Add(leaves[i].Left);
                        leaves.Add(leaves[i].Right);
                        didSplit = true;
                    }
                }
            }
        }

        // Ne garder que les feuilles terminales
        return leaves.FindAll(leaf => !leaf.IsSplit);
    }

    private void CreateRooms(List<Leaf> leaves)
    {
        int nextRoomId = 0;
        foreach (Leaf leaf in leaves)
        {
            leaf.Id = nextRoomId++;

            // Salle pré-faite (Start/End) : room/roomA/roomB déjà fixés par ApplyPreset.
            if (leaf.IsPreset) continue;

            // Déterminer des limites sûres pour la taille des salles à l'intérieur de la feuille
            int maxAvailableWidth = Mathf.Max(1, leaf.width - 2); // laisser 1 case de bordure de chaque côté
            int maxAvailableHeight = Mathf.Max(1, leaf.height - 2);

            int targetMaxWidth = Mathf.Min(maxAvailableWidth, maxRoomSize);
            int targetMaxHeight = Mathf.Min(maxAvailableHeight, maxRoomSize);

            int roomWidth;
            if (minRoomSize >= targetMaxWidth)
                roomWidth = targetMaxWidth;
            else
                roomWidth = random.Next(minRoomSize, targetMaxWidth + 1);

            int roomHeight;
            if (minRoomSize >= targetMaxHeight)
                roomHeight = targetMaxHeight;
            else
                roomHeight = random.Next(minRoomSize, targetMaxHeight + 1);

            // Calculer une position de salle sûre (avec au moins 1 case de marge)
            int maxOffsetX = Mathf.Max(1, leaf.width - roomWidth - 1);
            int offsetX = (maxOffsetX <= 1) ? 1 : random.Next(1, maxOffsetX + 1);

            int maxOffsetZ = Mathf.Max(1, leaf.height - roomHeight - 1);
            int offsetZ = (maxOffsetZ <= 1) ? 1 : random.Next(1, maxOffsetZ + 1);

            int roomX = leaf.x + offsetX;
            int roomZ = leaf.z + offsetZ;

            leaf.room = new RectInt(roomX, roomZ, roomWidth, roomHeight);
            leaf.roomShape = roomShape == RoomShape.Random ? PickRandomRoomShapeForLeaf(leaf, roomWidth, roomHeight) : roomShape;

            if (roomShape == RoomShape.Union && roomWidth >= minRoomSize * 2 - 1 && roomHeight >= minRoomSize * 2 - 1)
            {
                bool horizontal = random.NextDouble() > 0.5;
                if (horizontal)
                {
                    int widthA = random.Next(minRoomSize, roomWidth - minRoomSize + 2);
                    widthA = Mathf.Clamp(widthA, minRoomSize, roomWidth - minRoomSize + 1);
                    int widthB = roomWidth - widthA + 1;
                    int splitOffsetZ = random.Next(0, Mathf.Max(1, roomHeight - minRoomSize + 1));
                    int heightB = random.Next(minRoomSize, roomHeight - splitOffsetZ + 1);
                    heightB = Mathf.Clamp(heightB, minRoomSize, roomHeight - splitOffsetZ);

                    leaf.roomA = new RectInt(roomX, roomZ, widthA, roomHeight);
                    leaf.roomB = new RectInt(roomX + widthA - 1, roomZ + splitOffsetZ, widthB, heightB);
                }
                else
                {
                    int heightA = random.Next(minRoomSize, roomHeight - minRoomSize + 2);
                    heightA = Mathf.Clamp(heightA, minRoomSize, roomHeight - minRoomSize + 1);
                    int heightB = roomHeight - heightA + 1;
                    int splitOffsetX = random.Next(0, Mathf.Max(1, roomWidth - minRoomSize + 1));
                    int widthB = random.Next(minRoomSize, roomWidth - splitOffsetX + 1);
                    widthB = Mathf.Clamp(widthB, minRoomSize, roomWidth - splitOffsetX);

                    leaf.roomA = new RectInt(roomX, roomZ, roomWidth, heightA);
                    leaf.roomB = new RectInt(roomX + splitOffsetX, roomZ + heightA - 1, widthB, heightB);
                }
            }
            else
            {
                leaf.roomA = leaf.room;
                leaf.roomB = null;
            }
        }
    }

    // Choisit une forme pour une feuille spécifique en tenant compte de la taille de la salle
    private RoomShape PickRandomRoomShapeForLeaf(Leaf leaf, int roomWidth, int roomHeight)
    {
        bool canUnion = roomWidth >= minRoomSize * 2 && roomHeight >= minRoomSize * 2;
        // Use configurable weights; if Union not allowed, treat its weight as zero
        float wUnion = canUnion ? Mathf.Max(0f, unionWeight) : 0f;
        float wEllipse = Mathf.Max(0f, ellipseWeight);
        float wRectangle = Mathf.Max(0f, rectangleWeight);

        float total = wUnion + wEllipse + wRectangle;
        if (total <= 0f)
        {
            // fallback
            return RoomShape.Rectangle;
        }

        double r = random.NextDouble() * total;
        double acc = wUnion;
        if (r < acc && wUnion > 0f) return RoomShape.Union;
        acc += wEllipse;
        if (r < acc && wEllipse > 0f) return RoomShape.Ellipse;
        return RoomShape.Rectangle;
    }

    private void ConnectRooms(List<Leaf> leaves)
    {
        List<Vector2Int> roomCenters = new List<Vector2Int>();
        foreach (Leaf leaf in leaves)
        {
            if (!leaf.room.HasValue) continue;

            // Une salle pré-faite n'a qu'une seule case de sol en bord de motif (hors porte) :
            // les couloirs doivent viser précisément cette case, pas le centre géométrique
            // (qui peut tomber sur un mur/pilier du motif).
            if (leaf.IsPreset)
            {
                roomCenters.Add(leaf.ConnectorCell);
            }
            else
            {
                Vector2 center = leaf.room.Value.center;
                roomCenters.Add(new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y)));
            }
        }

        if (roomCenters.Count < 2) return;

        List<Edge> edges = new List<Edge>();
        for (int i = 0; i < roomCenters.Count; i++)
        {
            for (int j = i + 1; j < roomCenters.Count; j++)
            {
                float distance = Vector2Int.Distance(roomCenters[i], roomCenters[j]);
                edges.Add(new Edge(i, j, distance));
            }
        }

        edges.Sort((a, b) => a.weight.CompareTo(b.weight));

        UnionFind unionFind = new UnionFind(roomCenters.Count);
        List<Edge> corridorEdges = new List<Edge>();

        // Construire un arbre couvrant minimum pour relier toutes les salles.
        foreach (Edge edge in edges)
        {
            if (unionFind.Union(edge.a, edge.b))
            {
                corridorEdges.Add(edge);
            }

            if (corridorEdges.Count >= roomCenters.Count - 1)
                break;
        }

        // Construire la liste des arêtes restantes non utilisées par le MST.
        List<Edge> remainingEdges = new List<Edge>();
        foreach (Edge edge in edges)
        {
            if (!corridorEdges.Contains(edge))
            {
                remainingEdges.Add(edge);
            }
        }

        int extraAdded = 0;

        // Première passe : garantir un nombre minimum d'arêtes supplémentaires.
        for (int i = 0; i < remainingEdges.Count && extraAdded < minExtraEdges; i++)
        {
            corridorEdges.Add(remainingEdges[i]);
            extraAdded++;
        }

        if (extraAdded < minExtraEdges)
        {
            Debug.LogWarning($"[BspDungeonGenerator] minExtraEdges={minExtraEdges} demandé, mais seulement {extraAdded} arêtes supplémentaires disponibles.");
        }

        // Deuxième passe : ajouter de nouvelles arêtes aléatoires jusqu'à maxExtraEdges.
        for (int i = 0; i < remainingEdges.Count && extraAdded < maxExtraEdges; i++)
        {
            Edge edge = remainingEdges[i];
            if (corridorEdges.Contains(edge)) continue;
            if (random.NextDouble() < extraEdgeChance)
            {
                corridorEdges.Add(edge);
                extraAdded++;
            }
        }

        foreach (Edge edge in corridorEdges)
        {
            CarveCorridorBetween(roomCenters[edge.a], roomCenters[edge.b]);
        }
    }

    private void CarveCorridorBetween(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = FindCorridorPath(start, goal);
        if (path == null) return;

        foreach (Vector2Int pos in path)
        {
            grid[pos.x][pos.y] = groundCase;
        }
    }

    private List<Vector2Int> FindCorridorPath(Vector2Int start, Vector2Int goal)
    {
        if (start == goal) return new List<Vector2Int> { start };

        var openSet = new List<CorridorNode>();
        var closedSet = new HashSet<Vector2Int>();
        openSet.Add(new CorridorNode(start, null, 0f, Heuristic(start, goal)));

        while (openSet.Count > 0)
        {
            CorridorNode current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].F < current.F)
                    current = openSet[i];
            }

            openSet.Remove(current);
            closedSet.Add(current.Position);

            if (current.Position == goal)
            {
                return ReconstructPath(current);
            }

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighbor = current.Position + dir;
                if (!IsInBounds(grid, neighbor)) continue;
                if (closedSet.Contains(neighbor)) continue;
                if (corridorBlockedCells.Contains(neighbor)) continue;

                float moveCost = grid[neighbor.x][neighbor.y].IsWall() ? 5f : 1f;
                float g = current.G + moveCost;
                float h = Heuristic(neighbor, goal);
                float f = g + h;

                CorridorNode existing = openSet.Find(node => node.Position == neighbor);
                if (existing != null)
                {
                    if (g < existing.G)
                    {
                        existing.G = g;
                        existing.Parent = current;
                    }
                }
                else
                {
                    openSet.Add(new CorridorNode(neighbor, current, g, h));
                }
            }
        }

        return null;
    }

    private class CorridorNode
    {
        public Vector2Int Position;
        public CorridorNode Parent;
        public float G;
        public float H;
        public float F => G + H;

        public CorridorNode(Vector2Int position, CorridorNode parent, float g, float h)
        {
            Position = position;
            Parent = parent;
            G = g;
            H = h;
        }
    }

    private class Edge
    {
        public int a;
        public int b;
        public float weight;

        public Edge(int a, int b, float weight)
        {
            this.a = a;
            this.b = b;
            this.weight = weight;
        }
    }

    private class UnionFind
    {
        private int[] parent;
        public UnionFind(int size)
        {
            parent = new int[size];
            for (int i = 0; i < size; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] == x) return x;
            parent[x] = Find(parent[x]);
            return parent[x];
        }

        public bool Union(int x, int y)
        {
            int rootX = Find(x);
            int rootY = Find(y);
            if (rootX == rootY) return false;
            parent[rootY] = rootX;
            return true;
        }
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Vector2Int> ReconstructPath(CorridorNode node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    private static bool IsInBounds(Case[][] grid, Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < grid.Length
            && pos.y >= 0 && pos.y < grid[pos.x].Length;
    }

    private void CarveRooms(List<Leaf> leaves)
    {
        foreach (Leaf leaf in leaves)
        {
            if (leaf.IsPreset)
            {
                StampPresetRoomIds(leaf);
                continue;
            }

            if (!leaf.roomA.HasValue) continue;

            if (leaf.roomShape == RoomShape.Union && leaf.roomB.HasValue)
            {
                CarveUnionRoom(leaf);
            }
            else if (leaf.roomShape == RoomShape.Ellipse)
            {
                CarveEllipseRoom(leaf.roomA.Value, leaf.Id);
            }
            else
            {
                CarveRectangleRoom(leaf.roomA.Value, leaf.Id);
            }

            if (pillarChance > 0f)
            {
                PlaceRoomPillars(leaf);
            }
        }
    }

    private void CarveRectangleRoom(RectInt room, int roomId)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int z = room.yMin; z < room.yMax; z++)
            {
                grid[x][z] = groundCase;
                roomIds[x][z] = roomId;
            }
        }
    }

    private void CarveUnionRoom(Leaf leaf)
    {
        CarveRectangleRoom(leaf.roomA.Value, leaf.Id);
        CarveRectangleRoom(leaf.roomB.Value, leaf.Id);

        Vector2Int connectorA = GetConnectorPoint(leaf.roomA.Value, leaf.roomB.Value);
        Vector2Int connectorB = GetConnectorPoint(leaf.roomB.Value, leaf.roomA.Value);
        CarveStraightCorridor(connectorA, connectorB, leaf.Id);
    }

    private Vector2Int GetConnectorPoint(RectInt from, RectInt to)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(to.center.x), from.xMin, from.xMax - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(to.center.y), from.yMin, from.yMax - 1);
        return new Vector2Int(x, z);
    }

    private void CarveStraightCorridor(Vector2Int start, Vector2Int goal, int roomId)
    {
        Vector2Int pos = start;
        grid[pos.x][pos.y] = groundCase;
        roomIds[pos.x][pos.y] = roomId;

        while (pos.x != goal.x)
        {
            pos.x += (goal.x > pos.x) ? 1 : -1;
            grid[pos.x][pos.y] = groundCase;
            roomIds[pos.x][pos.y] = roomId;
        }

        while (pos.y != goal.y)
        {
            pos.y += (goal.y > pos.y) ? 1 : -1;
            grid[pos.x][pos.y] = groundCase;
            roomIds[pos.x][pos.y] = roomId;
        }
    }

    private void CarveEllipseRoom(RectInt room, int roomId)
    {
        Vector2 center = new Vector2((room.xMin + room.xMax - 1) / 2f, (room.yMin + room.yMax - 1) / 2f);
        float radiusX = room.width / 2f;
        float radiusZ = room.height / 2f;

        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int z = room.yMin; z < room.yMax; z++)
            {
                float normalizedX = (x - center.x) / radiusX;
                float normalizedZ = (z - center.y) / radiusZ;
                float radiusLimit = 1f + (Mathf.PerlinNoise((x + seed * 13) * 0.15f, (z + seed * 17) * 0.15f) - 0.5f) * 0.24f;
                if (normalizedX * normalizedX + normalizedZ * normalizedZ <= radiusLimit)
                {
                    grid[x][z] = groundCase;
                    roomIds[x][z] = roomId;
                }
            }
        }
    }

    private void PlaceRoomPillars(Leaf leaf)
    {
        if (!leaf.roomA.HasValue) return;

        PlacePillarsInArea(leaf.roomA.Value);
        if (leaf.roomB.HasValue)
        {
            PlacePillarsInArea(leaf.roomB.Value);
        }
    }

    private void PlacePillarsInArea(RectInt room)
    {
        for (int x = room.xMin + 1; x < room.xMax - 1; x++)
        {
            for (int z = room.yMin + 1; z < room.yMax - 1; z++)
            {
                if (random.NextDouble() < pillarChance && grid[x][z].IsGround())
                {
                    bool surroundedByGround = true;
                    foreach (Vector2Int dir in Directions)
                    {
                        Vector2Int neighbor = new Vector2Int(x, z) + dir;
                        if (!IsInBounds(grid, neighbor) || !grid[neighbor.x][neighbor.y].IsGround())
                        {
                            surroundedByGround = false;
                            break;
                        }
                    }

                    if (surroundedByGround)
                    {
                        grid[x][z] = wallCase;
                        roomIds[x][z] = -1;
                    }
                }
            }
        }
    }

    private void LogGridToConsole()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Dungeon {width}x{height} (rooms only, BSP)");

        for (int z = height - 1; z >= 0; z--)
        {
            for (int x = 0; x < width; x++)
            {
                builder.Append(grid[x][z].IsWall() ? 'W' : 'G');
                builder.Append(' ');
            }
            builder.AppendLine();
        }

        Debug.Log(builder.ToString());
    }

    private class Leaf
    {
        public int x;
        public int z;
        public int width;
        public int height;
        public int Id = -1;
        public Leaf Left;
        public Leaf Right;
        public RectInt? room;
        public RectInt? roomA;
        public RectInt? roomB;
        public RoomShape roomShape;

        // Salle pré-faite (Start/End, voir ApplyPreset) : motif fixe au lieu d'une forme
        // aléatoire. `room` ne couvre que la zone intérieure réellement occupée par le modèle
        // 3D (hors mur/porte/case de couloir, dessinés dans le motif pour la lisibilité) ;
        // `FullFootprint` couvre le motif complet, utilisé uniquement pour bloquer le carving
        // de couloirs sur le pourtour. ConnectorCell est l'unique case de sol en bord de motif
        // (hors porte), visée par ConnectRooms pour relier la salle au reste du donjon.
        public bool IsPreset;
        public RoomType PresetType;
        public int PresetRotation;      // 0-3 × 90° CW appliqués au motif ASCII
        public Vector2Int PivotCell;    // point d'ancrage du prefab 3D (calculé par ComputePivotCell)
        public RectInt FullFootprint;
        public Vector2Int ConnectorCell;
        public bool HasDoor;
        public Vector2Int DoorCell;
        public Vector2Int DoorAdjacentCell;

        public bool IsSplit => Left != null || Right != null;

        public Leaf(int x, int z, int width, int height)
        {
            this.x = x;
            this.z = z;
            this.width = width;
            this.height = height;
            this.Left = null;
            this.Right = null;
            this.room = null;
        }

        public bool CanSplit(int minSize)
        {
            return width >= minSize * 2 || height >= minSize * 2;
        }

        public bool Split(System.Random random, int minSize)
        {
            bool splitHorizontally = ChooseSplitDirection(random);
            if (width < minSize * 2)
            {
                splitHorizontally = false;
            }
            else if (height < minSize * 2)
            {
                splitHorizontally = true;
            }

            if (splitHorizontally)
            {
                int min = minSize;
                int max = height - minSize + 1; // exclusive upper bound for Random.Next
                if (max <= min) return false;
                int splitZ = random.Next(min, max);
                if (splitZ <= 0 || splitZ >= height) return false;
                Left = new Leaf(x, z, width, splitZ);
                Right = new Leaf(x, z + splitZ, width, height - splitZ);
                return true;
            }
            else
            {
                int min = minSize;
                int max = width - minSize + 1; // exclusive upper bound for Random.Next
                if (max <= min) return false;
                int splitX = random.Next(min, max);
                if (splitX <= 0 || splitX >= width) return false;
                Left = new Leaf(x, z, splitX, height);
                Right = new Leaf(x + splitX, z, width - splitX, height);
                return true;
            }
        }

        private bool ChooseSplitDirection(System.Random random)
        {
            if (width > height && width / (float)height >= 1.25f)
            {
                return false;
            }
            else if (height > width && height / (float)width >= 1.25f)
            {
                return true;
            }
            return random.NextDouble() > 0.5;
        }
    }
}
