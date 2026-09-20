#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class SkillHitboxTimelineAdapter
{
    public int PayloadIndex, GroupIndex, ShapeIndex;
    public bool HideOthers;
    Vector2 browserScroll, inspectorScroll;
    bool colliderDetails;
    public readonly SkillHitboxSceneHandles SceneHandles = new();
    public Action Repaint;

    public SkillHitboxAuthoringSession.PayloadDraft Selected(SkillHitboxAuthoringSession session) =>
        session.payloads.Count == 0 ? null : session.payloads[Mathf.Clamp(PayloadIndex, 0, session.payloads.Count - 1)];

    public void DrawPanel(SkillHitboxAuthoringSession session, SetAnimationVfxData target) => DrawPanel(session, target, 300);

    public void DrawPanel(SkillHitboxAuthoringSession session, SetAnimationVfxData target, float height)
    {
        float width = EditorGUIUtility.currentViewWidth;
        float browserWidth = Mathf.Clamp(width * .25f, 220, 300);
        using (new EditorGUILayout.HorizontalScope(GUILayout.Height(height)))
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(browserWidth), GUILayout.Height(height)))
                DrawBrowser(session, height);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(height)))
            {
                float previousLabel = EditorGUIUtility.labelWidth;
                bool previousWide = EditorGUIUtility.wideMode;
                try
                {
                    EditorGUIUtility.labelWidth = 105;
                    EditorGUIUtility.wideMode = true;
                    DrawSelectionInspector(session, target, width - browserWidth - 25, height);
                }
                finally { EditorGUIUtility.labelWidth = previousLabel; EditorGUIUtility.wideMode = previousWide; }
            }
        }
    }

    void DrawBrowser(SkillHitboxAuthoringSession session, float height)
    {
        GUILayout.Label("HITBOXES", EditorStyles.boldLabel);
        if (session.payloads.Count == 0)
        { EditorGUILayout.HelpBox("Add a Hitbox payload in Skill Designer to start.", MessageType.Info); return; }
        PayloadIndex = Mathf.Clamp(PayloadIndex, 0, session.payloads.Count - 1);
        var names = session.payloads.Select((p, i) => $"{i + 1}. {(p.source != null ? p.source.name : "Missing payload")}").ToArray();
        int next = EditorGUILayout.Popup(PayloadIndex, names);
        if (next != PayloadIndex)
        {
            var previous = session.Windows(Selected(session)).Find(w => w.Start.id == selectedWindow);
            EndTimelineDrag(); PayloadIndex = next; GroupIndex = ShapeIndex = 0;
            var shared = session.Windows(Selected(session)).Find(w =>
                w.Start.id == previous.Start?.id || w.End.id == previous.End?.id);
            SelectWindow(session, shared.Start?.id);
        }
        var draft = Selected(session);
        if (draft.source == null) { EditorGUILayout.HelpBox("Payload missing. Revert to reload.", MessageType.Error); return; }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Group")) AddGroup(session, draft);
            using (new EditorGUI.DisabledScope(GroupIndex < 0 || GroupIndex >= draft.hitboxLayout.Groups.Count))
                if (GUILayout.Button("+ Shape")) AddShape(session, draft);
        }
        browserScroll = EditorGUILayout.BeginScrollView(browserScroll, GUILayout.Height(Mathf.Max(40, height - 75)));
        try
        {
            var groups = draft.hitboxLayout.Groups;
            GroupIndex = groups.Count == 0 ? -1 : Mathf.Clamp(GroupIndex, 0, groups.Count - 1);
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g]; if (group == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool chosen = GUILayout.Toggle(GroupIndex == g, $"{group.GroupKey}   ({group.Shapes.Count})", EditorStyles.miniButton);
                    if (chosen && GroupIndex != g) { GroupIndex = g; ShapeIndex = 0; Repaint?.Invoke(); SceneView.RepaintAll(); }
                    if (GUILayout.Button("...", GUILayout.Width(25))) ShowGroupMenu(session, draft, g);
                }
                if (GroupIndex != g) continue;
                for (int s = 0; s < group.Shapes.Count; s++)
                {
                    var shape = group.Shapes[s]; if (shape == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(14);
                        if (GUILayout.Toggle(ShapeIndex == s, new GUIContent($"{s + 1}. {shape.ShapeName}", shape.Type.ToString()), EditorStyles.miniButton))
                        { ShapeIndex = s; }
                    }
                }
            }
            if (groups.Count == 0) EditorGUILayout.HelpBox("Create a group, then attach its shapes to a bone.", MessageType.Info);
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    void AddGroup(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft)
    {
        session.Record("Add Hitbox Group");
        var groups = draft.hitboxLayout.Groups.ToList();
        var group = new SkillHitboxLayoutData.HitBoxGroupData { GroupKey = UniqueKey(draft) };
        group.Shapes.Add(new SkillHitboxLayoutData.HitBoxShapeData()); groups.Add(group);
        draft.hitboxLayout.ReplaceGroups(groups); GroupIndex = groups.Count - 1; ShapeIndex = 0;
        GUIUtility.ExitGUI();
    }

    void AddShape(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft)
    {
        var group = draft.hitboxLayout.Groups[GroupIndex]; if (group == null) return;
        session.Record("Add Hitbox Shape"); group.Shapes.Add(new SkillHitboxLayoutData.HitBoxShapeData());
        ShapeIndex = group.Shapes.Count - 1; GUIUtility.ExitGUI();
    }

    void ShowGroupMenu(SkillHitboxAuthoringSession session, SkillHitboxAuthoringSession.PayloadDraft draft, int index)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Duplicate Group"), false, () =>
        {
            if (session == null || !session.payloads.Contains(draft) || index >= draft.hitboxLayout.Groups.Count) return;
            session.Record("Duplicate Hitbox Group");
            var copy = JsonUtility.FromJson<SkillHitboxLayoutData.HitBoxGroupData>(JsonUtility.ToJson(draft.hitboxLayout.Groups[index]));
            copy.GroupKey = UniqueKey(draft); var list = draft.hitboxLayout.Groups.ToList(); list.Add(copy);
            draft.hitboxLayout.ReplaceGroups(list); GroupIndex = list.Count - 1; ShapeIndex = 0; Repaint?.Invoke();
        });
        menu.AddItem(new GUIContent("Delete Group"), false, () =>
        {
            if (session == null || !session.payloads.Contains(draft) || index >= draft.hitboxLayout.Groups.Count) return;
            session.Record("Remove Hitbox Group");
            string key = draft.hitboxLayout.Groups[index]?.GroupKey;
            var so = new SerializedObject(session);
            var steps = so.FindProperty("payloads").GetArrayElementAtIndex(session.payloads.IndexOf(draft)).FindPropertyRelative("steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                var keys = steps.GetArrayElementAtIndex(i).FindPropertyRelative("groupKeys");
                for (int j = keys.arraySize - 1; j >= 0; j--)
                    if (string.Equals(keys.GetArrayElementAtIndex(j).stringValue, key, StringComparison.OrdinalIgnoreCase)) keys.DeleteArrayElementAtIndex(j);
            }
            int payload = session.payloads.IndexOf(draft);
            so.ApplyModifiedPropertiesWithoutUndo();
            var current = session.payloads[payload]; var list = current.hitboxLayout.Groups.ToList(); list.RemoveAt(index);
            current.hitboxLayout.ReplaceGroups(list); GroupIndex = Mathf.Max(0, index - 1); ShapeIndex = 0; Repaint?.Invoke();
        });
        menu.ShowAsContext();
    }

    void DrawSelectionInspector(SkillHitboxAuthoringSession session, SetAnimationVfxData target, float width, float height)
    {
        var draft = Selected(session);
        if (draft == null || draft.source == null || GroupIndex < 0 || GroupIndex >= draft.hitboxLayout.Groups.Count)
        { GUILayout.Label("Select a group or shape", EditorStyles.boldLabel); return; }
        var group = draft.hitboxLayout.Groups[GroupIndex]; if (group == null) return;
        ShapeIndex = group.Shapes.Count == 0 ? -1 : Mathf.Clamp(ShapeIndex, 0, group.Shapes.Count - 1);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            SceneHandles.Mode = (SkillHitboxSceneHandles.EditMode)GUILayout.Toolbar((int)SceneHandles.Mode,
                new[] { "Move", "Rotate", "Size", "Scale" }, EditorStyles.toolbarButton, GUILayout.Width(240));
            GUILayout.FlexibleSpace();
            HideOthers = GUILayout.Toggle(HideOthers, "Solo", EditorStyles.toolbarButton, GUILayout.Width(40));
            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(50))) FrameSelected(session, target);
        }
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(Mathf.Max(40, height - 36)));
        try
        {
            DrawHitDetails(session);
            bool wide = width >= 820;
            using (var columns = wide ? new EditorGUILayout.HorizontalScope() : null)
            {
            using (new EditorGUILayout.VerticalScope(wide ? new[] { GUILayout.Width(330) } : Array.Empty<GUILayoutOption>()))
            {
                GUILayout.Label("ATTACHMENT", EditorStyles.boldLabel);
                string key = EditorGUILayout.DelayedTextField("Group", group.GroupKey);
                if (key != group.GroupKey) { session.RenameGroup(draft, GroupIndex, key); GUIUtility.ExitGUI(); }
                var anchor = (SkillHitboxLayoutData.AnchorSpace)EditorGUILayout.EnumPopup("Follow", group.Anchor);
                if (anchor != group.Anchor) ChangeAnchor(session, target, draft, group, anchor, "");
                if (group.Anchor != SkillHitboxLayoutData.AnchorSpace.Payload)
                {
                    var root = SkillHitboxSceneHandles.AnchorRoot(target, group.Anchor);
                    if (root != null)
                    {
                        var boneRect = EditorGUILayout.GetControlRect();
                        HandleBoneDrop(boneRect, session, target, draft, group);
                        var field = EditorGUI.PrefixLabel(boneRect, new GUIContent("Bone"));
                        string label = string.IsNullOrEmpty(group.AnchorPath) ? "(root)" : group.AnchorPath;
                        if (GUI.Button(field, new GUIContent(label + "  ...", "Open Bone Picker, or drop a bone from Hierarchy here."), EditorStyles.miniButton))
                            SkillHitboxBonePickerWindow.Open(session, target, draft, group, () => { Repaint?.Invoke(); SceneView.RepaintAll(); });
                        GUILayout.Label("Click to pick a bone, or drop from Hierarchy.", EditorStyles.miniLabel);
                    }
                    else EditorGUILayout.HelpBox("Anchor root unavailable on this model.", MessageType.Warning);
                }
                GUILayout.Space(6);
                GUILayout.Label("Edit the selected shape in Scene View. Green = active, yellow = selected.", EditorStyles.wordWrappedMiniLabel);
            }
            if (wide) GUILayout.Space(16);
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("SHAPE", EditorStyles.boldLabel);
                if (ShapeIndex < 0 || group.Shapes[ShapeIndex] == null)
                    EditorGUILayout.HelpBox("Add a shape to this group.", MessageType.Info);
                else
                {
                    var so = new SerializedObject(session);
                    var shape = so.FindProperty("payloads").GetArrayElementAtIndex(PayloadIndex).FindPropertyRelative("hitboxLayout").FindPropertyRelative("groups")
                        .GetArrayElementAtIndex(GroupIndex).FindPropertyRelative("shapes").GetArrayElementAtIndex(ShapeIndex);
                    EditorGUILayout.PropertyField(shape.FindPropertyRelative("shapeName"), new GUIContent("Name"));
                    EditorGUILayout.PropertyField(shape.FindPropertyRelative("type"));
                    EditorGUILayout.PropertyField(shape.FindPropertyRelative("localPosition"), new GUIContent("Position"));
                    EditorGUILayout.PropertyField(shape.FindPropertyRelative("localEulerAngles"), new GUIContent("Rotation"));
                    var type = (SkillHitboxLayoutData.HitBoxType)shape.FindPropertyRelative("type").enumValueIndex;
                    EditorGUILayout.PropertyField(shape.FindPropertyRelative(type == SkillHitboxLayoutData.HitBoxType.Box ? "size" : "radius"));
                    if (type == SkillHitboxLayoutData.HitBoxType.Capsule)
                    {
                        EditorGUILayout.PropertyField(shape.FindPropertyRelative("height"));
                        var direction = shape.FindPropertyRelative("direction");
                        EditorGUI.BeginChangeCheck();
                        int axis = EditorGUILayout.Popup("Axis", direction.intValue, new[] { "X", "Y", "Z" });
                        if (EditorGUI.EndChangeCheck()) direction.intValue = axis;
                    }
                    colliderDetails = EditorGUILayout.Foldout(colliderDetails, "Scale & collider center", true);
                    if (colliderDetails)
                    { EditorGUILayout.PropertyField(shape.FindPropertyRelative("localScale")); EditorGUILayout.PropertyField(shape.FindPropertyRelative("center")); }
                    so.ApplyModifiedProperties();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Duplicate Shape"))
                        {
                            session.Record("Duplicate Hitbox Shape"); group.Shapes.Add(JsonUtility.FromJson<SkillHitboxLayoutData.HitBoxShapeData>(JsonUtility.ToJson(group.Shapes[ShapeIndex])));
                            ShapeIndex = group.Shapes.Count - 1; GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("Delete Shape"))
                        { session.Record("Remove Hitbox Shape"); group.Shapes.RemoveAt(ShapeIndex); ShapeIndex = Mathf.Max(0, ShapeIndex - 1); GUIUtility.ExitGUI(); }
                    }
                }
            }
            }
            DrawRepairMarkers(session);
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    internal static bool TryBoneDropPath(Transform root, UnityEngine.Object[] references, out string path)
    {
        path = null;
        if (root == null || references == null || references.Length != 1) return false;
        var bone = references[0] is GameObject go ? go.transform : references[0] as Transform;
        if (bone == null || EditorUtility.IsPersistent(bone) || (bone != root && !bone.IsChildOf(root))) return false;
        string candidate = AnimationUtility.CalculateTransformPath(bone, root);
        // Serialized runtime paths must resolve to the exact dropped bone, even with duplicate names.
        if ((string.IsNullOrEmpty(candidate) ? root : root.Find(candidate)) != bone) return false;
        path = candidate; return true;
    }

    internal static bool AssignDroppedBone(SkillHitboxAuthoringSession session, SetAnimationVfxData target,
        SkillHitboxAuthoringSession.PayloadDraft draft, SkillHitboxLayoutData.HitBoxGroupData group, UnityEngine.Object[] references)
    {
        if (session == null || !session.payloads.Contains(draft) || !draft.hitboxLayout.Groups.Contains(group) ||
            group.Anchor == SkillHitboxLayoutData.AnchorSpace.Payload ||
            !TryBoneDropPath(SkillHitboxSceneHandles.AnchorRoot(target, group.Anchor), references, out string path)) return false;
        if (path != group.AnchorPath)
        {
            Undo.IncrementCurrentGroup();
            ChangeAnchor(session, target, draft, group, group.Anchor, path);
        }
        return true;
    }

    void HandleBoneDrop(Rect rect, SkillHitboxAuthoringSession session, SetAnimationVfxData target,
        SkillHitboxAuthoringSession.PayloadDraft draft, SkillHitboxLayoutData.HitBoxGroupData group)
    {
        var current = Event.current;
        if (!GUI.enabled || !rect.Contains(current.mousePosition) ||
            (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
        bool valid = TryBoneDropPath(SkillHitboxSceneHandles.AnchorRoot(target, group.Anchor), DragAndDrop.objectReferences, out _);
        DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
        if (valid && current.type == EventType.DragPerform && AssignDroppedBone(session, target, draft, group, DragAndDrop.objectReferences))
        {
            DragAndDrop.AcceptDrag(); GUI.changed = true;
            Repaint?.Invoke(); SceneView.RepaintAll();
        }
        current.Use();
    }

    public static void ChangeAnchor(SkillHitboxAuthoringSession session, SetAnimationVfxData target, SkillHitboxAuthoringSession.PayloadDraft draft,
        SkillHitboxLayoutData.HitBoxGroupData group, SkillHitboxLayoutData.AnchorSpace space, string path)
    {
        bool hadBasis = SkillHitboxSceneHandles.TryBasis(target, draft, group, out var oldBasis);
        session.Record("Change Hitbox Anchor"); group.Anchor = space; group.AnchorPath = path;
        if (!hadBasis || !SkillHitboxSceneHandles.TryBasis(target, draft, group, out var newBasis)) return;
        foreach (var shape in group.Shapes)
        {
            if (shape == null) continue;
            var local = newBasis.inverse * oldBasis * Matrix4x4.TRS(shape.LocalPosition, Quaternion.Euler(shape.LocalEulerAngles), shape.LocalScale);
            shape.LocalPosition = local.GetColumn(3); shape.LocalEulerAngles = local.rotation.eulerAngles; shape.LocalScale = local.lossyScale;
        }
    }

    static string UniqueKey(SkillHitboxAuthoringSession.PayloadDraft draft)
    {
        int i = 1;
        while (draft.hitboxLayout.Groups.Any(g => g != null && string.Equals(g.GroupKey, $"Group{i:00}", StringComparison.OrdinalIgnoreCase))) i++;
        return $"Group{i:00}";
    }

    public void DrawScene(SkillHitboxAuthoringSession session, SetAnimationVfxData target, float time)
    {
        for (int p = 0; p < session.payloads.Count; p++)
        {
            var draft = session.payloads[p];
            if (draft.source == null) continue;
            for (int g = 0; g < draft.hitboxLayout.Groups.Count; g++)
            {
                if (HideOthers && (p != PayloadIndex || g != GroupIndex)) continue;
                var group = draft.hitboxLayout.Groups[g];
                if (group == null || !SkillHitboxSceneHandles.TryBasis(target, draft, group, out var basis)) continue;
                bool active = session.IsGroupActive(draft, group.GroupKey, time);
                var hit = p == PayloadIndex ? StepFor(draft, selectedWindow) : null;
                bool inSelectedHit = hit != null && hit.GroupKeys.Any(k => string.Equals(k, group.GroupKey, StringComparison.OrdinalIgnoreCase));
                for (int s = 0; s < group.Shapes.Count; s++)
                {
                    if (group.Shapes[s] == null) continue;
                    bool selected = p == PayloadIndex && g == GroupIndex && s == ShapeIndex;
                    if (SceneHandles.Draw(session, basis, group.Shapes[s], p == PayloadIndex && g == GroupIndex ? Color.yellow : active ? Color.green : inSelectedHit ? Color.cyan : Color.gray, selected))
                    { PayloadIndex = p; GroupIndex = g; ShapeIndex = s; Repaint?.Invoke(); }
                }
            }
        }
        if (GUI.changed) { Repaint?.Invoke(); SceneView.RepaintAll(); }
    }

    void FrameSelected(SkillHitboxAuthoringSession session, SetAnimationVfxData target)
    {
        var draft = Selected(session);
        if (draft == null || GroupIndex < 0 || GroupIndex >= draft.hitboxLayout.Groups.Count) return;
        var group = draft.hitboxLayout.Groups[GroupIndex];
        if (group == null || ShapeIndex < 0 || ShapeIndex >= group.Shapes.Count || group.Shapes[ShapeIndex] == null || !SkillHitboxSceneHandles.TryBasis(target, draft, group, out var basis)) return;
        SceneView.lastActiveSceneView?.Frame(SkillHitboxSceneHandles.BoundsFor(basis, group.Shapes[ShapeIndex]), false);
    }

}
#endif
