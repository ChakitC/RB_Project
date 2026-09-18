using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZLZ.Hub;

namespace ZLZ.AnimeShader
{
    // ─────────────────────────────────────────────────────────────────────────
    //  What the Hub knows about ZLZ Anime Shader.
    //
    //  Everything package-specific lives here; the Hub itself stays generic, so
    //  ZLZ Env Shader only has to ship its own copy of this file to appear in the
    //  same window with its own matrix and its own checklist.
    // ─────────────────────────────────────────────────────────────────────────
    public class ZLZ_AnimeHubProduct : ZLZ_HubProduct
    {
        public override string Id          => "zlz.anime";
        public override string DisplayName => "ZLZ Anime Shader";
        public override string Tagline     => "Stylised character shading, outlines, face shadow";
        public override int    SortOrder   => 10;

        public override string BannerTextureName => "ZLZ_Banner_Anime";

        // The shipped key art is the 3:2 Asset Store image, so the roughly 4:1 hero
        // can only show a band of it. Cropping to the top keeps the ZLZ logo but
        // slices the character off at the forehead; this band keeps the eyes, the
        // smile and the OFF/ON comparison instead. The window prints the product
        // name in text right underneath, so the logo is the part we can afford to
        // lose - the shading is not.
        public override float BannerFocus => 0.6f;
        public override string StoreUrl     => ZLZ_HubUrls.AnimeStore;
        public override string WebsiteUrl   => ZLZ_HubUrls.Website;
        public override string SupportEmail => ZLZ_HubUrls.SupportEmail;

        // ── Renderer features ─────────────────────────────────────────────────
        //
        // AssetName matches what ZLZ_CharacterDashboard already creates, so a
        // project set up through the old dashboard and one set up through the Hub
        // end up with identically named sub-assets rather than two conventions.
        public override IEnumerable<ZLZ_HubFeature> GetFeatures()
        {
            yield return new ZLZ_HubFeature
            {
                DisplayName = "Hull outline",
                FeatureType = typeof(ZLZ_OutlineRendererFeature),
                AssetName   = "ZLZ Hull Outline",
                Tooltip     = "Per-object inverted hull outline.",
            };
            yield return new ZLZ_HubFeature
            {
                DisplayName   = "Screen space outline",
                FeatureType   = typeof(ZLZ_ScreenSpaceOutlineFeature),
                AssetName     = "ZLZ Screen Space Outline",
                Tooltip       = "Interior detail lines. Installed switched off - turn it on per project.",
                DefaultActive = false,
                // characterLayers defaults to Nothing, so a feature switched on by hand
                // would draw an empty pass. Same default the Character Dashboard applies.
                Configure     = f => MergeLayerMask(f, "characterLayers", 1 << 0),
            };
            yield return new ZLZ_HubFeature
            {
                DisplayName = "Contact shadow",
                FeatureType = typeof(ZLZ_CharacterContactShadowFeature),
                AssetName   = "ZLZ Character Contact Shadow",
                // Installed switched ON, unlike the other two opt-in features. The
                // shipped materials ask for it : 38 of the 50 demo and preset
                // materials set _UseContactShadow, so leaving the feature off means
                // the package's own content renders wrong out of the box with nothing
                // on screen to explain why.
                Tooltip     = "Needs 'Contact Self-Shadow' on the character materials, which the shipped ones already have.",
            };
            yield return new ZLZ_HubFeature
            {
                DisplayName = "Tone mapping",
                FeatureType = typeof(ZLZ_AnimeToneMappingFeature),
                AssetName   = "ZLZ_AnimeToneMappingFeature",
                Tooltip     = "Anime tone mapping. Wants HDR and HDR colour grading.",
            };
            yield return new ZLZ_HubFeature
            {
                DisplayName   = "Selection outline",
                FeatureType   = typeof(ZLZ_SelectionOutlineFeature),
                AssetName     = "ZLZ Selection Outline",
                Tooltip       = "Costs three passes whenever it runs, so it is installed switched off.",
                DefaultActive = false,
            };
        }

