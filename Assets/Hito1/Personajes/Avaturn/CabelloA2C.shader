// Pelo, pestanas y cejas de la tia de Avaturn. Usa "alpha to coverage":
// con el antialiasing del Quest (MSAA) los bordes de cada mechon quedan
// suaves en vez de pixelados, sin los problemas de orden de lo
// transparente. Sin MSAA se ve como un recorte normal.
Shader "Hito1/CabelloA2C"
{
    Properties
    {
        _Color ("Tinte", Color) = (1, 1, 1, 1)
        _MainTex ("Color (A = opacidad)", 2D) = "white" {}
        [Normal] _BumpMap ("Normales", 2D) = "bump" {}
        _Cutoff ("Corte", Range(0, 1)) = 0.5
        _Glossiness ("Suavidad", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        AlphaToMask On

        CGPROGRAM
        #pragma surface surf Standard keepalpha addshadow fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Cutoff;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.z *= IN.facing > 0 ? 1 : -1;
            o.Normal = n;

        #ifdef UNITY_PASS_SHADOWCASTER
            clip(c.a - _Cutoff);
            o.Alpha = 1;
        #else
            // Borde nitido de un pixel alrededor del corte.
            o.Alpha = saturate((c.a - _Cutoff) / max(fwidth(c.a), 0.0001) + 0.5);
        #endif
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
