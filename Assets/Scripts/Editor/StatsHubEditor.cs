#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(StatsHub))]
public class StatsHubEditor : Editor
{
    static bool debugFoldout = true;

    SerializedProperty script;
    SerializedProperty ctx;
    SerializedProperty weapon;
    SerializedProperty statusEffectController;
    SerializedProperty debugInInspector;
    SerializedProperty useUnscaledTime;
    SerializedProperty debugRefreshInterval;
    SerializedProperty[] debugValues;

    static readonly string[] DebugValuePropertyNames =
    {
        "dbgWeaponName",
        "dbgWeaponType",
        "dbgFiringMode",
        "dbgBaseCharDamage",
        "dbgBaseCharCritRatePercent",
        "dbgBaseCharCritMult",
        "dbgWeaponDamage",
        "dbgWeaponCritRatePercent",
        "dbgWeaponCritMult",
        "dbgWeaponFireInterval",
        "dbgWeaponReloadTime",
        "dbgWeaponStability",
        "dbgWeaponBulletSpeed",
        "dbgWeaponMaxMagazine",
        "dbgWeaponMaxReserveAmmo",
        "dbgFinalDamage",
        "dbgFinalArmor",
        "dbgFinalMoveSpeed",
        "dbgFinalCritRatePercent",
        "dbgFinalCritRate01",
        "dbgFinalCritMult",
        "dbgFinalFireInterval",
        "dbgFinalReloadTime",
        "dbgFinalStability",
        "dbgFinalBulletSpeed",
        "dbgFinalMaxMagazine",
        "dbgFinalMaxReserveAmmo",
        "dbgFinalMaxHP",
        "dbgFinalMaxStamina",
        "dbgFinalStaminaRegen",
        "dbgFinalMaxEnergy"
    };

    void OnEnable()
    {
        script = serializedObject.FindProperty("m_Script");
        ctx = serializedObject.FindProperty("ctx");
        weapon = serializedObject.FindProperty("weapon");
        statusEffectController = serializedObject.FindProperty("statusEffectController");
        debugInInspector = serializedObject.FindProperty("debugInInspector");
        useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
        debugRefreshInterval = serializedObject.FindProperty("debugRefreshInterval");

        debugValues = new SerializedProperty[DebugValuePropertyNames.Length];
        for (int i = 0; i < DebugValuePropertyNames.Length; i++)
            debugValues[i] = serializedObject.FindProperty(DebugValuePropertyNames[i]);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(script);

        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(ctx);
        EditorGUILayout.PropertyField(weapon);
        EditorGUILayout.PropertyField(statusEffectController);

        EditorGUILayout.Space(2f);
        debugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(debugFoldout, "Debug (Inspector)");
        if (debugFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(debugInInspector);
            EditorGUILayout.PropertyField(useUnscaledTime);
            EditorGUILayout.PropertyField(debugRefreshInterval);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Debug Values (read-only)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < debugValues.Length; i++)
                    EditorGUILayout.PropertyField(debugValues[i]);
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    public override bool RequiresConstantRepaint()
    {
        return EditorApplication.isPlaying;
    }
}
#endif
