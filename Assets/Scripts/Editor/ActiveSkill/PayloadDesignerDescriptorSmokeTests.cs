#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Smoke tests for the Phase 1 payload designer descriptor foundation (registry discovery,
/// safe defaults, and summary/issue generation on incomplete drafts). Runs against
/// ScriptableObject instances in memory -- unlike the status-effect authoring smoke tests,
/// nothing here needs to be an asset on disk.
/// </summary>
public static class PayloadDesignerDescriptorSmokeTests
{
    static readonly Type[] ExpectedDescriptorBackedPayloadTypes =
    {
        typeof(TauntSkillPayloadDef),
        typeof(ApplyStatusSkillPayloadDef),
        typeof(MorphSkillPayloadDef),
        typeof(SpawnPickupSkillPayloadDef),
        typeof(PrefabHitboxSkillPayloadDef),
        typeof(ProjectileSkillPayloadDef),
        typeof(HealAreaSkillPayloadDef),
        typeof(SummonSkillPayloadDef),
    };

    [MenuItem("Tools/RB/Skills/Run Payload Descriptor Smoke Tests")]
    public static void RunFromMenu() => RunFromCommandLine();

    public static void RunFromCommandLine()
    {
        PayloadDesignerDescriptorRegistry.InvalidateCache();

        TestRegistryHasNoDiagnostics();
        TestEveryExpectedPayloadTypeHasExactlyOneDescriptor();
        TestCompositeIsNeverExposedToThePicker();
        TestUnknownProbePayloadsHaveNoDescriptor();
        TestSafeDefaultsAndSummaryAndIssuesDoNotThrowOnFreshDrafts();
        TestMissingRequiredReferenceIsReportedAsAnError();

        Debug.Log("[PayloadDescriptorTests] All payload designer descriptor smoke tests passed.");
    }

    static void TestRegistryHasNoDiagnostics()
    {
        IReadOnlyList<PayloadAuthoringIssue> diagnostics = PayloadDesignerDescriptorRegistry.GetDiagnostics();
        Expect(diagnostics.Count == 0,
            $"Registry reported {diagnostics.Count} diagnostic(s), expected none: " +
            string.Join(" | ", diagnostics.Select(d => d.Message)));
    }

    static void TestEveryExpectedPayloadTypeHasExactlyOneDescriptor()
    {
        for (int i = 0; i < ExpectedDescriptorBackedPayloadTypes.Length; i++)
        {
            Type payloadType = ExpectedDescriptorBackedPayloadTypes[i];
            Expect(PayloadDesignerDescriptorRegistry.TryGetDescriptor(payloadType, out IPayloadDesignerDescriptor descriptor),
                $"Expected a descriptor for '{payloadType.Name}'.");
            Equal(payloadType, descriptor.PayloadType, $"Descriptor for '{payloadType.Name}' reports a mismatched PayloadType.");
        }

        IReadOnlyList<IPayloadDesignerDescriptor> pickerDescriptors = PayloadDesignerDescriptorRegistry.GetPickerDescriptors();
        Equal(ExpectedDescriptorBackedPayloadTypes.Length, pickerDescriptors.Count,
            "Picker descriptor count does not match the expected Phase 1 payload set.");
    }

    static void TestCompositeIsNeverExposedToThePicker()
    {
        IReadOnlyList<IPayloadDesignerDescriptor> pickerDescriptors = PayloadDesignerDescriptorRegistry.GetPickerDescriptors();
        Expect(pickerDescriptors.All(d => d.PayloadType != typeof(CompositeSkillPayloadDef)),
            "CompositeSkillPayloadDef must never appear in the normal designer picker.");
    }

    static void TestUnknownProbePayloadsHaveNoDescriptor()
    {
        // Every concrete SkillPayloadDef in the project besides the expected descriptor-backed
        // set and Composite must be reported as missing a descriptor (e.g. the route-metadata
        // probe payload types declared by ActiveSkillStatusEffectAuthoringSmokeTests).
        IReadOnlyList<Type> withoutDescriptor = PayloadDesignerDescriptorRegistry.GetPayloadTypesWithoutDescriptor();
        Expect(withoutDescriptor.All(t => !ExpectedDescriptorBackedPayloadTypes.Contains(t)),
            "A Phase 1 payload type was unexpectedly reported as missing a descriptor.");
        Expect(!withoutDescriptor.Contains(typeof(CompositeSkillPayloadDef)),
            "CompositeSkillPayloadDef must never appear in the missing-descriptor diagnostics list.");
    }

    static void TestSafeDefaultsAndSummaryAndIssuesDoNotThrowOnFreshDrafts()
    {
        var context = new PayloadDesignerContext(null, null, null);

        for (int i = 0; i < ExpectedDescriptorBackedPayloadTypes.Length; i++)
        {
            Type payloadType = ExpectedDescriptorBackedPayloadTypes[i];
            Expect(PayloadDesignerDescriptorRegistry.TryGetDescriptor(payloadType, out IPayloadDesignerDescriptor descriptor),
                $"Expected a descriptor for '{payloadType.Name}'.");

            var draft = (SkillPayloadDef)ScriptableObject.CreateInstance(payloadType);
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                descriptor.ApplySafeDefaults(draft, context);

                PayloadGameplaySummary summary = descriptor.BuildSummary(draft, context);
                Expect(summary != null, $"'{payloadType.Name}' descriptor returned a null summary for a fresh draft.");
                Expect(!string.IsNullOrEmpty(summary.Headline), $"'{payloadType.Name}' descriptor returned an empty headline.");

                var issues = new List<PayloadAuthoringIssue>();
                descriptor.CollectAuthoringIssues(draft, context, issues);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"'{payloadType.Name}' descriptor threw on a fresh incomplete draft: {e}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }
    }

    static void TestMissingRequiredReferenceIsReportedAsAnError()
    {
        var context = new PayloadDesignerContext(null, null, null);

        // Every payload type has at least one required asset reference that ApplySafeDefaults
        // must not fabricate (plan section 9.4), except HealAreaSkillPayloadDef -- a fresh
        // Self-target draft with no status effects is a legitimate pure heal-power burst and is
        // covered separately by HealAreaPayloadParitySmokeTests.TestPayloadValidationDoesNotThrowOnAnEmptyDraft.
        // A fresh draft of every other type must report at least one blocking Error, never a
        // silently "valid" state.
        for (int i = 0; i < ExpectedDescriptorBackedPayloadTypes.Length; i++)
        {
            Type payloadType = ExpectedDescriptorBackedPayloadTypes[i];
            if (payloadType == typeof(HealAreaSkillPayloadDef))
                continue;

            PayloadDesignerDescriptorRegistry.TryGetDescriptor(payloadType, out IPayloadDesignerDescriptor descriptor);

            var draft = (SkillPayloadDef)ScriptableObject.CreateInstance(payloadType);
            draft.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                descriptor.ApplySafeDefaults(draft, context);

                var issues = new List<PayloadAuthoringIssue>();
                descriptor.CollectAuthoringIssues(draft, context, issues);

                Expect(issues.HasErrors(),
                    $"'{payloadType.Name}' descriptor reported no Error on a fresh draft with no required " +
                    "references assigned -- Create must stay blocked until the designer supplies them.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }
    }

    #region Assertions

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    #endregion
}
#endif
