using System.Collections.Generic;
using UnityEngine;

// Type d'une salle générée par BspDungeonGenerator.
public enum RoomType
{
    Start,   // Salle de départ du joueur : jamais de monstres ni d'objets.
    End,     // Salle de fin du donjon : jamais de monstres ni d'objets.
    Boss,    // Salle du boss : contient un ennemi unique qui droppera la clé. Pas de repop.
    Monster, // Salle pouvant contenir des ennemis, avec repop après un délai.
    Empty,   // Salle sans monstres, pour varier le rythme d'exploration.
}

// Métadonnées d'une salle générée par BspDungeonGenerator, consommées par RoomManager
// (population initiale + repop) et GameManager (choix du point de départ du joueur).
public class RoomInfo
{
    public readonly int Id;
    public readonly RoomType Type;
    public readonly List<Vector2Int> Cells;
    public readonly RectInt Bounds;
    public readonly int MaxEnemies;

    // Uniquement renseignés pour les salles pré-faites (Start/End) ayant une porte dans leur
    // motif : la case de la porte elle-même, et la case de sol juste à l'intérieur (= "devant
    // la porte"). Utilisé par GameManager pour placer/orienter le joueur au départ.
    public readonly Vector2Int? DoorCell;
    public readonly Vector2Int? DoorAdjacentCell;

    // Salles pré-faites seulement : pivot du prefab 3D dans la grille, et rotation appliquée
    // (N × 90° CW). GameManager instancie le prefab à PivotCell avec Quaternion.Euler(0, Rotation*90, 0).
    public readonly Vector2Int PivotCell;
    public readonly int Rotation;

    // État runtime suivi par RoomManager.
    public readonly List<EnemyController> AliveEnemies = new List<EnemyController>();
    public int LastRoundPlayerPresent;

    public RoomInfo(int id, RoomType type, List<Vector2Int> cells, RectInt bounds, int maxEnemies,
        Vector2Int? doorCell = null, Vector2Int? doorAdjacentCell = null,
        Vector2Int pivotCell = default, int rotation = 0)
    {
        Id = id;
        Type = type;
        Cells = cells;
        Bounds = bounds;
        MaxEnemies = maxEnemies;
        DoorCell = doorCell;
        DoorAdjacentCell = doorAdjacentCell;
        PivotCell = pivotCell;
        Rotation = rotation;
    }
}
