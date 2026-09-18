Shader "ZLZ/SelectionOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        [Range(0, 10)] _OutlineWidth ("Outline Width", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // ─────────────────────────────────────────────────────────────────
        // Pass 0 — Stencil Write
        // Marks every visible pixel of the character. No color output.
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ZLZSelectionStencil"

            Cull     Off
            ZWrite   Off
            ZTest    Always
            ColorMask 0

            Stencil
            {
                Ref       16        // 0x10 — bit 4
                WriteMask 240       // 0xF0 — only write bits 4-7, leave bits 0-3 (Hair Transparent) untouched
                Comp      Always
                Pass      Replace
            }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        // Pass 1 — Outline Draw
        // Inverted-hull (Cull Front) with world-space expansion via Nilo utility.
        // Smooth normals from UV7 (tangent space XY) give correct expansion at
        // all surface orientations, including flat/camera-facing surfaces and
        // orthographic cameras.
        // Stencil NotEqual masks interior — only the outer silhouette ring is drawn.
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ZLZSelectionOutline"

            Cull   Off
            ZWrite Off
            ZTest  LEqual

            Stencil
            {
                Ref      16
                ReadMask 240        // 0xF0 — read bits 4-7 only
                Comp     NotEqual
            }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            // Nilo utility: TransformPositionWSToOutlinePositionWS
            // handles perspective + orthographic, FOV correction, distance scaling.
            // Requires _OutlineWidth declared above.
            #include "../Features/ZLZ_NiloOutlineUtil.hlsl"

            struct Attributes
            {
                float4 positionOS     : POSITION;
                float3 normalOS       : NORMAL;
                float4 tangentOS      : TANGENT;
                float2 smoothNormalTS : TEXCOORD7;  // Baked smooth normals (XY tangent-space)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(i.normalOS, i.tangentOS);

                // Reconstruct smooth normal: XY from UV7, Z from unit-sphere formula.
                // Falls back to surface normal when UV7 is not baked (smoothNormalTS = 0,0 → Z = 1).
                float3 smoothNTS;
                smoothNTS.xy = i.smoothNormalTS;
                smoothNTS.z  = sqrt(1.0 - saturate(dot(i.smoothNormalTS, i.smoothNormalTS)));
                smoothNTS    = normalize(smoothNTS);

                float3x3 TBN = float3x3(
                    normalInput.tangentWS,
                    normalInput.bitangentWS,
                    normalInput.normalWS
                );
                float3 normalWS = normalize(mul(smoothNTS, TBN));

                // World-space inverted-hull expansion (Nilo).
                // Scales with FOV and distance — uniform visual thickness in both
                // perspective and orthographic cameras.
                float3 positionWS = TransformPositionWSToOutlinePositionWS(
                    vertexInput.positionWS,
                    vertexInput.positionVS.z,
                    normalWS
                );

                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
