using System;
using ASPUtil;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ASP
{
    public enum ScreenSpaceOutlineDebugMode : int
    {
            MaterialEdge = 0,
            AlbedoEdge = 1,
            DepthEdge = 2,
            NormalsEdge = 3,
            VertexColorMask = 4,
            CombinedResultAndVertexColorMask = 5,
            CombinedResult = 6,
    }
    [Serializable, VolumeComponentMenu("Post-processing/ASP Screen Space Outline")]
    public class ASPSreenSpaceOutline : VolumeComponent, IPostProcessComponent
    {
        [Header("General")]
        [FormerlySerializedAs("EnableOutlineEffect")] public BoolParameter EnableOutline = new BoolParameter(false, false);
        public ClampedFloatParameter OutlineWidth = new ClampedFloatParameter(1f, 0.5f, 5f);
        public ColorParameter OutlineColor = new ColorParameter(Color.black);
        [Tooltip("Apply outline on non-character pixels that have valid scene depth and normals.")]
        public BoolParameter EnableSceneObjectOutline = new BoolParameter(false);

        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Material Edge")]
        public BoolParameter EnableMaterialEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter MaterialEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);

        [Header("Albedo Edge")]
        public BoolParameter EnableAlbedoEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter AlbedoEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);

        [Header("Depth Edge")]
        public BoolParameter EnableDepthEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter DepthEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);

        [Header("Normals Edge")]
        public BoolParameter EnableNormalsEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter NormalsEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);
        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Distance Falloff")]
        public BoolParameter EnableDistanceFalloff = new BoolParameter(true);
        public FloatRangeParameter DistanceFalloffStartEnd = new FloatRangeParameter(new Vector2(2f,15f), 0.0f, 50f);
        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Debug")]
        public BoolParameter EnableDebugMode = new BoolParameter(false);
        public ColorParameter DebugBackground = new ColorParameter(Color.white);
        
        public ScreenSpaceOutlineDebugModeParameter ScreenSpaceOutlineDebugMode =
            new ScreenSpaceOutlineDebugModeParameter(ASP.ScreenSpaceOutlineDebugMode.CombinedResult);
        public ASPSreenSpaceOutline()
        {
            displayName = "Screen Space Character Outline";
        }
        
        public bool IsActive()
        {
            return EnableOutline.value;
        }


        /// <inheritdoc/>
        [Obsolete("Unused #from(2023.1)", false)]
        public bool IsTileCompatible() => false;
    }

    [Serializable]
    public sealed class ScreenSpaceOutlineDebugModeParameter : VolumeParameter<ScreenSpaceOutlineDebugMode>
    {
        
        public ScreenSpaceOutlineDebugModeParameter(ScreenSpaceOutlineDebugMode value, bool overrideState = false)
            : base(value, overrideState)
        {
            
        }
    }
}
