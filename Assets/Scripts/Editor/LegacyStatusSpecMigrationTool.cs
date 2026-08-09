#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ย้ายข้อมูล status เก่าที่ยังนอนอยู่ใน legacy flat field ([SerializeField, HideInInspector]) ขึ้นมาไว้ที่
/// StatusApplicationSpec เพื่อให้ designer เห็นและแก้ StatusEffectDef ได้จาก Inspector.
///
/// ก่อน migrate ช่อง Inspector จะโชว์ "None (Status Effect Def)" ทั้งที่ asset มีข้อมูลจริง เพราะค่าอยู่ใน
/// legacy field ที่ซ่อนไว้ แล้ว ResolvedSpec() ค่อย fallback ไปอ่านตอนรัน (ดูหัวข้อ Legacy Migration ท้ายไฟล์
/// StatusEffects/StatusApplicationSpec.cs). tool นี้คัดลอกค่าเดิมลง spec ครั้งเดียว ไม่ลบ legacy field ทิ้ง
/// เพราะ ResolvedSpec() เช็ค spec.effect ก่อนเสมอ — ค่าที่ย้ายแล้วจะ override ของเดิมโดยไม่เปลี่ยนพฤติกรรม.
///
/// สแกนเฉพาะ .asset (ScriptableObject) ไม่แตะ prefab — apply-site ที่เป็น MonoBehaviour มีแค่
/// ApplyStatusOnHitModule ซึ่ง nest StatusApplicationSpec ตรงๆ อยู่แล้ว ไม่มี legacy field ให้ย้าย.
/// </summary>
public static class LegacyStatusSpecMigrationTool
{
    /// <summary>ชื่อ field ของแต่ละ apply-site shape: legacy effect / legacy stacks / spec ปลายทาง.</summary>
    private static readonly (string effect, string stacks, string spec)[] Shapes =
    {
        ("effect", "stacks", "spec"),
        ("statusEffect", "statusInitialStacks", "statusSpec"),
    };

    /// <summary>โฟลเดอร์ที่เก็บ data asset ของโปรเจกต์จริง — apply-site ทั้ง 5 แบบอยู่ในนี้ทั้งหมด.</summary>
    private static readonly string[] SearchFolders =
    {
        "Assets/Data",
        "Assets/Character",
        "Assets/Scripts",
    };

    /// <summary>เพดานฉุกเฉินสำหรับ asset ที่มี serialized graph ผิดปกติ.</summary>
    private const int MaxPropertyVisits = 10000;

    [MenuItem("Tools/RB/Status Effects/Migrate Legacy Status Specs (Dry Run)")]
    private static void DryRunMenu() => Run(dryRun: true);

    [MenuItem("Tools/RB/Status Effects/Migrate Legacy Status Specs")]
    private static void MigrateMenu() => Run(dryRun: false);

    private static void Run(bool dryRun)
    {
        var report = new StringBuilder();
        int migratedFields = 0;
        int migratedAssets = 0;
        int inspectedAssets = 0;

        List<string> candidatePaths = FindCandidateAssets();
        try
        {
            for (int candidateIndex = 0; candidateIndex < candidatePaths.Count; candidateIndex++)
            {
                string path = candidatePaths[candidateIndex];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Migrate Legacy Status Specs",
                        path,
                        candidatePaths.Count == 0 ? 1f : (float)candidateIndex / candidatePaths.Count))
                {
                    Debug.LogWarning(
                        $"{(dryRun ? "[Dry Run] " : string.Empty)}Legacy status migration cancelled after " +
                        $"inspecting {inspectedAssets} of {candidatePaths.Count} candidate asset(s).");
                    return;
                }

                inspectedAssets++;

                // asset เดียวอาจมี sub-asset หลายตัว (payload ที่ embed อยู่ในสกิล) ต้องไล่ทุกตัวไม่ใช่แค่ main asset
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                bool assetChanged = false;

                for (int a = 0; a < assets.Length; a++)
                {
                    Object asset = assets[a];
                    if (asset == null)
                        continue;

                    var serializedObject = new SerializedObject(asset);
                    int changed = MigrateObject(serializedObject, path, asset.name, report);
                    if (changed == 0)
                        continue;

                    migratedFields += changed;
                    assetChanged = true;

                    if (dryRun)
                        continue;

                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }

                if (assetChanged)
                    migratedAssets++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!dryRun && migratedAssets > 0)
            AssetDatabase.SaveAssets();

        // ไม่ใช้ EditorUtility.DisplayDialog — modal dialog บล็อก editor จน automation/MCP เรียก menu item นี้ไม่ได้
        string mode = dryRun ? "[Dry Run] " : string.Empty;
        if (migratedFields == 0)
        {
            Debug.Log($"{mode}No legacy status data left to migrate ({inspectedAssets} candidate asset(s) inspected).");
            return;
        }

