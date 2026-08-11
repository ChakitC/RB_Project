#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

/// <summary>หนึ่ง <see cref="ConditionalStatusRoute"/> field ที่ประกาศไว้บน payload/step ชนิดหนึ่ง.</summary>
public sealed class SkillStatusRouteFieldInfo
{
    public FieldInfo Field;

    /// <summary>ชื่อ field ตามที่ serialize (= ส่วนแรกของ property path).</summary>
    public string FieldName;

    public SkillStatusTarget? FixedTarget;

    /// <summary>Member ที่คืน target เมื่อ target ขึ้นกับ behavior. null = ใช้ <see cref="FixedTarget"/>.</summary>
    public MemberInfo TargetMember;

    public string Label;

    /// <summary>ไม่ null = ประกาศผิด และ route นี้ใช้ไม่ได้ (ต้องรายงาน ไม่ใช่เดา target ให้).</summary>
    public string MetadataError;

    public string ApplicationsPropertyPath =>
        $"{FieldName}.{ConditionalStatusRoute.ApplicationsFieldName}";
}

/// <summary>
/// อ่าน metadata ของ conditional status route จาก declaration จริงด้วย reflection แล้ว cache ตาม
/// <see cref="Type"/>.
///
/// จุดประสงค์: resolver, scanner และ inspector drawer ใช้คำตอบชุดเดียวกันว่า "field ไหนเป็น route
/// และ route นั้นลงให้ใคร" โดยไม่มีใครต้องรู้จักชนิด payload/step ที่ลง status.
/// </summary>
public static class SkillStatusRouteMetadata
{
    static readonly Dictionary<Type, List<SkillStatusRouteFieldInfo>> CacheByType = new();

    public static IReadOnlyList<SkillStatusRouteFieldInfo> GetRouteFields(Type type)
    {
        if (type == null)
            return Array.Empty<SkillStatusRouteFieldInfo>();

        if (CacheByType.TryGetValue(type, out List<SkillStatusRouteFieldInfo> cached))
            return cached;

        var result = new List<SkillStatusRouteFieldInfo>();
        CollectHierarchy(type, result);
        CacheByType[type] = result;
        return result;
    }

    /// <summary>คืน route field ที่ชื่อตรงกับ <paramref name="fieldName"/>, null เมื่อไม่มี.</summary>
    public static SkillStatusRouteFieldInfo FindRouteField(Type type, string fieldName)
    {
        IReadOnlyList<SkillStatusRouteFieldInfo> fields = GetRouteFields(type);
        for (int i = 0; i < fields.Count; i++)
        {
            if (string.Equals(fields[i].FieldName, fieldName, StringComparison.Ordinal))
                return fields[i];
        }

        return null;
    }

    public static bool TryResolveTarget(
        SkillStatusRouteFieldInfo info,
        object owner,
        out SkillStatusTarget target,
        out string error)
    {
        target = SkillStatusTarget.Self;
        error = null;

        if (info == null)
        {
            error = "Missing conditional status route metadata.";
            return false;
        }

        if (!string.IsNullOrEmpty(info.MetadataError))
        {
            error = info.MetadataError;
            return false;
        }

        if (info.FixedTarget.HasValue)
        {
            target = info.FixedTarget.Value;
            return true;
        }

        if (owner == null)
        {
            error =
                $"'{Describe(info)}' reads its target from '{info.TargetMember?.Name}', but the owning " +
                "payload/step instance could not be resolved.";
            return false;
        }

        try
        {
            object value = info.TargetMember switch
            {
                PropertyInfo property => property.GetValue(owner),
                FieldInfo field => field.GetValue(owner),
                MethodInfo method => method.Invoke(owner, null),
                _ => null,
            };

            if (value is SkillStatusTarget resolved)
            {
                target = resolved;
                return true;
            }

            error = $"'{Describe(info)}' target member '{info.TargetMember?.Name}' returned no target.";
            return false;
        }
        catch (Exception exception)
        {
            error = $"'{Describe(info)}' target member threw: {exception.Message}";
            return false;
        }
    }

    /// <summary>ป้ายของ route: label จาก attribute, ไม่มีก็ใช้ชื่อชนิดที่ประกาศ field.</summary>
    public static string ResolveLabel(SkillStatusRouteFieldInfo info, Type ownerType)
    {
        if (!string.IsNullOrEmpty(info?.Label))
            return info.Label;

        Type type = info?.Field?.DeclaringType ?? ownerType;
        return type != null ? ObjectNames.NicifyVariableName(type.Name) : "Conditional Status";
    }

    static void CollectHierarchy(Type type, List<SkillStatusRouteFieldInfo> result)
    {
        // base -> derived so declaration order stays stable and private base fields are not missed
        // (GetFields on a derived type does not return private fields of its base types).
        if (type.BaseType != null)
            CollectHierarchy(type.BaseType, result);

        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.FieldType != typeof(ConditionalStatusRoute))
                continue;

            result.Add(Build(field));
        }
    }

    static SkillStatusRouteFieldInfo Build(FieldInfo field)
    {
        var info = new SkillStatusRouteFieldInfo
        {
            Field = field,
            FieldName = field.Name,
        };

        var attribute = field.GetCustomAttribute<SkillStatusRouteTargetAttribute>(true);
        if (attribute == null)
        {
            info.MetadataError =
                $"'{field.DeclaringType?.Name}.{field.Name}' is a {nameof(ConditionalStatusRoute)} with no " +
                $"[{nameof(SkillStatusRouteTargetAttribute)}]. Add the attribute so tooling knows who the " +
                "status is applied to.";
            return info;
        }

        info.Label = attribute.Label;

        if (attribute.FixedTarget.HasValue)
        {
            info.FixedTarget = attribute.FixedTarget;
            return info;
        }

        if (string.IsNullOrWhiteSpace(attribute.TargetMemberName))
        {
            info.MetadataError =
                $"'{field.DeclaringType?.Name}.{field.Name}' declares an empty target member name.";
            return info;
        }

        info.TargetMember = FindTargetMember(field.DeclaringType, attribute.TargetMemberName);
        if (info.TargetMember == null)
        {
            info.MetadataError =
                $"'{field.DeclaringType?.Name}.{field.Name}' points at target member " +
                $"'{attribute.TargetMemberName}', which does not exist or does not return a " +
                $"{nameof(SkillStatusTarget)}.";
        }

        return info;
    }

    static MemberInfo FindTargetMember(Type declaringType, string memberName)
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        for (Type type = declaringType; type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(SkillStatusTarget) && property.CanRead)
                return property;

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(SkillStatusTarget))
                return field;

            MethodInfo method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(SkillStatusTarget))
                return method;
        }

        return null;
    }

    static string Describe(SkillStatusRouteFieldInfo info) =>
        $"{info.Field?.DeclaringType?.Name}.{info.FieldName}";
}
#endif
