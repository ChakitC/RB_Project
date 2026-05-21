#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ShopSpawner))]
public class ShopSpawnerEditor : Editor
{
    SerializedProperty previewTier;
    SerializedProperty previewShop;

    void OnEnable()
    {
        previewTier = serializedObject.FindProperty("previewTier");
        previewShop = serializedObject.FindProperty("previewShop");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("เครื่องมือจัดตำแหน่ง Shop", EditorStyles.boldLabel);

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("เลือก ShopSpawner ทีละตัวเพื่อใช้เครื่องมือสร้าง Shop ตัวอย่าง", MessageType.Info);
            return;
        }

        var spawner = (ShopSpawner)target;

        if (GUILayout.Button("สร้าง Shop ตัวอย่างใน Scene"))
            CreatePreviewShop(spawner);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("บันทึกตำแหน่งจากตัวอย่าง"))
                SavePreviewTransform(spawner);

            if (GUILayout.Button("ลบ Shop ตัวอย่าง"))
                ClearPreviewShop(spawner);
        }
    }

    void CreatePreviewShop(ShopSpawner spawner)
    {
        if (spawner == null)
            return;

        serializedObject.Update();

        int tier = previewTier != null ? previewTier.intValue : 1;
        if (!spawner.TryGetTierEntry(tier, out ShopSpawner.ShopTierEntry entry) &&
            !spawner.TryGetFirstValidTierEntry(out entry))
        {
            Debug.LogWarning("ShopSpawner: ยังไม่มี Tier/Tire ที่ตั้งค่า prefab และ weight มากกว่า 0", spawner);
            return;
        }

        ClearPreviewShop(spawner);

        var previewObject = PrefabUtility.InstantiatePrefab(entry.prefab) as GameObject;
        if (previewObject == null)
            previewObject = Instantiate(entry.prefab);

        Undo.RegisterCreatedObjectUndo(previewObject, "Create Shop Preview");

        previewObject.transform.SetParent(spawner.transform, true);

        previewObject.transform.SetPositionAndRotation(
            spawner.ResolveSpawnPosition(),
            spawner.ResolveSpawnRotation());

        previewObject.name = $"{entry.prefab.name}_PositionPreview_Tier_{entry.tier}";
        TryTagEditorOnly(previewObject);

        if (previewShop != null)
            previewShop.objectReferenceValue = previewObject;

        serializedObject.ApplyModifiedProperties();
        MarkSpawnerDirty(spawner);
        Selection.activeGameObject = previewObject;
    }

    void SavePreviewTransform(ShopSpawner spawner)
    {
        if (spawner == null)
            return;

        serializedObject.Update();
        var previewObject = previewShop != null ? previewShop.objectReferenceValue as GameObject : null;

        if (previewObject == null)
        {
            Debug.LogWarning("ShopSpawner: ยังไม่มี Shop ตัวอย่างให้บันทึกตำแหน่ง", spawner);
            return;
        }

        Undo.RecordObject(spawner, "Save Shop Spawn Transform");
        spawner.SaveSpawnTransform(previewObject.transform.position, previewObject.transform.rotation);

        serializedObject.Update();
        MarkSpawnerDirty(spawner);
    }

    void ClearPreviewShop(ShopSpawner spawner)
    {
        serializedObject.Update();
        var previewObject = previewShop != null ? previewShop.objectReferenceValue as GameObject : null;

        if (previewObject != null && !EditorUtility.IsPersistent(previewObject))
            Undo.DestroyObjectImmediate(previewObject);

        if (previewShop != null)
            previewShop.objectReferenceValue = null;

        serializedObject.ApplyModifiedProperties();

        if (spawner != null)
            MarkSpawnerDirty(spawner);
    }

    static void TryTagEditorOnly(GameObject previewObject)
    {
        if (previewObject == null)
            return;

        try
        {
            previewObject.tag = "EditorOnly";
        }
        catch (UnityException)
        {
            // Some projects remove built-in tags from TagManager; the preview still works without it.
        }
    }

    static void MarkSpawnerDirty(ShopSpawner spawner)
    {
        EditorUtility.SetDirty(spawner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);

        if (spawner.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
    }
}
#endif
