using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// Discovers concrete IPayloadDesignerDescriptor types through TypeCache and indexes them by
// exact PayloadType. Lazily built and cached in a static field for the lifetime of the editor
// domain, matching the convention already used by SkillPayloadAssetUtility.GetPayloadTypes and
// ActiveSkillTreeEditorWindow's cached step-type list -- no [InitializeOnLoad] needed, a domain
// reload already resets static fields.
public static class PayloadDesignerDescriptorRegistry
{
    private static Dictionary<Type, IPayloadDesignerDescriptor> cachedDescriptorsByType;
    private static List<PayloadAuthoringIssue> cachedDiagnostics;

    public static void InvalidateCache()
    {
        cachedDescriptorsByType = null;
        cachedDiagnostics = null;
    }

    // Registry-level problems: duplicate descriptors, descriptors mapped to an invalid/abstract
    // type, or a descriptor mapped to CompositeSkillPayloadDef. Does not include per-payload
    // authoring issues -- see IPayloadDesignerDescriptor.CollectAuthoringIssues for those.
    public static IReadOnlyList<PayloadAuthoringIssue> GetDiagnostics()
    {
        EnsureBuilt();
        return cachedDiagnostics;
    }

    public static bool TryGetDescriptor(Type payloadType, out IPayloadDesignerDescriptor descriptor)
    {
        EnsureBuilt();
        if (payloadType == null)
        {
            descriptor = null;
            return false;
        }

        return cachedDescriptorsByType.TryGetValue(payloadType, out descriptor);
    }

    public static bool TryGetDescriptor(SkillPayloadDef payload, out IPayloadDesignerDescriptor descriptor)
    {
        descriptor = null;
        return payload != null && TryGetDescriptor(payload.GetType(), out descriptor);
    }

    // Descriptor-backed, non-composite payload types available to the normal designer picker.
    // A payload without a valid descriptor must never appear here (plan section 3, decision 11).
    public static IReadOnlyList<IPayloadDesignerDescriptor> GetPickerDescriptors()
    {
        EnsureBuilt();
        return cachedDescriptorsByType.Values
            .Where(descriptor => descriptor.PayloadType != typeof(CompositeSkillPayloadDef))
            .OrderBy(descriptor => descriptor.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Concrete SkillPayloadDef types that exist in the project but have no registered descriptor.
    // Composite is always excluded -- it is a structural payload, not a designer-facing ability.
    // Advanced/Developer diagnostics use this list; the normal picker never does.
    public static IReadOnlyList<Type> GetPayloadTypesWithoutDescriptor()
    {
        EnsureBuilt();
        return SkillPayloadAssetUtility.GetPayloadTypes()
            .Where(type => type != typeof(CompositeSkillPayloadDef) && !cachedDescriptorsByType.ContainsKey(type))
            .ToList();
    }

    private static void EnsureBuilt()
    {
        if (cachedDescriptorsByType != null)
            return;

        var descriptorsByType = new Dictionary<Type, IPayloadDesignerDescriptor>();
        var diagnostics = new List<PayloadAuthoringIssue>();
        var descriptorTypesByPayloadType = new Dictionary<Type, List<Type>>();

        List<Type> descriptorTypes = TypeCache.GetTypesDerivedFrom<IPayloadDesignerDescriptor>()
            .Where(type => type != null && !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition)
            .ToList();

        for (int i = 0; i < descriptorTypes.Count; i++)
        {
            Type descriptorType = descriptorTypes[i];
            IPayloadDesignerDescriptor descriptor;
            try
            {
                descriptor = Activator.CreateInstance(descriptorType) as IPayloadDesignerDescriptor;
            }
            catch (Exception e)
            {
                diagnostics.Add(PayloadAuthoringIssue.Error(
                    $"Descriptor '{descriptorType.FullName}' threw while being constructed: {e.Message}"));
                continue;
            }

            if (descriptor == null)
            {
                diagnostics.Add(PayloadAuthoringIssue.Error(
                    $"Descriptor '{descriptorType.FullName}' could not be instantiated."));
                continue;
            }

            Type payloadType = descriptor.PayloadType;
            if (payloadType == null || payloadType.IsAbstract || !typeof(SkillPayloadDef).IsAssignableFrom(payloadType))
            {
                diagnostics.Add(PayloadAuthoringIssue.Error(
                    $"Descriptor '{descriptorType.FullName}' maps to an invalid payload type " +
                    $"('{payloadType?.FullName ?? "null"}'). PayloadType must be a concrete SkillPayloadDef."));
                continue;
            }

            if (payloadType == typeof(CompositeSkillPayloadDef))
            {
                diagnostics.Add(PayloadAuthoringIssue.Error(
                    $"Descriptor '{descriptorType.FullName}' maps to CompositeSkillPayloadDef, which cannot " +
                    "be selected as a child ability payload."));
                continue;
            }

            if (!descriptorTypesByPayloadType.TryGetValue(payloadType, out List<Type> owningDescriptorTypes))
            {
                owningDescriptorTypes = new List<Type>();
                descriptorTypesByPayloadType[payloadType] = owningDescriptorTypes;
            }

            owningDescriptorTypes.Add(descriptorType);

            if (!descriptorsByType.ContainsKey(payloadType))
                descriptorsByType[payloadType] = descriptor;
        }

        foreach (KeyValuePair<Type, List<Type>> entry in descriptorTypesByPayloadType)
        {
            if (entry.Value.Count <= 1)
                continue;

            string owners = string.Join(", ", entry.Value.Select(type => type.FullName));
            diagnostics.Add(PayloadAuthoringIssue.Error(
                $"Payload type '{entry.Key.FullName}' has {entry.Value.Count} descriptors ({owners}). " +
                "Exactly one descriptor is allowed per payload type."));
        }

        cachedDescriptorsByType = descriptorsByType;
        cachedDiagnostics = diagnostics;
    }
}
