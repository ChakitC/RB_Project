#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// A schematic picker bound to real transforms. No preview models or animation player.
public sealed class SkillHitboxBonePickerWindow : EditorWindow
{
    SkillHitboxAuthoringSession session;
    SetAnimationVfxData target;
    SkillHitboxAuthoringSession.PayloadDraft draft;
    SkillHitboxLayoutData.HitBoxGroupData group;
    SkillHitboxLayoutData.AnchorSpace anchor;
    Transform root, selected;
    Transform[] bones = Array.Empty<Transform>();
    Action applied;
    string search = "";
    Vector2 listScroll, pan;
    float yaw, pitch, zoom = 1f;
    bool allTransforms, names, modelPose;
    int dragControl;

    public static SkillHitboxBonePickerWindow Open(SkillHitboxAuthoringSession session, SetAnimationVfxData target,
        SkillHitboxAuthoringSession.PayloadDraft draft, SkillHitboxLayoutData.HitBoxGroupData group, Action applied)
    {
        foreach (var existing in Resources.FindObjectsOfTypeAll<SkillHitboxBonePickerWindow>()) existing.Close();
        var window = CreateInstance<SkillHitboxBonePickerWindow>();
        window.session = session; window.target = target; window.draft = draft; window.group = group;
        window.anchor = group.Anchor; window.root = SkillHitboxSceneHandles.AnchorRoot(target, group.Anchor);
        window.selected = window.root != null ? (string.IsNullOrEmpty(group.AnchorPath) ? window.root : window.root.Find(group.AnchorPath)) : null;
        window.applied = applied; window.titleContent = new GUIContent("Bone Picker");
        window.minSize = new Vector2(740, 520);
        window.Rebuild(); window.ShowUtility(); window.position = new Rect(150, 100, 980, 720); return window;
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        AssemblyReloadEvents.beforeAssemblyReload += Close;
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= Close;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
        applied = null;
    }

    void PlayModeChanged(PlayModeStateChange state) { if (state != PlayModeStateChange.EnteredEditMode) Close(); }
    void OnInspectorUpdate() { if (!ValidBinding()) Close(); else Repaint(); }
    bool ValidBinding() => !EditorApplication.isPlayingOrWillChangePlaymode && session != null && target != null && root != null &&
        session.payloads.Contains(draft) && draft.hitboxLayout.Groups.Contains(group) && group.Anchor == anchor &&
        SkillHitboxSceneHandles.AnchorRoot(target, anchor) == root;