        // Mirrors ZLZ_CharacterDashboard : OR the layer in rather than overwrite, so a
        // customer who already narrowed the mask keeps their choice.
        static void MergeLayerMask(ScriptableRendererFeature feature, string propertyName, int mask)
        {
            var so   = new SerializedObject(feature);
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;

            prop.intValue |= mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Project checks ────────────────────────────────────────────────────

        public override IEnumerable<ZLZ_HubCheck> GetChecks()
        {
            yield return new ZLZ_HubCheck
            {
                Title       = "Linear colour space",
                Description = "Toon ramps and tone mapping are authored for linear lighting. " +
                              "In gamma the shading bands land in the wrong place.",
                Evaluate    = () => PlayerSettings.colorSpace == ColorSpace.Linear
                                    ? ZLZ_CheckStatus.Ok : ZLZ_CheckStatus.Error,
                FixSummary  = () => "Switch the project to Linear colour space.\n\n" +
                                    "Unity will reimport shaders and textures, which can take a while on a large project.",
                Fix         = () => PlayerSettings.colorSpace = ColorSpace.Linear,
            };

            yield return new ZLZ_HubCheck
            {
                Title       = "HDR enabled",
                Description = "Tone mapping needs values above 1.0 to work with.",
                Targets     = () => BoolTargets("m_SupportsHDR"),
                FixSummary  = () => Summary("Enable HDR on:", AssetsWithBoolOff("m_SupportsHDR")),
                Fix         = () => SetBoolOnAll("m_SupportsHDR", true),
            };

            yield return new ZLZ_HubCheck
            {
                Title       = "HDR colour grading",
                Description = "Colour grading in LDR clips the tone mapping curve. " +
                              "Only matters where the tone mapping feature is in use.",
                Targets     = () => GradingTargets(),
                FixSummary  = () => Summary("Set colour grading to HDR on:",
                                            AssetsWithGradingLdr().Select(a => AssetDatabase.GetAssetPath(a)).ToList()),
                Fix         = () =>
                {
                    foreach (var a in AssetsWithGradingLdr())
                    {
                        var so = new SerializedObject(a);
                        var p  = so.FindProperty("m_ColorGradingMode");
                        if (p == null) continue;
                        p.intValue = (int)ColorGradingMode.HighDynamicRange;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(a);
                    }
                },
            };

            yield return new ZLZ_HubCheck
            {
                Title       = "Depth priming off",
                Description = "Depth priming makes URP draw opaques with an equal-depth test. " +
                              "Custom outline passes are the usual casualty, so ZLZ ships expecting it disabled.",
                Targets     = () => PrimingTargets(),
                FixSummary  = () => Summary("Disable depth priming on:",
                                            RenderersWithPriming().Select(rd => AssetDatabase.GetAssetPath(rd)).ToList()),
                Fix         = () =>
                {
                    foreach (var rd in RenderersWithPriming())
                    {
                        var so = new SerializedObject(rd);
                        var p  = so.FindProperty("m_DepthPrimingMode");
                        if (p == null) continue;
                        p.intValue = 0;   // DepthPrimingMode.Disabled
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rd);
                    }
                },
            };
        }

        // ── Check helpers ─────────────────────────────────────────────────────

        // Goes through the Hub's scanner rather than calling FindAssets again, so
        // the "skip read-only package assets" rule lives in exactly one place.
        static List<UniversalRenderPipelineAsset> PipelineAssets() => ZLZ_HubUrp.ScanPipelineAssets();

        // ── Per-asset breakdowns ──────────────────────────────────────────────
        // Each check names the files it looked at, so "HDR is off" always says which
        // quality asset it is off on rather than leaving the customer to go hunting.

        static ZLZ_HubCheckTarget Target(Object asset, ZLZ_CheckStatus status)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return new ZLZ_HubCheckTarget
            {
                Name   = System.IO.Path.GetFileNameWithoutExtension(path),
                Path   = path,
                Status = status,
            };
        }

        static List<ZLZ_HubCheckTarget> BoolTargets(string property)
        {
            var list = new List<ZLZ_HubCheckTarget>();
            foreach (var a in PipelineAssets())
            {
                var p = new SerializedObject(a).FindProperty(property);
                bool ok = p == null || p.boolValue;
                list.Add(Target(a, ok ? ZLZ_CheckStatus.Ok : ZLZ_CheckStatus.Warning));
            }
            return list;
        }

        static List<ZLZ_HubCheckTarget> GradingTargets()
        {
            var list = new List<ZLZ_HubCheckTarget>();
            foreach (var a in PipelineAssets())
            {
                var p = new SerializedObject(a).FindProperty("m_ColorGradingMode");
                bool ok = p == null || p.intValue == (int)ColorGradingMode.HighDynamicRange;
                list.Add(Target(a, ok ? ZLZ_CheckStatus.Ok : ZLZ_CheckStatus.Warning));
            }
            return list;
        }

        static List<ZLZ_HubCheckTarget> PrimingTargets()
        {
            var list = new List<ZLZ_HubCheckTarget>();
            foreach (var entry in ZLZ_HubUrp.ScanRenderers())
            {
                var p = new SerializedObject(entry.Data).FindProperty("m_DepthPrimingMode");
                bool ok = p == null || p.intValue == 0;
                list.Add(Target(entry.Data, ok ? ZLZ_CheckStatus.Ok : ZLZ_CheckStatus.Warning));
            }
            return list;
        }

        static List<string> AssetsWithBoolOff(string property)
        {
            var paths = new List<string>();
            foreach (var a in PipelineAssets())
            {
                var p = new SerializedObject(a).FindProperty(property);
                if (p != null && !p.boolValue) paths.Add(AssetDatabase.GetAssetPath(a));
            }
            return paths;
        }

        static void SetBoolOnAll(string property, bool value)
        {
            foreach (var a in PipelineAssets())
            {
                var so = new SerializedObject(a);
                var p  = so.FindProperty(property);
                if (p == null || p.boolValue == value) continue;
                p.boolValue = value;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(a);
            }
        }

        static List<UniversalRenderPipelineAsset> AssetsWithGradingLdr()
        {
            var hits = new List<UniversalRenderPipelineAsset>();
            foreach (var a in PipelineAssets())
            {
                var p = new SerializedObject(a).FindProperty("m_ColorGradingMode");
                if (p != null && p.intValue != (int)ColorGradingMode.HighDynamicRange) hits.Add(a);
            }
            return hits;
        }

        static List<UniversalRendererData> RenderersWithPriming()
        {
            var hits = new List<UniversalRendererData>();
            foreach (var entry in ZLZ_HubUrp.ScanRenderers())
            {
                var p = new SerializedObject(entry.Data).FindProperty("m_DepthPrimingMode");
                if (p != null && p.intValue != 0) hits.Add(entry.Data);
            }
            return hits;
        }

        // Never change a customer's render settings without naming the files first.
        // Locked assets under Perforce or Plastic fail silently otherwise.
        static string Summary(string header, List<string> paths)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine();
            foreach (var p in paths) sb.AppendLine("  " + p);
            return sb.ToString();
        }
    }
}
