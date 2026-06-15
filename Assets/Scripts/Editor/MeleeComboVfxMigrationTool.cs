#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MeleeComboVfxMigrationTool
{
    [MenuItem("Tools/RB/Animation VFX/Assign Missing Melee Step IDs")]
    static void AssignMissingMeleeStepIds()
    {
        string[] guids = AssetDatabase.FindAssets("t:MeleeComboSO");
        int changedCount = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MeleeComboSO combo = AssetDatabase.LoadAssetAtPath<MeleeComboSO>(path);
            if (combo == null)
                continue;

            Undo.RecordObject(combo, "Assign Melee Step IDs");
            if (!combo.EnsureUniqueStepEntryIds())
                continue;

            EditorUtility.SetDirty(combo);
            changedCount++;
        }

        if (changedCount > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"Animation VFX migration assigned missing or duplicate IDs in {changedCount} Melee Combo asset(s).");
    }
}
#endif
