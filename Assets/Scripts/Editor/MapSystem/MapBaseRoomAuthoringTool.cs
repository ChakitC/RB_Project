#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the neutral map-room topology prefabs. The visual hierarchy is deliberately empty so
/// artists can style a room without touching its runtime sockets, entrance spawns, or navigation.
/// </summary>
public static class MapBaseRoomAuthoringTool
{
    const string TemplatePath = "Assets/Prefab/MAP/Base_Map/RoomPrefab.GenericMapRoom.prefab";
    const string OutputFolder = "Assets/Prefab/MAP/Base_Map";
    const string StartFolder = OutputFolder + "/Start";
    const string DeadEndUpPath = OutputFolder + "/DeadEnd/RoomPrefab.Base.DeadEnd.Up.prefab";
    const string StartBasePath = StartFolder + "/RoomPrefab.Base.Start.Up.prefab";
    const string NavigationRootName = "Navigation";
    const string VisualRootName = "Visual";
    const string EntranceSpawnName = "EntranceSpawnPoint";
    const int InteractableLayer = 20;

    readonly struct RoomSpec
    {
        public readonly string Folder;
        public readonly string Name;
        public readonly RoomExitDirection[] Directions;

        public RoomSpec(string folder, string name, params RoomExitDirection[] directions)
        {
            Folder = folder;
            Name = name;
            Directions = directions;
        }

        public string PrefabPath => $"{OutputFolder}/{Folder}/{Name}.prefab";
    }

    static readonly RoomSpec[] Specs =
    {
        new("DeadEnd", "RoomPrefab.Base.DeadEnd.Up", RoomExitDirection.Up),
        new("DeadEnd", "RoomPrefab.Base.DeadEnd.Right", RoomExitDirection.Right),
        new("DeadEnd", "RoomPrefab.Base.DeadEnd.Down", RoomExitDirection.Down),
        new("DeadEnd", "RoomPrefab.Base.DeadEnd.Left", RoomExitDirection.Left),

        new("Straight", "RoomPrefab.Base.Straight.UpDown", RoomExitDirection.Up, RoomExitDirection.Down),
        new("Straight", "RoomPrefab.Base.Straight.LeftRight", RoomExitDirection.Left, RoomExitDirection.Right),

        new("Turn", "RoomPrefab.Base.Turn.UpRight", RoomExitDirection.Up, RoomExitDirection.Right),
        new("Turn", "RoomPrefab.Base.Turn.RightDown", RoomExitDirection.Right, RoomExitDirection.Down),
        new("Turn", "RoomPrefab.Base.Turn.DownLeft", RoomExitDirection.Down, RoomExitDirection.Left),
        new("Turn", "RoomPrefab.Base.Turn.LeftUp", RoomExitDirection.Left, RoomExitDirection.Up),

        new("T", "RoomPrefab.Base.T.UpRightDown", RoomExitDirection.Up, RoomExitDirection.Right, RoomExitDirection.Down),
        new("T", "RoomPrefab.Base.T.RightDownLeft", RoomExitDirection.Right, RoomExitDirection.Down, RoomExitDirection.Left),
        new("T", "RoomPrefab.Base.T.DownLeftUp", RoomExitDirection.Down, RoomExitDirection.Left, RoomExitDirection.Up),
        new("T", "RoomPrefab.Base.T.LeftUpRight", RoomExitDirection.Left, RoomExitDirection.Up, RoomExitDirection.Right),

        new("Cross", "RoomPrefab.Base.Cross.UpRightDownLeft", RoomExitDirection.Up, RoomExitDirection.Right, RoomExitDirection.Down, RoomExitDirection.Left),
    };

