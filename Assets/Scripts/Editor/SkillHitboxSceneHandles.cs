#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Draws geometry only: no preview GameObjects, Colliders, physics callbacks or material instances.
public sealed class SkillHitboxSceneHandles
{
    readonly BoxBoundsHandle box = new();
    readonly SphereBoundsHandle sphere = new();
    readonly CapsuleBoundsHandle capsule = new();
    public enum EditMode { Move, Rotate, Size, Scale }
    public EditMode Mode;

    static CharacteContext ResolveContext(SetAnimationVfxData target) =>
        CharacterContextModuleLookup.ResolveContext(target.CharacterRoot) ??
        SkillHitboxCharacterSetup.ResolveCharacter(target.gameObject);

    public static Transform AnchorRoot(SetAnimationVfxData target, SkillHitboxLayoutData.AnchorSpace space)
    {
        if (target == null) return null;
        var context = ResolveContext(target);
        context?.ResolveReferences();
        if (context != null && SkillHitboxGroup.TryResolveAnchor(space, "", context.transform, context, out var root)) return root;
        if (space == SkillHitboxLayoutData.AnchorSpace.AnimatorRoot) return target.PreviewAnimator != null ? target.PreviewAnimator.transform : null;
        return target.CharacterRoot;
    }

    public static bool TryBasis(SetAnimationVfxData target, SkillHitboxAuthoringSession.PayloadDraft payload,
        SkillHitboxLayoutData.HitBoxGroupData group, out Matrix4x4 basis)
    {
        basis = Matrix4x4.identity;
        var root = AnchorRoot(target, group.Anchor);
        if (root == null) return false;
        if (group.Anchor != SkillHitboxLayoutData.AnchorSpace.Payload)
        {
            var anchor = string.IsNullOrWhiteSpace(group.AnchorPath) ? root : root.Find(group.AnchorPath);
            if (anchor == null) return false;
            basis = anchor.localToWorldMatrix; return true;
        }
        if (!string.IsNullOrWhiteSpace(group.AnchorPath)) return false;
        var context = ResolveContext(target);
        Transform origin = context != null && context.EnegySystem != null ? context.EnegySystem.CastOrigin : root;
        var data = new SerializedObject(payload.source);
        var mode = (PrefabHitboxSkillPayloadDef.HitboxAnchorMode)data.FindProperty("anchorMode").enumValueIndex;
        Transform chosen = mode == PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CastOrigin ? origin : root;
        if (mode == PrefabHitboxSkillPayloadDef.HitboxAnchorMode.CasterChildPath)
            chosen = root.Find(data.FindProperty("anchorChildPath").stringValue) ?? origin;
        basis = Matrix4x4.TRS(chosen.position + chosen.rotation * payload.source.LocalPositionOffset,
            chosen.rotation * Quaternion.Euler(payload.source.LocalEulerOffset), root.lossyScale);
        return true;
    }

    static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    static Vector3 Divide(Vector3 a, Vector3 b) => new(a.x / Mathf.Max(.00001f, b.x), a.y / Mathf.Max(.00001f, b.y), a.z / Mathf.Max(.00001f, b.z));
    static Matrix4x4 ShapeMatrix(Matrix4x4 basis, SkillHitboxLayoutData.HitBoxShapeData shape) =>
        basis * Matrix4x4.TRS(shape.LocalPosition, Quaternion.Euler(shape.LocalEulerAngles), shape.LocalScale);

    public static Bounds BoundsFor(Matrix4x4 basis, SkillHitboxLayoutData.HitBoxShapeData shape)
    {
        var matrix = ShapeMatrix(basis, shape);
        Vector3 scale = Abs(matrix.lossyScale), size = Vector3.one * shape.Radius * 2 * Mathf.Max(scale.x, scale.y, scale.z);
        if (shape.Type == SkillHitboxLayoutData.HitBoxType.Box) size = Vector3.Scale(shape.Size, scale);
        else if (shape.Type == SkillHitboxLayoutData.HitBoxType.Capsule)
        {
            int d = Mathf.Clamp(shape.Direction, 0, 2);
            float radius = shape.Radius * Mathf.Max(scale[(d + 1) % 3], scale[(d + 2) % 3]);
            size = Vector3.one * radius * 2; size[d] = Mathf.Max(size[d], shape.Height * scale[d]);
        }
        var rotation = Matrix4x4.Rotate(matrix.rotation);
        var extents = Abs(rotation.MultiplyVector(new Vector3(size.x, 0, 0))) +
            Abs(rotation.MultiplyVector(new Vector3(0, size.y, 0))) + Abs(rotation.MultiplyVector(new Vector3(0, 0, size.z)));
        return new Bounds(matrix.MultiplyPoint3x4(shape.Center), extents);
    }

