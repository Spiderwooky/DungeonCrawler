using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LootEntry
{
    public ItemData item;
    public int minAmount = 1;
    public int maxAmount = 1;
    [Range(0f, 1f)] public float dropChance = 1f;
}

[CreateAssetMenu(menuName = "Loot/Loot Table", fileName = "NewLootTable")]
public class LootTable : ScriptableObject
{
    [SerializeField] private LootEntry[] entries;

    public List<(ItemData item, int amount)> Roll()
    {
        var result = new List<(ItemData, int)>();
        if (entries == null) return result;
        foreach (LootEntry entry in entries)
        {
            if (entry.item == null) continue;
            if (Random.value <= entry.dropChance)
                result.Add((entry.item, Random.Range(entry.minAmount, entry.maxAmount + 1)));
        }
        return result;
    }
}
