Shader "Custom/Shader_ARM_SSS"
{
    Properties
    {
        _MainTex ("Base Color (Albedo)", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)

        _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Strength", Range(0, 2)) = 1

        _ARMMap ("ARM Map (R:AO, G:Roughness, B:Metallic)", 2D) = "black" {}

        _RoughnessMap ("Extra Roughness Map", 2D) = "white" {}
        _RoughnessScale ("Roughness Scale", Range(0,1)) = 1
        _AOScale ("AO Scale", Range(0,1)) = 1
        _MetallicScale ("Metallic Scale", Range(0,1)) = 1

        [Toggle(_USE_SSS)] _UseSSS ("Enable SSS (Fake)", Float) = 0
        _SSSMap ("SSS Map (Emission)", 2D) = "black" {}
        _SSSColor ("SSS Color", Color) = (1, 0.3, 0.2, 1)
        _SSSScale ("SSS Intensity", Range(0, 5)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature _USE_SSS

        sampler2D _MainTex;
        float4 _Color;

        sampler2D _BumpMap;
        float _NormalScale;

        sampler2D _ARMMap;
        sampler2D _RoughnessMap;

        float _RoughnessScale;
        float _AOScale;
        float _MetallicScale;

        sampler2D _SSSMap;
        float4 _SSSColor;
        float _SSSScale;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_ARMMap;
            float2 uv_RoughnessMap;
            float2 uv_SSSMap;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 1. Albedo
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = baseColor.rgb;

            // 2. Normal Map with strength
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Normal = normalize(lerp(float3(0,0,1), n, _NormalScale));

            // 3. ARM Map
            float3 arm = tex2D(_ARMMap, IN.uv_ARMMap).rgb;
            float ao = arm.r * _AOScale;
            float roughness = arm.g;
            float metallic = arm.b * _MetallicScale;

            // 4. Roughness Map 叠加
            float roughTex = tex2D(_RoughnessMap, IN.uv_RoughnessMap).r;
            float finalRoughness = lerp(roughness, roughTex, 0.5) * _RoughnessScale;
            o.Smoothness = 1.0 - saturate(finalRoughness);

            o.Metallic = saturate(metallic);
            o.Occlusion = saturate(ao);

        #ifdef _USE_SSS
            float3 sssMask = tex2D(_SSSMap, IN.uv_SSSMap).rgb;
            o.Emission = _SSSColor.rgb * sssMask * _SSSScale;
        #endif
        }
        ENDCG
    }

    FallBack "Standard"
}
