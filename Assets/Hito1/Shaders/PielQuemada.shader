// Capa de piel quemada sobre la mano (BurnSkinOverlay). Igual que el
// Standard en modo Fade, pero usa el color de cada vertice como mascara:
// alfa = cuanto se ve la quemadura en ese punto (borde difuso y manchas) y
// rojo = intensidad del centro (mas oscuro y mas brillante, como la piel
// humeda donde salen las ampollas).
Shader "Hito1/PielQuemada"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.5, 0.9)
        _MainTex ("Textura", 2D) = "white" {}
        _Glossiness ("Brillo", Range(0, 1)) = 0.3
        _CoreGloss ("Brillo del centro", Range(0, 1)) = 0.75
        _CoreDarken ("Oscurecer el centro", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _CoreGloss;
        half _CoreDarken;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half core = IN.color.r;
            o.Albedo = c.rgb * lerp(1.0, 1.0 - _CoreDarken, core);
            o.Smoothness = lerp(_Glossiness, _CoreGloss, core);
            o.Metallic = 0;
            o.Alpha = c.a * IN.color.a;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
