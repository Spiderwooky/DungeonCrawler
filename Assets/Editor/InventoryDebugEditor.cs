#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Ajoute un panneau de debug sous l'inspecteur par défaut de Inventory : permet d'ajouter
/// n'importe quel ItemData à l'inventaire pendant le Play Mode, pour tester sans avoir à
/// ramasser les objets en jeu.
/// </summary>
[CustomEditor(typeof(Inventory))]
public class InventoryDebugEditor : Editor
{
    private ItemData debugItem;
    private int debugAmount = 1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying) return;

        Inventory inventory = (Inventory)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug (Play Mode)", EditorStyles.boldLabel);

        debugItem = (ItemData)EditorGUILayout.ObjectField("Objet", debugItem, typeof(ItemData), false);
        debugAmount = Mathf.Max(1, EditorGUILayout.IntField("Quantité", debugAmount));

        using (new EditorGUI.DisabledScope(debugItem == null))
        {
            if (GUILayout.Button("Ajouter à l'inventaire"))
                inventory.AddItem(debugItem, debugAmount);
        }
    }
}
#endif