    public bool Draw(SkillHitboxAuthoringSession session, Matrix4x4 basis,
        SkillHitboxLayoutData.HitBoxShapeData shape, Color color, bool selected)
    {
        var matrix = ShapeMatrix(basis, shape);
        Vector3 center = matrix.MultiplyPoint3x4(shape.Center), scale = Abs(matrix.lossyScale);
        var physical = Matrix4x4.TRS(center, matrix.rotation, Vector3.one);
        var oldColor = Handles.color;
        Handles.color = color;
        using (new Handles.DrawingScope(physical))
        {
            if (shape.Type == SkillHitboxLayoutData.HitBoxType.Box) Handles.DrawWireCube(Vector3.zero, Vector3.Scale(shape.Size, scale));
            else if (shape.Type == SkillHitboxLayoutData.HitBoxType.Sphere)
                DrawSphere(shape.Radius * Mathf.Max(scale.x, scale.y, scale.z));
            else
            {
                int d = Mathf.Clamp(shape.Direction, 0, 2);
                float radius = shape.Radius * Mathf.Max(scale[(d + 1) % 3], scale[(d + 2) % 3]);
                DrawCapsule(radius, Mathf.Max(shape.Height * scale[d], radius * 2), d);
            }
        }
        bool picked = !selected && Handles.Button(center, Quaternion.identity, HandleUtility.GetHandleSize(center) * .035f,
            HandleUtility.GetHandleSize(center) * .06f, Handles.DotHandleCap);
        if (selected)
        {
            EditorGUI.BeginChangeCheck();
            if (Mode != EditMode.Size)
            {
                using (new Handles.DrawingScope(basis))
                {
                    Vector3 position = shape.LocalPosition, scaleValue = shape.LocalScale;
                    Quaternion rotation = Quaternion.Euler(shape.LocalEulerAngles);
                    if (Mode == EditMode.Move) position = Handles.PositionHandle(position, rotation);
                    else if (Mode == EditMode.Rotate) rotation = Handles.RotationHandle(rotation, position);
                    else scaleValue = Handles.ScaleHandle(scaleValue, position, rotation, HandleUtility.GetHandleSize(position));
                    if (EditorGUI.EndChangeCheck())
                    {
                        session.Record("Transform Hitbox Shape");
                        shape.LocalPosition = position; shape.LocalEulerAngles = rotation.eulerAngles; shape.LocalScale = scaleValue;
                    }
                }
            }
            else
            {
                using (new Handles.DrawingScope(physical))
                {
                    Vector3 newCenter;
                    if (shape.Type == SkillHitboxLayoutData.HitBoxType.Box)
                    {
                        box.center = Vector3.zero; box.size = Vector3.Scale(shape.Size, scale); box.SetColor(color); box.DrawHandle(); newCenter = box.center;
                    }
                    else if (shape.Type == SkillHitboxLayoutData.HitBoxType.Sphere)
                    {
                        sphere.center = Vector3.zero; sphere.radius = shape.Radius * Mathf.Max(scale.x, scale.y, scale.z); sphere.SetColor(color); sphere.DrawHandle(); newCenter = sphere.center;
                    }
                    else
                    {
                        int d = Mathf.Clamp(shape.Direction, 0, 2);
                        capsule.heightAxis = (CapsuleBoundsHandle.HeightAxis)d;
                        capsule.center = Vector3.zero; capsule.radius = shape.Radius * Mathf.Max(scale[(d + 1) % 3], scale[(d + 2) % 3]);
                        capsule.height = Mathf.Max(shape.Height * scale[d], capsule.radius * 2); capsule.SetColor(color); capsule.DrawHandle(); newCenter = capsule.center;
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        session.Record("Resize Hitbox Shape");
                        shape.Center = matrix.inverse.MultiplyPoint3x4(physical.MultiplyPoint3x4(newCenter));
                        if (shape.Type == SkillHitboxLayoutData.HitBoxType.Box) shape.Size = Divide(box.size, scale);
                        else if (shape.Type == SkillHitboxLayoutData.HitBoxType.Sphere) shape.Radius = sphere.radius / Mathf.Max(.00001f, scale.x, scale.y, scale.z);
                        else
                        {
                            int d = Mathf.Clamp(shape.Direction, 0, 2);
                            shape.Radius = capsule.radius / Mathf.Max(.00001f, scale[(d + 1) % 3], scale[(d + 2) % 3]);
                            shape.Height = capsule.height / Mathf.Max(.00001f, scale[d]);
                        }
                    }
                }
            }
        }
        Handles.color = oldColor;
        return picked;
    }

    static void DrawSphere(float radius)
    {
        Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
        Handles.DrawWireDisc(Vector3.zero, Vector3.right, radius);
        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
    }
    static void DrawCapsule(float radius, float height, int direction)
    {
        Vector3 axis = Vector3.zero; axis[direction] = 1;
        Vector3 side = Vector3.zero; side[(direction + 1) % 3] = 1;
        Vector3 other = Vector3.Cross(axis, side), end = axis * Mathf.Max(0, height * .5f - radius);
        Handles.DrawWireDisc(end, axis, radius); Handles.DrawWireDisc(-end, axis, radius);
        foreach (var tangent in new[] { side, -side, other, -other }) Handles.DrawLine(end + tangent * radius, -end + tangent * radius);
        Handles.DrawWireArc(end, other, side, -180, radius); Handles.DrawWireArc(-end, other, side, 180, radius);
        Handles.DrawWireArc(end, side, other, 180, radius); Handles.DrawWireArc(-end, side, other, -180, radius);
    }
}
#endif
