using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ASP.Scripts.Editor
{
    internal sealed class ASPCharacterShaderVariantStripper : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static bool s_IsBuildInProgress;
        private static bool s_HasLoggedUsageSummary;
        private static long s_OnProcessShaderElapsedTicks;
        private static int s_OnProcessShaderInvocationCount;
        private static readonly HashSet<string> s_LoggedPasses = new HashSet<string>(StringComparer.Ordinal);

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ASPCharacterShaderFeatureUsage.ClearCache();
            s_IsBuildInProgress = true;
            s_HasLoggedUsageSummary = false;
            s_OnProcessShaderElapsedTicks = 0L;
            s_OnProcessShaderInvocationCount = 0;
            s_LoggedPasses.Clear();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!s_IsBuildInProgress)
            {
                return;
            }

            s_IsBuildInProgress = false;

            if (s_OnProcessShaderInvocationCount == 0)
            {
                Debug.Log(
                    $"ASPCharacterShaderVariantStripper: OnProcessShader was not invoked during build ({report.summary.result}).");
                return;
            }

            var elapsedSeconds = (double)s_OnProcessShaderElapsedTicks / Stopwatch.Frequency;
            var elapsedMilliseconds = elapsedSeconds * 1000.0;
            var averageMilliseconds = elapsedMilliseconds / s_OnProcessShaderInvocationCount;

            Debug.Log(
                $"ASPCharacterShaderVariantStripper: OnProcessShader took {elapsedMilliseconds:F2} ms total " +
                $"across {s_OnProcessShaderInvocationCount} calls during build ({report.summary.result}, avg {averageMilliseconds:F4} ms/call).");
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            var shouldMeasure = s_IsBuildInProgress;
            var startTimestamp = shouldMeasure ? Stopwatch.GetTimestamp() : 0L;

            try
            {
                if (shader == null || data == null || data.Count == 0)
                {
                    return;
                }

                if (!string.Equals(shader.name, ASPCharacterShaderFeatureUsage.TargetShaderName, StringComparison.Ordinal))
                {
                    return;
                }

                var usage = ASPCharacterShaderFeatureUsage.Analyze();
                if (!usage.IsReady || usage.MaterialCount == 0)
                {
                    return;
                }

                if (!s_HasLoggedUsageSummary)
                {
                    var globallyUnusedFeatures = usage.GetUnusedFeatures();
                    Debug.Log(
                        $"ASPCharacterShaderVariantStripper: tracking {usage.MaterialCount} materials, " +
                        $"{usage.SignatureCount} unique combinations, {usage.FeatureNames.Count} local shader features.");
                    if (globallyUnusedFeatures.Count > 0)
                    {
                        Debug.Log(
                            "ASPCharacterShaderVariantStripper: globally stripped local features: " +
                            string.Join(", ", globallyUnusedFeatures));
                    }

                    s_HasLoggedUsageSummary = true;
                }

                var featureNames = usage.FeatureNames;
                var passMask = 0UL;
                var passKeywords = new LocalKeyword[featureNames.Count];

                for (var featureIndex = 0; featureIndex < featureNames.Count; featureIndex++)
                {
                    var keyword = new LocalKeyword(shader, featureNames[featureIndex]);
                    passKeywords[featureIndex] = keyword;

                    for (var compilerIndex = 0; compilerIndex < data.Count; compilerIndex++)
                    {
                        if (!data[compilerIndex].shaderKeywordSet.IsEnabled(keyword))
                        {
                            continue;
                        }

                        passMask |= 1UL << featureIndex;
                        break;
                    }
                }

                var allowedProjectedSignatures = usage.GetProjectedSignatures(passMask);
                var usedFeatureMaskInPass = usage.GetProjectedUsedFeatureMask(passMask);
                var strippedCount = 0;

                var passLogKey = $"{snippet.passName}|{snippet.passType}|{snippet.shaderType}";
                if (s_LoggedPasses.Add(passLogKey))
                {
                    var passUnusedFeatures = usage.CollectFeaturesFromMask(passMask & ~usedFeatureMaskInPass);
                    if (passUnusedFeatures.Count > 0)
                    {
                        Debug.Log(
                            $"ASPCharacterShaderVariantStripper: pass '{snippet.passName}' strips unused local features: " +
                            string.Join(", ", passUnusedFeatures));
                    }
                }

                for (var compilerIndex = data.Count - 1; compilerIndex >= 0; compilerIndex--)
                {
                    var variantSignature = 0UL;

                    for (var featureIndex = 0; featureIndex < passKeywords.Length; featureIndex++)
                    {
                        var featureBit = 1UL << featureIndex;
                        if ((passMask & featureBit) == 0)
                        {
                            continue;
                        }

                        if (data[compilerIndex].shaderKeywordSet.IsEnabled(passKeywords[featureIndex]))
                        {
                            variantSignature |= featureBit;
                        }
                    }

                    if (allowedProjectedSignatures.Contains(variantSignature))
                    {
                        continue;
                    }

                    data.RemoveAt(compilerIndex);
                    strippedCount++;
                }

                if (strippedCount > 0)
                {
                    Debug.Log(
                        $"ASPCharacterShaderVariantStripper: stripped {strippedCount} variants from " +
                        $"{shader.name} pass '{snippet.passName}'.");
                }
            }
            finally
            {
                if (shouldMeasure)
                {
                    s_OnProcessShaderElapsedTicks += Stopwatch.GetTimestamp() - startTimestamp;
                    s_OnProcessShaderInvocationCount++;
                }
            }
        }
    }

    public static class ASPCharacterShaderFeatureUsage
    {
        private enum StyleMode
        {
            Unknown = -1,
            StylizePbr = 0,
            CelShading = 1,
            Face = 2,
        }

        internal sealed class UsageData
        {
            public static readonly UsageData Empty = new UsageData(
                false,
                new List<string>(),
                new HashSet<ulong>(),
                new Dictionary<ulong, List<string>>(),
                0UL);

            private readonly Dictionary<ulong, HashSet<ulong>> _projectedSignaturesByMask =
                new Dictionary<ulong, HashSet<ulong>>();

            public UsageData(
                bool isReady,
                List<string> featureNames,
                HashSet<ulong> materialSignatures,
                Dictionary<ulong, List<string>> materialPathsBySignature,
                ulong usedFeatureMask)
            {
                IsReady = isReady;
                FeatureNames = featureNames;
                MaterialSignatures = materialSignatures;
                MaterialPathsBySignature = materialPathsBySignature;
                UsedFeatureMask = usedFeatureMask;
            }

            public bool IsReady { get; }

            public List<string> FeatureNames { get; }

            public HashSet<ulong> MaterialSignatures { get; }

            public Dictionary<ulong, List<string>> MaterialPathsBySignature { get; }

            public ulong UsedFeatureMask { get; }

            public int MaterialCount
            {
                get
                {
                    var total = 0;
                    foreach (var entry in MaterialPathsBySignature)
                    {
                        total += entry.Value.Count;
                    }

                    return total;
                }
            }

            public int SignatureCount => MaterialSignatures.Count;

            public HashSet<ulong> GetProjectedSignatures(ulong passMask)
            {
                if (_projectedSignaturesByMask.TryGetValue(passMask, out var projectedSignatures))
                {
                    return projectedSignatures;
                }

                projectedSignatures = new HashSet<ulong>();
                foreach (var signature in MaterialSignatures)
                {
                    projectedSignatures.Add(signature & passMask);
                }

                _projectedSignaturesByMask.Add(passMask, projectedSignatures);
                return projectedSignatures;
            }

            public ulong GetProjectedUsedFeatureMask(ulong passMask)
            {
                ulong usedFeatureMask = 0UL;
                foreach (var signature in GetProjectedSignatures(passMask))
                {
                    usedFeatureMask |= signature;
                }

                return usedFeatureMask;
            }

            public List<string> GetUnusedFeatures()
            {
                var allFeatureMask = FeatureNames.Count >= 64 ? ulong.MaxValue : ((1UL << FeatureNames.Count) - 1UL);
                return ASPCharacterShaderFeatureUsage.CollectFeaturesFromMask(FeatureNames, allFeatureMask & ~UsedFeatureMask);
            }

            public List<string> CollectFeaturesFromMask(ulong mask)
            {
                return ASPCharacterShaderFeatureUsage.CollectFeaturesFromMask(FeatureNames, mask);
            }
        }

        public const string TargetShaderName = "ASP/Character";

        private const string TargetShaderAssetPath = "Assets/ASP/Shaders/ASPCharacter.shader";

        private static UsageData s_CachedUsage;

        public static void ClearCache()
        {
            s_CachedUsage = null;
        }

        [MenuItem("Tools/ASP/Shader Variants/Log ASPCharacter Material Usage")]
        public static void LogUsageReport()
        {
            Debug.Log(BuildUsageReport(forceRefresh: true));
        }

        public static void LogUsageReportBatchMode()
        {
            Debug.Log(BuildUsageReport(forceRefresh: true));
        }

        public static string BuildUsageReport(bool forceRefresh = false)
        {
            var usage = Analyze(forceRefresh);
            if (!usage.IsReady)
            {
                return "ASP/Character shader usage report could not be generated.";
            }

            var builder = new StringBuilder(2048);
            builder.AppendLine("ASP/Character Shader Usage");
            builder.AppendLine($"Declared local shader features: {usage.FeatureNames.Count}");
            builder.AppendLine($"Materials using shader: {usage.MaterialCount}");
            builder.AppendLine($"Unique feature combinations: {usage.SignatureCount}");
            builder.AppendLine();

            builder.AppendLine("Used features:");
            var usedFeatures = CollectFeaturesFromMask(usage.FeatureNames, usage.UsedFeatureMask);
            if (usedFeatures.Count == 0)
            {
                builder.AppendLine("  (none)");
            }
            else
            {
                for (var index = 0; index < usedFeatures.Count; index++)
                {
                    builder.Append("  ");
                    builder.AppendLine(usedFeatures[index]);
                }
            }

            builder.AppendLine();
            builder.AppendLine("Unused features:");
            var unusedFeatures = usage.GetUnusedFeatures();
            if (unusedFeatures.Count == 0)
            {
                builder.AppendLine("  (none)");
            }
            else
            {
                for (var index = 0; index < unusedFeatures.Count; index++)
                {
                    builder.Append("  ");
                    builder.AppendLine(unusedFeatures[index]);
                }
            }

            builder.AppendLine();
            builder.AppendLine("Combinations:");

            var combinations = new List<KeyValuePair<ulong, List<string>>>(usage.MaterialPathsBySignature);
            combinations.Sort((left, right) =>
            {
                var materialCountCompare = right.Value.Count.CompareTo(left.Value.Count);
                if (materialCountCompare != 0)
                {
                    return materialCountCompare;
                }

                return left.Key.CompareTo(right.Key);
            });

            for (var comboIndex = 0; comboIndex < combinations.Count; comboIndex++)
            {
                var combination = combinations[comboIndex];
                builder.Append(comboIndex + 1);
                builder.Append(". ");
                builder.Append(combination.Value.Count);
                builder.Append(" material(s): ");

                var enabledFeatures = CollectFeaturesFromMask(usage.FeatureNames, combination.Key);
                if (enabledFeatures.Count == 0)
                {
                    builder.AppendLine("(no local keywords enabled)");
                }
                else
                {
                    builder.AppendLine(string.Join(", ", enabledFeatures));
                }

                for (var materialIndex = 0; materialIndex < combination.Value.Count; materialIndex++)
                {
                    builder.Append("   - ");
                    builder.AppendLine(combination.Value[materialIndex]);
                }
            }

            return builder.ToString().TrimEnd();
        }

        internal static List<string> GetUnusedFeatures(bool forceRefresh = false)
        {
            var usage = Analyze(forceRefresh);
            return usage.IsReady ? usage.GetUnusedFeatures() : new List<string>();
        }

        internal static UsageData Analyze(bool forceRefresh = false)
        {
            if (!forceRefresh && s_CachedUsage != null)
            {
                return s_CachedUsage;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(TargetShaderAssetPath);
            if (shader == null)
            {
                s_CachedUsage = UsageData.Empty;
                return s_CachedUsage;
            }

            var featureNames = ParseShaderFeatureNames(TargetShaderAssetPath);
            if (featureNames.Count == 0)
            {
                s_CachedUsage = UsageData.Empty;
                return s_CachedUsage;
            }

            var featureIndexByName = new Dictionary<string, int>(featureNames.Count, StringComparer.Ordinal);
            for (var featureIndex = 0; featureIndex < featureNames.Count; featureIndex++)
            {
                featureIndexByName.Add(featureNames[featureIndex], featureIndex);
            }

            var materialSignatures = new HashSet<ulong>();
            var materialPathsBySignature = new Dictionary<ulong, List<string>>();
            ulong usedFeatureMask = 0UL;

            var materialGuids = AssetDatabase.FindAssets("t:Material");
            for (var materialGuidIndex = 0; materialGuidIndex < materialGuids.Length; materialGuidIndex++)
            {
                var materialPath = AssetDatabase.GUIDToAssetPath(materialGuids[materialGuidIndex]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (!string.Equals(material.shader.name, TargetShaderName, StringComparison.Ordinal))
                {
                    continue;
                }

                var signature = ComputeMaterialSignature(material, featureNames, featureIndexByName);
                materialSignatures.Add(signature);
                usedFeatureMask |= signature;

                if (!materialPathsBySignature.TryGetValue(signature, out var materialPaths))
                {
                    materialPaths = new List<string>();
                    materialPathsBySignature.Add(signature, materialPaths);
                }

                materialPaths.Add(materialPath);
            }

            foreach (var entry in materialPathsBySignature)
            {
                entry.Value.Sort(StringComparer.Ordinal);
            }

            s_CachedUsage = new UsageData(
                true,
                featureNames,
                materialSignatures,
                materialPathsBySignature,
                usedFeatureMask);
            return s_CachedUsage;
        }

        private static ulong ComputeMaterialSignature(
            Material material,
            List<string> featureNames,
            Dictionary<string, int> featureIndexByName)
        {
            var signature = 0UL;
            var styleMode = ResolveStyleMode(material);

            for (var featureIndex = 0; featureIndex < featureNames.Count; featureIndex++)
            {
                if (!IsFeatureEnabled(material, featureNames[featureIndex], styleMode))
                {
                    continue;
                }

                signature |= 1UL << featureIndex;
            }

            if (styleMode == StyleMode.Unknown)
            {
                EnableKeywordIfPresent(material, featureIndexByName, ref signature, "_STYLE_STYLIZEPBR");
                EnableKeywordIfPresent(material, featureIndexByName, ref signature, "_STYLE_CELSHADING");
                EnableKeywordIfPresent(material, featureIndexByName, ref signature, "_STYLE_FACE");
            }

            return signature;
        }

        private static StyleMode ResolveStyleMode(Material material)
        {
            if (material.HasProperty("_style"))
            {
                var roundedStyle = Mathf.RoundToInt(material.GetFloat("_style"));
                if (roundedStyle >= 0 && roundedStyle <= 2)
                {
                    return (StyleMode)roundedStyle;
                }
            }

            if (material.IsKeywordEnabled("_STYLE_STYLIZEPBR"))
            {
                return StyleMode.StylizePbr;
            }

            if (material.IsKeywordEnabled("_STYLE_CELSHADING"))
            {
                return StyleMode.CelShading;
            }

            if (material.IsKeywordEnabled("_STYLE_FACE"))
            {
                return StyleMode.Face;
            }

            return StyleMode.Unknown;
        }

        private static bool IsFeatureEnabled(Material material, string featureName, StyleMode styleMode)
        {
            switch (featureName)
            {
                case "_STYLE_STYLIZEPBR":
                    return styleMode == StyleMode.StylizePbr;
                case "_STYLE_CELSHADING":
                    return styleMode == StyleMode.CelShading;
                case "_STYLE_FACE":
                    return styleMode == StyleMode.Face;
                case "_NORMALMAP":
                    return HasTexture(material, "_BumpMap") || material.IsKeywordEnabled(featureName);
                case "_METALLICSPECGLOSSMAP":
                    return ((styleMode == StyleMode.StylizePbr && HasTexture(material, "_OSMTexture")) ||
                            material.IsKeywordEnabled(featureName));
                case "_RAMP_OCCLUSION_MAP":
                    return ((styleMode != StyleMode.StylizePbr && styleMode != StyleMode.Unknown &&
                             HasTexture(material, "_RampOcclusionMap")) ||
                            material.IsKeywordEnabled(featureName));
                case "_FACESHADOW":
                    return ((styleMode != StyleMode.StylizePbr && styleMode != StyleMode.Unknown &&
                             HasTexture(material, "_FaceShadowMap")) ||
                            material.IsKeywordEnabled(featureName));
                case "_HAIRMAP":
                    return ((styleMode == StyleMode.CelShading && HasTexture(material, "_HairHighlightMaskMap")) ||
                            material.IsKeywordEnabled(featureName));
                case "_SSSMAP":
                    return ((styleMode != StyleMode.StylizePbr && styleMode != StyleMode.Unknown &&
                             HasTexture(material, "_SSSRampMap") &&
                             HasEnabledToggle(material, "_EnableSSSRamp")) ||
                            material.IsKeywordEnabled(featureName));
                case "_MATCAP_HIGHLIGHT_MAP":
                    return HasTexture(material, "_MatCapReflectionMap") || material.IsKeywordEnabled(featureName);
                case "_RECEIVE_ASP_SHADOW":
                    return HasEnabledToggle(material, "_ReceiveASPShadow") || material.IsKeywordEnabled(featureName);
                case "_RECEIVE_OFFSETED_SHADOW_ON":
                    return HasEnabledToggle(material, "_ReceiveOffsetedDepthMap") || material.IsKeywordEnabled(featureName);
                case "_SURFACE_TYPE_TRANSPARENT":
                    return HasFloatAtLeast(material, "_SurfaceType", 1f) || material.IsKeywordEnabled(featureName);
                case "_ALPHATEST_ON":
                    return HasEnabledToggle(material, "_AlphaClip") || material.IsKeywordEnabled(featureName);
                case "_SPECULAR_LIGHTING_ON":
                    return HasEnabledToggle(material, "_SpecularHighlights") || material.IsKeywordEnabled(featureName);
                case "_RIMLIGHTING_ON":
                    return HasEnabledToggle(material, "_RimLightOn") || material.IsKeywordEnabled(featureName);
                case "_DEPTH_RIMLIGHTING_ON":
                    return HasEnabledToggle(material, "_DepthRimLightOn") || material.IsKeywordEnabled(featureName);
                case "_ENVIRONMENTREFLECTIONS_OFF":
                    return HasPropertyDisabled(material, "_EnvironmentReflections") || material.IsKeywordEnabled(featureName);
                case "_EMISSION":
                    return HasEnabledToggle(material, "_EmissionToggle") || material.IsKeywordEnabled(featureName);
                case "_RECEIVE_SHADOWS_OFF":
                    return HasPropertyDisabled(material, "_ReceiveShadows") || material.IsKeywordEnabled(featureName);
                case "_USE_BAKED_NORAML":
                    return material.IsKeywordEnabled(featureName);
                case "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A":
                    return HasFloatAtLeast(material, "_SmoothnessTextureChannel", 1f) || material.IsKeywordEnabled(featureName);
                case "_ADD_PRECOMPUTED_VELOCITY":
                    return HasEnabledToggle(material, "_AddPrecomputedVelocity") || material.IsKeywordEnabled(featureName);
                default:
                    return material.IsKeywordEnabled(featureName);
            }
        }

        private static void EnableKeywordIfPresent(
            Material material,
            Dictionary<string, int> featureIndexByName,
            ref ulong signature,
            string keywordName)
        {
            if (!material.IsKeywordEnabled(keywordName))
            {
                return;
            }

            if (!featureIndexByName.TryGetValue(keywordName, out var featureIndex))
            {
                return;
            }

            signature |= 1UL << featureIndex;
        }

        private static bool HasTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetTexture(propertyName) != null;
        }

        private static bool HasEnabledToggle(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
        }

        private static bool HasFloatAtLeast(Material material, string propertyName, float minimum)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) >= minimum;
        }

        private static bool HasPropertyDisabled(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) <= 0.5f;
        }

        private static List<string> ParseShaderFeatureNames(string shaderAssetPath)
        {
            var featureNames = new List<string>();
            var featureNameSet = new HashSet<string>(StringComparer.Ordinal);
            var shaderFilePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, shaderAssetPath);

            foreach (var rawLine in File.ReadAllLines(shaderFilePath))
            {
                var line = rawLine;
                var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex);
                }

                line = line.Trim();
                if (!line.StartsWith("#pragma shader_feature_local", StringComparison.Ordinal))
                {
                    continue;
                }

                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                for (var tokenIndex = 2; tokenIndex < tokens.Length; tokenIndex++)
                {
                    var token = tokens[tokenIndex];
                    if (token == "_" || token.Length == 0 || token[0] != '_')
                    {
                        continue;
                    }

                    if (!featureNameSet.Add(token))
                    {
                        continue;
                    }

                    featureNames.Add(token);
                }
            }

            return featureNames;
        }

        internal static List<string> CollectFeaturesFromMask(List<string> featureNames, ulong mask)
        {
            var features = new List<string>();
            for (var featureIndex = 0; featureIndex < featureNames.Count; featureIndex++)
            {
                var featureBit = 1UL << featureIndex;
                if ((mask & featureBit) == 0)
                {
                    continue;
                }

                features.Add(featureNames[featureIndex]);
            }

            return features;
        }
    }
}
