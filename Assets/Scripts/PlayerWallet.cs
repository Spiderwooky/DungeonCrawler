using System;
using UnityEngine;

/// <summary>
/// Or possédé par le joueur. À placer sur le même GameObject que Inventory
/// (ex : CopilotPlayerController). Seule source de vérité pour les dépenses/gains —
/// les marchands passent toujours par TrySpend/Add, jamais par une écriture directe.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    [SerializeField] private int startingGold = 20;

    public event Action<int> OnGoldChanged;

    public int CurrentGold { get; private set; }

    private void Awake()
    {
        CurrentGold = Mathf.Max(0, startingGold);
    }

    /// <summary>Dépense `amount` pièces si possible. Ne fait rien et renvoie false sinon.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (CurrentGold < amount) return false;

        CurrentGold -= amount;
        OnGoldChanged?.Invoke(CurrentGold);
        return true;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;

        CurrentGold += amount;
        OnGoldChanged?.Invoke(CurrentGold);
    }
}