    internal static Transform[] CollectBones(Transform root, bool allTransforms, Transform current)
    {
        if (root == null) return Array.Empty<Transform>();
        var hierarchy = root.GetComponentsInChildren<Transform>(true).Where(b => !SkillHitboxBonePickerLayout.IsFinger(b, root)).ToArray();
        if (allTransforms) return hierarchy;
        var included = new HashSet<Transform>();
        void Include(Transform bone)
        {
            if (bone == null || (bone != root && !bone.IsChildOf(root))) return;
            while (bone != null) { included.Add(bone); if (bone == root) break; bone = bone.parent; }
        }
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var bone in renderer.bones) Include(bone);
        if (included.Count == 0) return hierarchy;
        Include(current); Include(root);
        foreach (var bone in hierarchy.Where(b => SkillHitboxBonePickerLayout.IsWeaponAttachment(b, root))) included.Add(bone);
        return hierarchy.Where(included.Contains).ToArray();
    }

    void Rebuild() { bones = CollectBones(root, allTransforms, selected); }

    internal static Vector3 ProjectBone(Transform root, Transform bone, float yaw, float pitch) =>
        Quaternion.Euler(pitch, yaw, 0) * root.InverseTransformPoint(bone.position);

    internal bool ApplySelection()
    {
        if (!ValidBinding() || !SkillHitboxTimelineAdapter.AssignDroppedBone(session, target, draft, group, new UnityEngine.Object[] { selected })) return false;
        applied?.Invoke(); return true;
    }

    void OnGUI()
    {
        if (!ValidBinding()) { EditorGUILayout.HelpBox("The source or model changed. Reopen Bone Picker from the Hitbox tool.", MessageType.Info); return; }
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Event.current.Use(); Close(); return; }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(root.name + " · " + anchor, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            bool showAll = GUILayout.Toggle(allTransforms, "All transforms", EditorStyles.toolbarButton);
            if (showAll != allTransforms) { allTransforms = showAll; Rebuild(); }
            names = GUILayout.Toggle(names, "Names", EditorStyles.toolbarButton);
            modelPose = GUILayout.Toggle(modelPose, "Model pose", EditorStyles.toolbarButton);
            if (GUILayout.Button("Front", EditorStyles.toolbarButton)) { yaw = pitch = 0; pan = Vector2.zero; }
            if (GUILayout.Button("Side", EditorStyles.toolbarButton)) { yaw = 90; pitch = 0; pan = Vector2.zero; }
            if (GUILayout.Button("Top", EditorStyles.toolbarButton)) { yaw = 0; pitch = 90; pan = Vector2.zero; }
            if (GUILayout.Button("Frame", EditorStyles.toolbarButton)) { zoom = 1; pan = Vector2.zero; }
        }
        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250)))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                GUILayout.Label($"{bones.Length} transforms · click or double-click to choose", EditorStyles.wordWrappedMiniLabel);
                using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll))
                {
                listScroll = scroll.scrollPosition;
                foreach (var bone in bones)
                {
                    if (bone == null) continue;
                    string path = AnimationUtility.CalculateTransformPath(bone, root);
                    if (!Matches(bone, path)) continue;
                    bool valid = SkillHitboxTimelineAdapter.TryBoneDropPath(root, new UnityEngine.Object[] { bone }, out _);
                    using (new EditorGUI.DisabledScope(!valid))
                    {
                        if (GUILayout.Button(new GUIContent(bone == root ? "(root) " + bone.name : bone.name,
                            valid ? path : "This hierarchy path is ambiguous."), selected == bone ? EditorStyles.miniButton : EditorStyles.label))
                        { selected = bone; Repaint(); if (Event.current.clickCount == 2 && ApplySelection()) { Close(); GUIUtility.ExitGUI(); } }
                    }
                }
                }
                GUILayout.Label("Enable All transforms for sockets and unweighted bones.", EditorStyles.wordWrappedMiniLabel);
            }
            Rect canvas = GUILayoutUtility.GetRect(200, 10000, 200, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawSkeleton(canvas);
        }
        GUILayout.Label("Diagram selects real bones · Fingers hidden · Right-drag: orbit · Middle-drag: pan · Scroll: zoom", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string path = selected != null ? AnimationUtility.CalculateTransformPath(selected, root) : null;
            GUILayout.Label(new GUIContent(selected != null ? (string.IsNullOrEmpty(path) ? "(root)" : path) : "Choose a bone", path), EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(75))) { Close(); GUIUtility.ExitGUI(); }
            using (new EditorGUI.DisabledScope(!SkillHitboxTimelineAdapter.TryBoneDropPath(root, new UnityEngine.Object[] { selected }, out _)))
                if (GUILayout.Button("Select Bone", GUILayout.Width(110)) && ApplySelection()) { Close(); GUIUtility.ExitGUI(); }
        }
    }

    bool Matches(Transform bone, string path) => string.IsNullOrWhiteSpace(search) ||
        bone.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    static Color BoneColor(Transform bone)
    {
        string name = bone.name.ToLowerInvariant();
        if (name.Contains("left") || name.EndsWith(".l") || name.EndsWith("_l")) return new Color(1f, .55f, .13f);
        if (name.Contains("right") || name.EndsWith(".r") || name.EndsWith("_r")) return new Color(.55f, .6f, 1f);
        return new Color(.15f, .8f, .8f);
    }

    void DrawSkeleton(Rect canvas)
    {
        var current = Event.current;
        int control = GUIUtility.GetControlID("BonePickerView".GetHashCode(), FocusType.Passive);
        var positions = SkillHitboxBonePickerLayout.BodyPositions(root, bones, modelPose, yaw, pitch);
        if (positions.Count == 0) return;
        float minX = positions.Values.Min(p => p.x), maxX = positions.Values.Max(p => p.x);
        float minY = positions.Values.Min(p => p.y), maxY = positions.Values.Max(p => p.y);
        float scale = Mathf.Min(Mathf.Max(1, canvas.width - 210) / Mathf.Max(.1f, maxX - minX),
            Mathf.Max(1, canvas.height - 75) / Mathf.Max(.1f, maxY - minY)) * zoom;
        Vector2 center = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
        var points = positions.ToDictionary(p => p.Key, p => SkillHitboxBonePickerLayout.ToCanvas(p.Value, center, scale, canvas.size, pan));
        var weapons = SkillHitboxBonePickerLayout.WeaponPositions(root, bones, modelPose, yaw, pitch)
            .ToDictionary(p => p.Key, p => SkillHitboxBonePickerLayout.ToCanvas(p.Value, center, scale, canvas.size, pan));
        points = SkillHitboxBonePickerLayout.SeparateTwistLanes(points, out var starts);
        Vector2 mouse = current.mousePosition - canvas.position;
        var candidates = canvas.Contains(current.mousePosition) ? SkillHitboxBonePickerLayout.HitCandidates(points, mouse, starts) : Array.Empty<Transform>();
        Transform hovered = candidates.FirstOrDefault();
        if (current.type == EventType.Repaint)
        {
            GUI.BeginGroup(canvas);
            EditorGUI.DrawRect(new Rect(Vector2.zero, canvas.size), new Color(.12f, .13f, .15f));
            for (float x = 0; x < canvas.width; x += 40) EditorGUI.DrawRect(new Rect(x, 0, 1, canvas.height), new Color(.18f, .19f, .21f));
            for (float y = 0; y < canvas.height; y += 40) EditorGUI.DrawRect(new Rect(0, y, canvas.width, 1), new Color(.18f, .19f, .21f));
            Handles.BeginGUI(); Color oldColor = Handles.color;
            // Draw the selection last so an overlapping branch cannot cover it.
            foreach (var pair in points.OrderBy(p => p.Key == selected ? 2 : p.Key == hovered ? 1 : 0))
            {
                bool match = Matches(pair.Key, AnimationUtility.CalculateTransformPath(pair.Key, root));
                var color = pair.Key == selected ? Color.yellow : pair.Key == hovered ? Color.white : BoneColor(pair.Key);
                if (!match) color = new Color(.3f, .32f, .35f);
                Handles.color = color;
                if (starts.TryGetValue(pair.Key, out var parent))
                {
                    Vector2 direction = pair.Value - parent;
                    if (direction.sqrMagnitude > 4)
                    {
                        Vector2 side = new Vector2(-direction.y, direction.x).normalized * Mathf.Min(7, direction.magnitude * .12f);
                        Vector2 shoulder = parent + direction * .22f;
                        Handles.DrawAAConvexPolygon(parent, shoulder + side, pair.Value, shoulder - side);
                    }
                }
                Handles.DrawSolidDisc(pair.Value, Vector3.forward, pair.Key == selected ? 5 : 3);
            }
            Handles.color = oldColor; Handles.EndGUI();
            var labels = new List<Rect>();
            foreach (var pair in points.OrderByDescending(p => p.Key == selected))
                if (pair.Key == selected || pair.Key == hovered || (names && Matches(pair.Key, AnimationUtility.CalculateTransformPath(pair.Key, root))))
                {
                    float width = Mathf.Min(canvas.width - 12, EditorStyles.whiteMiniLabel.CalcSize(new GUIContent(pair.Key.name)).x + 8);
                    var label = new Rect(Mathf.Clamp(pair.Value.x + 7, 6, canvas.width - width - 6), Mathf.Clamp(pair.Value.y - 10, 4, canvas.height - 24), width, 20);
                    while (labels.Any(r => r.Overlaps(label)) && label.yMax + 20 < canvas.height) label.y += 20;
                    if (!labels.Any(r => r.Overlaps(label))) { GUI.Label(label, pair.Key.name, EditorStyles.whiteMiniLabel); labels.Add(label); }
                }
            if (candidates.Length > 1)
                GUI.Label(new Rect(8, 6, canvas.width - 16, 22), $"{candidates.Length} overlapping bones — click to choose by name", EditorStyles.whiteMiniLabel);
            GUI.EndGroup();
        }
        DrawWeapons(canvas, weapons);
        if (current.type == EventType.MouseDown && canvas.Contains(current.mousePosition))
        {
            GUI.FocusControl(null);
            if (current.button == 0 && hovered != null)
            {
                if (candidates.Length > 1) ShowOverlappingBones(candidates);
                else
                {
                    selected = hovered;
                    if (current.clickCount == 2 && ApplySelection()) { current.Use(); Close(); GUIUtility.ExitGUI(); }
                }
            }
            else if (current.button == 1 || current.button == 2) { dragControl = control; GUIUtility.hotControl = control; }
            current.Use(); Repaint();
        }
        if (current.type == EventType.ScrollWheel && canvas.Contains(current.mousePosition))
        { zoom = Mathf.Clamp(zoom * Mathf.Exp(-current.delta.y * .08f), .2f, 12f); current.Use(); Repaint(); }
        if (dragControl != 0 && GUIUtility.hotControl == dragControl)
        {
            if (current.type == EventType.MouseDrag)
            {
                if (current.button == 2) pan += current.delta;
                else { yaw += current.delta.x * .5f; pitch = Mathf.Clamp(pitch - current.delta.y * .5f, -90, 90); }
                current.Use(); Repaint();
            }
            if (current.type == EventType.MouseUp) { GUIUtility.hotControl = 0; dragControl = 0; current.Use(); }
        }
        if (current.type == EventType.MouseMove) Repaint();
    }

    void ShowOverlappingBones(Transform[] candidates)
    {
        var menu = new GenericMenu();
        foreach (var bone in candidates)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            string label = bone.name + " — " + (string.IsNullOrEmpty(path) ? "(root)" : path.Replace("/", " › "));
            if (!SkillHitboxTimelineAdapter.TryBoneDropPath(root, new UnityEngine.Object[] { bone }, out _))
                menu.AddDisabledItem(new GUIContent(label + " (ambiguous path)"));
            else menu.AddItem(new GUIContent(label), selected == bone, () =>
            {
                if (!ValidBinding() || !SkillHitboxTimelineAdapter.TryBoneDropPath(root, new UnityEngine.Object[] { bone }, out _)) return;
                selected = bone; Repaint();
            });
        }
        menu.ShowAsContext();
    }

    void DrawWeapons(Rect canvas, IReadOnlyDictionary<Transform, Vector2> weapons)
    {
        GUI.BeginGroup(canvas);
        try
        {
        foreach (var pair in weapons)
        {
            var bone = pair.Key;
            var rect = new Rect(pair.Value.x - 62, pair.Value.y - 13, 124, 26);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = selected == bone ? Color.yellow : BoneColor(bone);
            bool clicked = GUI.Button(rect, new GUIContent("◆ " + bone.name, "Weapon attachment · " + AnimationUtility.CalculateTransformPath(bone, root)), EditorStyles.miniButton);
            GUI.backgroundColor = previous;
            if (clicked)
            {
                var overlapping = weapons.Where(p => new Rect(p.Value.x - 62, p.Value.y - 13, 124, 26).Contains(Event.current.mousePosition))
                    .Select(p => p.Key).ToArray();
                if (overlapping.Length > 1) { ShowOverlappingBones(overlapping); continue; }
                selected = bone; Repaint();
                if (Event.current.clickCount == 2 && ApplySelection()) { Close(); GUIUtility.ExitGUI(); }
            }
        }
        }
        finally { GUI.EndGroup(); }
    }
}
#endif
