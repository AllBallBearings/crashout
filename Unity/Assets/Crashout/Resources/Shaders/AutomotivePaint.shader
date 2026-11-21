Shader "Crashout/Automotive Paint"
{
    Properties
    {
        [MainColor] _BaseColor("Paint", Color) = (0.4,0.1,0.02,1)
        _Metallic("Metallic flake", Range(0,1)) = 0.65
        _Smoothness("Base smoothness", Range(0,1)) = 0.73
        _ClearCoatMask("Clear coat", Range(0,1)) = 1
        _ClearCoatSmoothness("Coat smoothness", Range(0,1)) = 0.94
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Metallic, _Smoothness, _ClearCoatMask, _ClearCoatSmoothness;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 damage : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half2 damage : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings PaintVertex(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS=p.positionCS; o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.uv=input.uv; o.damage=input.damage.a < .5 ? input.damage.rg : half2(0,0); o.fog=ComputeFogFactor(p.positionCS.z);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "AutomotiveForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PaintVertex
            #pragma fragment PaintFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define _CLEARCOAT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 PaintFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                InputData lighting=(InputData)0;
                lighting.positionWS=i.positionWS;
                lighting.positionCS=i.positionCS;
                lighting.normalWS=NormalizeNormalPerPixel(i.normalWS);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.vertexLighting=VertexLighting(i.positionWS,lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                // Damage travels with the actual vertices, including detached panels and replay.
                half stress=saturate(i.damage.r);
                float noise=frac(sin(dot(floor(i.uv*85),float2(12.9898,78.233)))*43758.5453);
                half scrape=smoothstep(.28,.82,stress+noise*.23);
                float grooves=abs(sin(i.uv.y*390+i.uv.x*17));
                half score=smoothstep(.76,1,grooves)*stress;
                SurfaceData s=(SurfaceData)0;
                s.albedo=lerp(_BaseColor.rgb,half3(.24,.26,.28),scrape);
                s.albedo*=1-score*.24;
                s.metallic=lerp(_Metallic,.86,scrape);
                s.smoothness=lerp(_Smoothness,.27,saturate(stress*.65+scrape*.45));
                s.normalTS=half3(0,0,1); s.occlusion=1; s.alpha=1;
                s.clearCoatMask=_ClearCoatMask*(1-saturate(stress*1.4));
                s.clearCoatSmoothness=_ClearCoatSmoothness;
                half4 color=UniversalFragmentPBR(lighting,s);
                color.rgb=MixFog(color.rgb,i.fog); return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            float4 ShadowVertex(Attributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(i.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 light=normalize(_LightPosition-p);
                #else
                    float3 light=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,light));
                #if UNITY_REVERSED_Z
                    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 DepthFragment() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PaintVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PaintVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
