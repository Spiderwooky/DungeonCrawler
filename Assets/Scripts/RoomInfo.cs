using System.Collections.Generic;
using UnityEngine;

// Type d'une salle générée par BspDungeonGenerator.
public enum RoomType
{
    Start,   // Salle de départ du joueur : jamais de monstres ni d'objets.
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

    // État runtime suivi par RoomManager.
    public readonly List<EnemyController> AliveEnemies = new List<EnemyController>();
    public int LastRoundPlayerPresent;

    public RoomInfo(int id, RoomType type, List<Vector2Int> cells, RectInt bounds, int maxEnemies)
    {
        Id = id;
        Type = type;
        Cells = cells;
        Bounds = bounds;
        MaxEnemies = maxEnemies;
    }
}
