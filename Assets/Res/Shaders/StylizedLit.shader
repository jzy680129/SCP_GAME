Shader "Project/StylizedLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.78, 0.82, 0.88, 1)
        _ShadowColor ("Shadow Color", Color) = (0.30, 0.36, 0.48, 1)
        _HighlightColor ("Highlight Color", Color) = (1.00, 0.82, 0.55, 1)
        _RimColor ("Rim Color", Color) = (0.55, 0.78, 1.00, 1)
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.48
        _RampSoftness ("Ramp Softness", Range(0.001, 0.5)) = 0.08
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.28
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.28
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.18
        _SpecularPower ("Specular Power", Range(2, 96)) = 24
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _RampThreshold;
                half _RampSoftness;
                half _AmbientStrength;
                half _RimPower;
                half _RimIntensity;
                half _SpecularStrength;
                half _SpecularPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 viewDirWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half softness = max(_RampSoftness, 0.001);
                half litBand = smoothstep(_RampThreshold - softness, _RampThreshold + softness, ndotl);
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half band = saturate(litBand * attenuation);

                half3 ambient = SampleSH(normalWS) * albedo * _AmbientStrength;
                half3 shadowColor = _ShadowColor.rgb * albedo;
                half3 litColor = albedo * mainLight.color + ambient;
                half3 color = lerp(shadowColor, litColor, band);

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength * band;
                half rim = pow(saturate(1.0 - dot(normalWS, viewDirWS)), _RimPower) * _RimIntensity;

                color += _HighlightColor.rgb * spec;
                color += _RimColor.rgb * rim;
                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            fixed4 _ShadowColor;
            fixed4 _HighlightColor;
            fixed4 _RimColor;
            fixed _RampThreshold;
            fixed _RampSoftness;
            fixed _AmbientStrength;
            fixed _RimPower;
            fixed _RimIntensity;
            fixed _SpecularStrength;
            fixed _SpecularPower;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = UnityObjectToWorldNormal(input.normal);
                float3 positionWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.viewDirWS = UnityWorldSpaceViewDir(positionWS);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed3 normalWS = normalize(input.normalWS);
                fixed3 viewDirWS = normalize(input.viewDirWS);
                fixed3 lightDirWS = normalize(_WorldSpaceLightPos0.xyz);

                fixed ndotl = saturate(dot(normalWS, lightDirWS));
                fixed softness = max(_RampSoftness, 0.001);
                fixed band = smoothstep(_RampThreshold - softness, _RampThreshold + softness, ndotl);

                fixed3 albedo = tex2D(_BaseMap, input.uv).rgb * _BaseColor.rgb;
                fixed3 ambient = ShadeSH9(fixed4(normalWS, 1)) * albedo * _AmbientStrength;
                fixed3 shadowColor = _ShadowColor.rgb * albedo;
                fixed3 litColor = albedo * _LightColor0.rgb + ambient;
                fixed3 color = lerp(shadowColor, litColor, band);

                fixed3 halfDir = normalize(lightDirWS + viewDirWS);
                fixed spec = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength * band;
                fixed rim = pow(saturate(1.0 - dot(normalWS, viewDirWS)), _RimPower) * _RimIntensity;

                color += _HighlightColor.rgb * spec;
                color += _RimColor.rgb * rim;
                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct Varyings
            {
                V2F_SHADOW_CASTER;
            };

            Varyings Vert(appdata_base v)
            {
                Varyings o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack Off
}
