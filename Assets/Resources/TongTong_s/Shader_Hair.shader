Shader "Custom/Shader_Hair"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Toggle(_USE_ALPHA)] _UseAlpha ("Use Transparency", Float) = 0
        _AlphaMask ("Alpha Mask", 2D) = "white" {}
        _AlphaStrength ("Alpha Strength", Range(0,1)) = 1

        _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        _ARMMap ("ARM Map (R=AO G=Rough B=Metal)", 2D) = "black" {}
        _RoughnessMap ("Extra Roughness Map", 2D) = "white" {}
        _RoughnessStrength ("Roughness Strength", Range(0,1)) = 1
        _AOStrength ("AO Strength", Range(0,1)) = 1
        _MetallicStrength ("Metallic Strength", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0
        #pragma shader_feature _USE_ALPHA

        sampler2D _MainTex;
        fixed4 _Color;

        sampler2D _AlphaMask;
        float _AlphaStrength;

        sampler2D _BumpMap;
        float _NormalStrength;

        sampler2D _ARMMap;
        sampler2D _RoughnessMap;
        float _RoughnessStrength;
        float _AOStrength;
        float _MetallicStrength;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_ARMMap;
            float2 uv_RoughnessMap;
            float2 uv_AlphaMask;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // BaseColor
            fixed4 col = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = col.rgb;

            // Alpha
        #ifdef _USE_ALPHA
            float alphaMask = tex2D(_AlphaMask, IN.uv_AlphaMask).r;
            o.Alpha = alphaMask * _AlphaStrength;
        #else
            o.Alpha = 1;
        #endif

            // Normal Map
            float3 normalTex = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Normal = normalize(lerp(float3(0,0,1), normalTex, _NormalStrength));

            // ARM Map
            float3 arm = tex2D(_ARMMap, IN.uv_ARMMap).rgb;
            float ao = arm.r * _AOStrength;
            float rough = arm.g;
            float metal = arm.b * _MetallicStrength;

            // Extra Roughness Map
            float extraRough = tex2D(_RoughnessMap, IN.uv_RoughnessMap).r;
            float combinedRough = lerp(rough, extraRough, 0.5) * _RoughnessStrength;

            o.Smoothness = 1.0 - saturate(combinedRough);
            o.Metallic = saturate(metal);
            o.Occlusion = saturate(ao);
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/VertexLit"
}