    [MenuItem("Tools/RB Project/Map/Generate Base Room Prefabs")]
    public static void GenerateAll()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath) == null)
            throw new InvalidOperationException($"Map base room template is missing: {TemplatePath}");

        EnsureFolder(OutputFolder);

        for (int i = 0; i < Specs.Length; i++)
            Generate(Specs[i]);

        AssetDatabase.SaveAssets();
        ValidateAll();
        Debug.Log($"[MapBaseRoomAuthoringTool] Generated and validated {Specs.Length} base room prefabs.");
    }

    /// <summary>Entry point for Unity's -executeMethod command-line option.</summary>
    public static void GenerateAllFromCommandLine()
    {
        GenerateAll();
    }

    [MenuItem("Tools/RB Project/Map/Generate Start Base Prefab")]
    public static void GenerateStartBase()
    {
        GameObject deadEndUp = AssetDatabase.LoadAssetAtPath<GameObject>(DeadEndUpPath);
        if (deadEndUp == null)
            throw new InvalidOperationException($"Start base needs its parent prefab: {DeadEndUpPath}");

        EnsureFolder(StartFolder);

        // Saving an instantiated prefab root creates a Prefab Variant. This intentionally reuses
        // the parent room's topology and serialized references instead of copying them.
        GameObject instance = PrefabUtility.InstantiatePrefab(deadEndUp) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Could not instantiate Start base parent: {DeadEndUpPath}");

        try
        {
            instance.name = "RoomPrefab.Base.Start.Up";
            PrefabUtility.SaveAsPrefabAsset(instance, StartBasePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        ValidateStartBase();
        Debug.Log($"[MapBaseRoomAuthoringTool] Generated and validated Start base prefab: {StartBasePath}");
    }

    /// <summary>Entry point for Unity's -executeMethod command-line option.</summary>
    public static void GenerateStartBaseFromCommandLine()
    {
        GenerateStartBase();
    }

    [MenuItem("Tools/RB Project/Map/Validate Base Room Prefabs")]
    public static void ValidateAll()
    {
        var errors = new List<string>();

        for (int i = 0; i < Specs.Length; i++)
            Validate(Specs[i], errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "[MapBaseRoomAuthoringTool] Base room validation failed:\n- " + string.Join("\n- ", errors));

        Debug.Log($"[MapBaseRoomAuthoringTool] Validated {Specs.Length} base room prefabs.");
    }

    [MenuItem("Tools/RB Project/Map/Validate Start Base Prefab")]
    public static void ValidateStartBase()
    {
        var errors = new List<string>();
        GameObject startBase = AssetDatabase.LoadAssetAtPath<GameObject>(StartBasePath);
        GameObject deadEndUp = AssetDatabase.LoadAssetAtPath<GameObject>(DeadEndUpPath);

        if (startBase == null)
        {
            errors.Add($"Missing Start base prefab: {StartBasePath}");
        }
        else
        {
            if (PrefabUtility.GetPrefabAssetType(startBase) != PrefabAssetType.Variant)
                errors.Add($"{StartBasePath} must be a Prefab Variant, not a copied prefab.");

            if (deadEndUp == null || PrefabUtility.GetCorrespondingObjectFromSource(startBase) != deadEndUp)
                errors.Add($"{StartBasePath} must inherit directly from {DeadEndUpPath}.");

            ValidateInheritedStartBase(startBase, errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "[MapBaseRoomAuthoringTool] Start base validation failed:\n- " + string.Join("\n- ", errors));

        Debug.Log($"[MapBaseRoomAuthoringTool] Validated Start base prefab: {StartBasePath}");
    }

    static void Generate(RoomSpec spec)
    {
        EnsureFolder($"{OutputFolder}/{spec.Folder}");

        GameObject root = PrefabUtility.LoadPrefabContents(TemplatePath);
        try
        {
            root.name = spec.Name;
            RoomController controller = root.GetComponent<RoomController>();
            if (controller == null)
                throw new InvalidOperationException($"Template '{TemplatePath}' has no RoomController.");

            var allowedDirections = new HashSet<RoomExitDirection>(spec.Directions);
            RoomExitInteractable[] sourceExits = root.GetComponentsInChildren<RoomExitInteractable>(true);
            var exits = new List<RoomExitInteractable>(spec.Directions.Length);

            for (int i = 0; i < sourceExits.Length; i++)
            {
                RoomExitInteractable exit = sourceExits[i];
                if (!allowedDirections.Contains(exit.AuthoredDirection))
                {
                    UnityEngine.Object.DestroyImmediate(exit.gameObject);
                    continue;
                }

                ConfigureExit(exit);
                exits.Add(exit);
            }

            exits.Sort((left, right) => left.AuthoredDirection.CompareTo(right.AuthoredDirection));
            ConfigureController(controller, exits);
            EnsureChild(root.transform, VisualRootName);
            RemoveChild(root.transform, NavigationRootName);

            PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureExit(RoomExitInteractable exit)
    {
        GameObject exitObject = exit.gameObject;
        exitObject.layer = InteractableLayer;

        InteractableLink link = exitObject.GetComponent<InteractableLink>();
        if (link == null)
            link = exitObject.AddComponent<InteractableLink>();

        SerializedObject serializedLink = new SerializedObject(link);
        SerializedProperty targets = serializedLink.FindProperty("targets");
        targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).objectReferenceValue = exit;
        serializedLink.ApplyModifiedPropertiesWithoutUndo();

        Transform spawn = EnsureChild(exit.transform, EntranceSpawnName);
        Vector3 inward = -DirectionVector(exit.AuthoredDirection);
        spawn.localPosition = inward * 2.5f;
        spawn.localRotation = Quaternion.LookRotation(inward, Vector3.up);
    }

    static void ConfigureController(RoomController controller, List<RoomExitInteractable> exits)
    {
        SerializedObject serializedController = new SerializedObject(controller);

        SerializedProperty serializedExits = serializedController.FindProperty("exits");
        serializedExits.arraySize = exits.Count;
        for (int i = 0; i < exits.Count; i++)
            serializedExits.GetArrayElementAtIndex(i).objectReferenceValue = exits[i];

        SerializedProperty entranceSpawns = serializedController.FindProperty("playerSpawnPointsByDirection");
        entranceSpawns.arraySize = exits.Count;
        for (int i = 0; i < exits.Count; i++)
        {
            SerializedProperty entry = entranceSpawns.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("direction").enumValueIndex = (int)exits[i].AuthoredDirection;
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue =
                exits[i].transform.Find(EntranceSpawnName);
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Validate(RoomSpec spec, List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
        if (prefab == null)
        {
            errors.Add($"Missing prefab: {spec.PrefabPath}");
            return;
        }

        RoomController controller = prefab.GetComponent<RoomController>();
        if (controller == null)
            errors.Add($"{prefab.name} has no RoomController.");

        if (prefab.transform.Find(NavigationRootName) != null)
            errors.Add($"{prefab.name} must not contain a baked {NavigationRootName} child; add it only to the finished art room.");

        var expected = new HashSet<RoomExitDirection>(spec.Directions);
        RoomExitInteractable[] exits = prefab.GetComponentsInChildren<RoomExitInteractable>(true);
        if (exits.Length != expected.Count)
            errors.Add($"{prefab.name} has {exits.Length} exits; expected {expected.Count}.");

        var actual = new HashSet<RoomExitDirection>();
        for (int i = 0; i < exits.Length; i++)
        {
            RoomExitInteractable exit = exits[i];
            if (exit == null)
                continue;

            if (!actual.Add(exit.AuthoredDirection))
                errors.Add($"{prefab.name} has duplicate {exit.AuthoredDirection} exits.");
            if (!expected.Contains(exit.AuthoredDirection))
                errors.Add($"{prefab.name} has unexpected {exit.AuthoredDirection} exit.");
            if (exit.gameObject.layer != InteractableLayer)
                errors.Add($"{prefab.name}/{exit.name} is not on the Interactable layer.");

            BoxCollider trigger = exit.GetComponent<BoxCollider>();
            if (trigger == null || !trigger.isTrigger)
                errors.Add($"{prefab.name}/{exit.name} needs a trigger BoxCollider.");

            InteractableLink link = exit.GetComponent<InteractableLink>();
            if (link == null || !LinksToExit(link, exit))
                errors.Add($"{prefab.name}/{exit.name} needs an InteractableLink bound to itself.");

            if (exit.transform.Find(EntranceSpawnName) == null)
                errors.Add($"{prefab.name}/{exit.name} has no {EntranceSpawnName}.");
        }

        foreach (RoomExitDirection direction in expected)
        {
            if (!actual.Contains(direction))
                errors.Add($"{prefab.name} is missing its {direction} exit.");
        }

    }

    static void ValidateInheritedStartBase(GameObject prefab, List<string> errors)
    {
        RoomController controller = prefab.GetComponent<RoomController>();
        if (controller == null)
            errors.Add($"{prefab.name} has no RoomController.");

        RoomExitInteractable[] exits = prefab.GetComponentsInChildren<RoomExitInteractable>(true);
        if (exits.Length != 1 || exits[0].AuthoredDirection != RoomExitDirection.Up)
            errors.Add($"{prefab.name} must inherit exactly one Up exit.");
        else
        {
            RoomExitInteractable exit = exits[0];
            if (exit.gameObject.layer != InteractableLayer)
                errors.Add($"{prefab.name}/{exit.name} is not on the Interactable layer.");

            BoxCollider trigger = exit.GetComponent<BoxCollider>();
            if (trigger == null || !trigger.isTrigger)
                errors.Add($"{prefab.name}/{exit.name} needs an inherited trigger BoxCollider.");

            InteractableLink link = exit.GetComponent<InteractableLink>();
            if (link == null || !LinksToExit(link, exit))
                errors.Add($"{prefab.name}/{exit.name} needs an inherited InteractableLink bound to itself.");

            if (exit.transform.Find(EntranceSpawnName) == null)
                errors.Add($"{prefab.name}/{exit.name} has no inherited {EntranceSpawnName}.");
        }

        if (controller != null && exits.Length == 1)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty serializedExits = serializedController.FindProperty("exits");
            SerializedProperty entranceSpawns = serializedController.FindProperty("playerSpawnPointsByDirection");
            if (serializedExits.arraySize != 1 || serializedExits.GetArrayElementAtIndex(0).objectReferenceValue != exits[0])
                errors.Add($"{prefab.name} RoomController does not reference its inherited Up exit.");
            if (entranceSpawns.arraySize != 1 ||
                entranceSpawns.GetArrayElementAtIndex(0).FindPropertyRelative("direction").enumValueIndex != (int)RoomExitDirection.Up ||
                entranceSpawns.GetArrayElementAtIndex(0).FindPropertyRelative("spawnPoint").objectReferenceValue != exits[0].transform.Find(EntranceSpawnName))
                errors.Add($"{prefab.name} RoomController does not reference its inherited Up entrance spawn.");
        }

        if (prefab.transform.Find(NavigationRootName) != null)
            errors.Add($"{prefab.name} must not inherit a baked {NavigationRootName}; add navigation after the room art is complete.");
    }

    static bool LinksToExit(InteractableLink link, RoomExitInteractable exit)
    {
        SerializedProperty targets = new SerializedObject(link).FindProperty("targets");
        return targets.arraySize == 1 && targets.GetArrayElementAtIndex(0).objectReferenceValue == exit;
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    static void RemoveChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    static Vector3 DirectionVector(RoomExitDirection direction)
    {
        return direction switch
        {
            RoomExitDirection.Right => Vector3.right,
            RoomExitDirection.Down => Vector3.back,
            RoomExitDirection.Left => Vector3.left,
            _ => Vector3.forward,
        };
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
