using ASPUtil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    public static partial class ASPRenderUtil
    {
        public static bool ShouldSkipCamera(CameraType cameraType)
        {
            return cameraType == CameraType.Preview || cameraType == CameraType.Reflection;
        }

        public static bool ShouldSkipScreenSpaceShadowPass(ref CameraData cameraData)
        {
#if UNITY_6000_0_OR_NEWER
            if (UniversalRenderer.IsOffscreenDepthTexture(ref cameraData))
                return true;
#else
            if (cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth)
                return true;
#endif

            return ShouldSkipCamera(cameraData.cameraType);
        }

        public static void DrawFullScreen(CommandBuffer cmd, Material material, int shaderPass)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
                cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
            else
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
        }

        public static void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
                cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
            else
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1);
        }

#if UNITY_6000_0_OR_NEWER
        public static void DrawFullScreen(RasterCommandBuffer cmd, Material material, int shaderPass)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
                cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
            else
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
        }

        public static void DrawTriangle(RasterCommandBuffer cmd, Material material, int shaderPass)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
                cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
            else
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1);
        }
#endif
    }

}
