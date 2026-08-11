#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// จุดหนึ่งในสกิลที่รับ conditional status application ได้จริง — คือ <see cref="ConditionalStatusRoute"/>
/// field ที่ประกาศไว้บน payload หรือ step ตัวใดตัวหนึ่ง.
///
/// Route ไม่ได้ "สร้างที่ทาง" ให้: ถ้าสกิลไม่มี HealArea(Allies) ก็จะไม่มี route Allies และ wizard
/// ต้องปิด target นั้นทิ้ง แทนที่จะแอบสร้าง Step/Payload ใหม่ให้ (การ insert step เปลี่ยนลำดับ execution
/// ของสกิลซึ่งเป็นการตัดสินใจด้าน design ไม่ใช่ผลข้างเคียงของการเพิ่มบัฟ).
/// </summary>
public sealed class SkillStatusRoute
{
    public SkillStatusTarget Target;

    /// <summary>Asset ที่ SerializedObject ของมันถือ list — payload sub-asset หรือ composite เอง.</summary>
    public UnityEngine.Object Container;

    /// <summary>Property path ของ list ภายใน <see cref="Container"/>.</summary>
    public string ListPropertyPath;

    /// <summary>Index ของ composite step ที่ route นี้อยู่, -1 เมื่อ payload เป็น payload หลักของสกิล.</summary>
    public int StepIndex = -1;

    /// <summary>ป้ายที่โชว์ใน Destination selector.</summary>
    public string DisplayLabel;

    /// <summary>ชื่อ route field เอง เช่น "Apply Status" — ไม่มี step/target ปน สำหรับการ์ดที่มี Summary
    /// บอก target อยู่แล้ว.</summary>
    public string FieldLabel;

    /// <summary>ตำแหน่งของ route เช่น "Step 2" หรือ "Skill payload".</summary>
    public string LocationLabel;

    /// <summary>
    /// คีย์ที่คงที่ข้ามการ re-resolve ภายใน session เดียวกัน ใช้จำ destination ที่ผู้ใช้เลือกไว้.
    /// อ้าง ชนิด container + step index + property path แทน InstanceID เพราะ InstanceID เปลี่ยนหลัง
    /// domain reload — และเพราะ payload/step หนึ่งตัวมีได้หลาย route ที่ target เดียวกัน.
    /// </summary>
    public string RouteKey;

    public SerializedObject CreateSerializedObject() => new(Container);

    public SerializedProperty FindList(SerializedObject serialized) =>
        serialized?.FindProperty(ListPropertyPath);

    public bool Matches(SkillStatusRoute other) =>
        other != null && string.Equals(RouteKey, other.RouteKey, StringComparison.Ordinal);

    public static string DescribeTarget(SkillStatusTarget target)
    {
        switch (target)
        {
            case SkillStatusTarget.Allies:
                return "Allies";
            case SkillStatusTarget.TauntedEnemies:
                return "Taunted Enemies";
            default:
                return "Self";
        }
    }
}

/// <summary>ผลของการ resolve: route ที่ใช้ได้ + ปัญหาการประกาศที่ต้องรายงาน.</summary>
public sealed class SkillStatusRouteResolutionResult
{
    public readonly List<SkillStatusRoute> Routes = new();

    /// <summary>Metadata ที่ประกาศผิด — blocking เสมอ ไม่ fallback เป็น Self.</summary>
    public readonly List<string> Issues = new();
}

/// <summary>
/// หา route ทั้งหมดของสกิลหนึ่งตัว โดยเดินโครงสร้างจริง (composite steps + embedded payload sub-assets)
/// ไม่ใช่การเดาจากชื่อ asset.
///
/// Resolver รู้จักเฉพาะ "โครงของสกิล" — root payload, composite และ PayloadStep — ไม่รู้จักชนิด payload
/// ที่ลง status. Payload/Step ใหม่ที่ถือ <see cref="ConditionalStatusRoute"/> จึงถูกพบเองโดยไม่ต้องแก้ไฟล์นี้.
/// </summary>
public static class SkillStatusRouteResolver
{
    const string StepsArrayFormat = "steps.Array.data[{0}]";

    public static List<SkillStatusRoute> Resolve(SkillGemDefinition skill) => ResolveDetailed(skill).Routes;