        Debug.Log(
            $"{mode}Migrated {migratedFields} legacy status field(s) across {migratedAssets} of " +
            $"{inspectedAssets} candidate asset(s).\n{report}");
    }

    /// <summary>
    /// กรองด้วยการอ่าน YAML เป็น text ก่อน แล้วค่อยโหลด asset ที่มี legacy field จริงเข้า memory — การโหลด
    /// ScriptableObject ทุกตัวใต้ Assets แล้วเดิน property tree ทั้งหมดทำให้ editor ค้างยาวจน MCP หลุด connection.
    /// </summary>
    private static List<string> FindCandidateAssets()
    {
        var candidates = new List<string>();

        for (int i = 0; i < SearchFolders.Length; i++)
        {
            string absoluteFolder = Path.Combine(Directory.GetCurrentDirectory(), SearchFolders[i]);
            if (!Directory.Exists(absoluteFolder))
                continue;

            string[] files = Directory.GetFiles(absoluteFolder, "*.asset", SearchOption.AllDirectories);
            for (int f = 0; f < files.Length; f++)
            {
                string text;
                try
                {
                    text = File.ReadAllText(files[f]);
                }
                catch (IOException)
                {
                    continue;
                }

                if (!HasLegacyFieldText(text))
                    continue;

                candidates.Add(ToProjectRelativePath(files[f]));
            }
        }

        return candidates;
    }

    private static bool HasLegacyFieldText(string text)
    {
        for (int s = 0; s < Shapes.Length; s++)
        {
            // legacy field ที่ว่างจะ serialize เป็น {fileID: 0} — เช็ค "{fileID: " กว้างๆ ไว้ก่อน
            // แล้วให้ MigrateOne คัดตัวที่ไม่มีค่าจริงออกอีกที
            if (text.Contains(Shapes[s].effect + ": {fileID: "))
                return true;
        }

        return false;
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string normalized = absolutePath.Replace('\\', '/');
        int assetsIndex = normalized.LastIndexOf("/Assets/", System.StringComparison.Ordinal);
        return assetsIndex >= 0 ? normalized.Substring(assetsIndex + 1) : normalized;
    }

    private static int MigrateObject(
        SerializedObject serializedObject,
        string assetPath,
        string assetName,
        StringBuilder report)
    {
        // เก็บ path ให้ครบก่อนแล้วค่อยแก้ — แก้ระหว่างที่ iterator ยังเดินอยู่ทำให้ iterator หลุด
        var specPaths = new List<(string path, int shapeIndex)>();
        var visitedManagedReferences = new HashSet<long>();

        SerializedProperty iterator = serializedObject.GetIterator();
        int visits = 0;
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = true;
            if (++visits > MaxPropertyVisits)
            {
                Debug.LogError(
                    $"Aborted scanning '{assetPath}' [{assetName}] after {MaxPropertyVisits} properties — " +
                    "the serialized graph is unexpectedly large or malformed. This asset was NOT migrated.");
                return 0;
            }

            if (iterator.propertyType == SerializedPropertyType.ManagedReference)
            {
                long referenceId = iterator.managedReferenceId;
                enterChildren = referenceId < 0 || visitedManagedReferences.Add(referenceId);
            }

            for (int s = 0; s < Shapes.Length; s++)
            {
                if (iterator.name != Shapes[s].spec)
                    continue;

                specPaths.Add((iterator.propertyPath, s));
                break;
            }
        }

        int migrated = 0;
        for (int i = 0; i < specPaths.Count; i++)
        {
            (string specPath, int shapeIndex) = specPaths[i];
            if (MigrateOne(serializedObject, specPath, Shapes[shapeIndex], assetPath, assetName, report))
                migrated++;
        }

        return migrated;
    }

    private static bool MigrateOne(
        SerializedObject serializedObject,
        string specPath,
        (string effect, string stacks, string spec) shape,
        string assetPath,
        string assetName,
        StringBuilder report)
    {
        SerializedProperty spec = serializedObject.FindProperty(specPath);
        SerializedProperty specEffect = spec?.FindPropertyRelative("effect");
        if (specEffect == null)
            return false;

        // designer author ค่าใหม่ไว้แล้ว — legacy field กลายเป็น dead data ไม่ต้องแตะ
        if (specEffect.objectReferenceValue != null)
            return false;

        SerializedProperty legacyEffect = FindSibling(serializedObject, specPath, shape.spec, shape.effect);
        if (legacyEffect == null || legacyEffect.objectReferenceValue == null)
            return false;

        SerializedProperty legacyStacks = FindSibling(serializedObject, specPath, shape.spec, shape.stacks);
        int stacks = legacyStacks != null && legacyStacks.intValue > 0 ? legacyStacks.intValue : 1;

        specEffect.objectReferenceValue = legacyEffect.objectReferenceValue;

        SerializedProperty specStacks = spec.FindPropertyRelative("stacks");
        if (specStacks != null)
            specStacks.intValue = stacks;

        report.AppendLine(
            $"{assetPath} [{assetName}] {specPath} <- {legacyEffect.objectReferenceValue.name} x{stacks}");
        return true;
    }

    /// <summary>
    /// หา field พี่น้องของ spec ที่อยู่ใน container เดียวกัน — ตัดชื่อ spec ท้าย propertyPath ออกแล้วต่อชื่อ
    /// legacy field แทน. spec ที่อยู่ระดับ root ของ ScriptableObject (เช่น ApplyStatusPickupEffectDef) จะไม่มี
    /// prefix อะไรเลย จึงหาจากชื่อ field ตรงๆ.
    /// </summary>
    private static SerializedProperty FindSibling(
        SerializedObject serializedObject,
        string specPath,
        string specFieldName,
        string siblingFieldName)
    {
        if (specPath == specFieldName)
            return serializedObject.FindProperty(siblingFieldName);

        string suffix = "." + specFieldName;
        if (!specPath.EndsWith(suffix, System.StringComparison.Ordinal))
            return null;

        string containerPath = specPath.Substring(0, specPath.Length - suffix.Length);
        SerializedProperty container = serializedObject.FindProperty(containerPath);
        return container?.FindPropertyRelative(siblingFieldName);
    }
}
#endif
