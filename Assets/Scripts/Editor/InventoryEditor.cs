#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerInventory))]
public class InventoryEditor : Editor
{
    public override void OnInspectorGUI()
    {
    
        base.OnInspectorGUI();
        
        PlayerInventory inv = (PlayerInventory)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Inventory Debug View", EditorStyles.boldLabel);

        if (inv.slots == null || inv.slots.Count == 0)
        {
            EditorGUILayout.HelpBox("No slots.", MessageType.Info);
            return;
        }
        
        for (int i = 0; i < inv.slots.Count; i++)
        {
            var slot = inv.slots[i];

            string text;
            if (slot == null || slot.IsEmpty)
            {
                text = $"Slot {i}: (empty)";
            }
            else if (slot.HasWeaponInstance && slot.weaponInstance != null)
            {
                string name = slot.item != null ? slot.item.displayName : slot.weaponInstance.baseWeaponId;
                string rarity = slot.weaponInstance.rarity.ToString();
                text = $"Slot {i}: {name} [{rarity}] id={slot.weaponInstance.instanceId}";
            }
            else
            {
                string name = slot.item != null ? slot.item.displayName : "(null item)";
                text = $"Slot {i}: {name} x{slot.amount}";
            }

            EditorGUILayout.LabelField(text);
        }
    }
}
#endif