    public static SkillStatusRouteResolutionResult ResolveDetailed(SkillGemDefinition skill)
    {
        var result = new SkillStatusRouteResolutionResult();
        if (skill == null || skill.payload == null)
            return result;

        if (skill.payload is CompositeSkillPayloadDef composite)
        {
            IReadOnlyList<SkillEffectStep> steps = composite.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                SkillEffectStep step = steps[i];
                if (step == null)
                    continue;

                if (step is PayloadStep payloadStep)
                {
                    if (payloadStep.Payload != null)
                        CollectRoutes(payloadStep.Payload, payloadStep.Payload, string.Empty, i, result);

                    continue;
                }

                CollectRoutes(composite, step, $"{string.Format(StepsArrayFormat, i)}.", i, result);
            }

            return result;
        }

        CollectRoutes(skill.payload, skill.payload, string.Empty, -1, result);
        return result;
    }

    /// <summary>Target ที่สกิลนี้รองรับจริง — ใช้ปิดตัวเลือกใน wizard.</summary>
    public static List<SkillStatusTarget> ResolveTargets(IReadOnlyList<SkillStatusRoute> routes)
    {
        var targets = new List<SkillStatusTarget>();
        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            if (!targets.Contains(routes[i].Target))
                targets.Add(routes[i].Target);
        }

        targets.Sort((first, second) => ((int)first).CompareTo((int)second));
        return targets;
    }

    public static List<SkillStatusRoute> FilterByTarget(
        IReadOnlyList<SkillStatusRoute> routes,
        SkillStatusTarget target)
    {
        var result = new List<SkillStatusRoute>();
        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            if (routes[i].Target == target)
                result.Add(routes[i]);
        }

        return result;
    }

    public static SkillStatusRoute FindByKey(IReadOnlyList<SkillStatusRoute> routes, string routeKey)
    {
        if (string.IsNullOrEmpty(routeKey))
            return null;

        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            if (string.Equals(routes[i].RouteKey, routeKey, StringComparison.Ordinal))
                return routes[i];
        }

        return null;
    }

    /// <summary>
    /// เดิน route field ทุกตัวที่ <paramref name="owner"/> ประกาศไว้.
    /// <paramref name="container"/> คือ Object ที่ SerializedObject จะถูกสร้างจาก — สำหรับ step ที่ฝัง
    /// ใน composite คือ composite เอง ส่วน owner คือ instance ของ step ที่ถือค่า target จริง.
    /// </summary>
    static void CollectRoutes(
        UnityEngine.Object container,
        object owner,
        string propertyPathPrefix,
        int stepIndex,
        SkillStatusRouteResolutionResult result)
    {
        if (container == null || owner == null)
            return;

        IReadOnlyList<SkillStatusRouteFieldInfo> fields =
            SkillStatusRouteMetadata.GetRouteFields(owner.GetType());
        var serialized = new SerializedObject(container);

        string where = stepIndex >= 0 ? $"Step {stepIndex}" : "Skill payload";

        for (int i = 0; i < fields.Count; i++)
        {
            SkillStatusRouteFieldInfo field = fields[i];
            if (!SkillStatusRouteMetadata.TryResolveTarget(field, owner, out SkillStatusTarget target,
                    out string error))
            {
                result.Issues.Add($"{where}: {error}");
                continue;
            }

            string listPath = $"{propertyPathPrefix}{field.ApplicationsPropertyPath}";
            SerializedProperty list = serialized.FindProperty(listPath);
            if (list == null || !list.isArray)
            {
                result.Issues.Add(
                    $"{where}: '{owner.GetType().Name}.{field.FieldName}' does not serialize a conditional " +
                    $"status application list at '{listPath}'. Ensure the route field is serialized and " +
                    $"contains '{ConditionalStatusRoute.ApplicationsFieldName}'.");
                continue;
            }

            string label = SkillStatusRouteMetadata.ResolveLabel(field, owner.GetType());

            result.Routes.Add(new SkillStatusRoute
            {
                Target = target,
                Container = container,
                ListPropertyPath = listPath,
                StepIndex = stepIndex,
                DisplayLabel = $"{where} — {label} ({SkillStatusRoute.DescribeTarget(target)})",
                FieldLabel = label,
                LocationLabel = where,
                RouteKey = $"{container.GetType().Name}|{stepIndex}|{listPath}",
            });
        }
    }
}
#endif
