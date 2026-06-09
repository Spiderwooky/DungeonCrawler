using UnityEngine;
using System;
using System.Collections.Generic;
 
// Définit qui peut agir pendant un tour.
public enum TurnOwner
{
    Player,
    Enemies,
}
 
// Interface à implémenter par tout acteur qui joue un tour (joueur, ennemis…)
public interface ITurnActor
{
    // Appelé par le TurnManager quand c'est au tour de cet acteur.
    // L'acteur doit appeler EndTurn() sur le TurnManager quand il a terminé.
    void OnTurnStart();
}
 
/// <summary>
/// Orchestre le déroulement des tours : Joueur → Ennemis → Joueur → …
/// 
/// Usage :
///   1. Les acteurs s'enregistrent via RegisterActor().
///   2. Appelez StartGame() pour lancer le premier tour.
///   3. Chaque acteur appelle TurnManager.Instance.EndTurn() quand il a terminé.
/// </summary>
public class TurnManager : MonoBehaviour
{
    // Singleton accessible depuis n'importe quel script
    public static TurnManager Instance { get; private set; }
 
    // Événements auxquels les scripts peuvent s'abonner
    public event Action<TurnOwner> OnTurnChanged;
    public event Action<int> OnRoundStarted; // int = numéro de round
 
    private TurnOwner currentTurn = TurnOwner.Player;
    private int roundNumber = 0;
 
    // Liste des acteurs ennemis enregistrés
    private readonly List<ITurnActor> enemyActors = new List<ITurnActor>();
 
    // Référence au joueur (un seul joueur pour l'instant)
    private ITurnActor playerActor;
 
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
 
    // ──────────────────────────────────────────
    // Enregistrement des acteurs
    // ──────────────────────────────────────────
 
    public void RegisterPlayer(ITurnActor player)
    {
        playerActor = player;
    }
 
    public void RegisterEnemy(ITurnActor enemy)
    {
        if (!enemyActors.Contains(enemy))
            enemyActors.Add(enemy);
    }
 
    public void UnregisterEnemy(ITurnActor enemy)
    {
        enemyActors.Remove(enemy);
    }
 
    // ──────────────────────────────────────────
    // Cycle de jeu
    // ──────────────────────────────────────────
 
    // Démarre la partie : déclenche le premier tour du joueur.
    public void StartGame()
    {
        roundNumber = 1;
        currentTurn = TurnOwner.Player;
        BeginCurrentTurn();
    }
 
    // Appelé par l'acteur courant quand il a terminé son action.
    public void EndTurn()
    {
        switch (currentTurn)
        {
            case TurnOwner.Player:
                currentTurn = TurnOwner.Enemies;
                break;
            case TurnOwner.Enemies:
                roundNumber++;
                currentTurn = TurnOwner.Player;
                OnRoundStarted?.Invoke(roundNumber);
                break;
        }
 
        BeginCurrentTurn();
    }
 
    private void BeginCurrentTurn()
    {
        OnTurnChanged?.Invoke(currentTurn);
 
        switch (currentTurn)
        {
            case TurnOwner.Player:
                playerActor?.OnTurnStart();
                break;
 
            case TurnOwner.Enemies:
                if (enemyActors.Count == 0)
                {
                    // Pas d'ennemis : on passe immédiatement au tour suivant
                    EndTurn();
                    return;
                }
                foreach (ITurnActor enemy in enemyActors)
                {
                    enemy.OnTurnStart();
                }
                break;
        }
    }
 
    // ──────────────────────────────────────────
    // Utilitaires
    // ──────────────────────────────────────────
 
    public TurnOwner GetCurrentTurn() => currentTurn;
    public int GetRoundNumber() => roundNumber;
    public bool IsPlayerTurn() => currentTurn == TurnOwner.Player;
}