Shader "Custom/Shader_ARM"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("BaseColor Texture", 2D) = "white" {}

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1

        _ARMMap ("ARM Map (R=AO, G=Roughness, B=Metal)", 2D) = "white" {}
        _RoughnessTex ("Extra Roughness Texture", 2D) = "white" {}
        
        _RoughnessStrength ("Roughness Strength", Range(0, 10)) = 1
        _MetallicStrength ("Metallic Strength", Range(0, 1)) = 1
        _AOStrength ("AO Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _BaseColor;

        sampler2D _NormalMap;
        float _NormalStrength;

        sampler2D _ARMMap;
        sampler2D _RoughnessTex;

        float _RoughnessStrength;
        float _MetallicStrength;
        float _AOStrength;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float2 uv_ARMMap;
            float2 uv_RoughnessTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Base Color
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex) * _BaseColor;
            o.Albedo = baseColor.rgb;

            // Normal
            float3 normalTex = UnpackNormal(tex2D(_NormalMap, IN.uv_NormalMap));
            o.Normal = normalize(lerp(float3(0, 0, 1), normalTex, _NormalStrength));

            // ARM Map
            float3 arm = tex2D(_ARMMap, IN.uv_ARMMap).rgb;
            float ao = arm.r;
            float roughness = arm.g;
            float metallic = arm.b;

            // Extra roughness
            float rough2 = tex2D(_RoughnessTex, IN.uv_RoughnessTex).r;
            float combinedRoughness = lerp(roughness, rough2, 0.5) * _RoughnessStrength;

            o.Smoothness = 1.0 - saturate(combinedRoughness);
            o.Metallic = saturate(metallic * _MetallicStrength);
            o.Occlusion = saturate(ao * _AOStrength);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
