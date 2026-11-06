// Quemadura sobre la piel de la mano (BurnSkinOverlay). Antes era un color
// parejo sobre toda la mano ("guante rojo"). Ahora cada vertice trae una
// mascara (alfa del color del vertice): alta en el dorso, donde cayo el
// agua, y baja hacia la palma, la muneca y las puntas de los dedos, con
// bordes irregulares. El canal rojo marca el centro de la quemadura.
//   _Spread: cuanto de la mascara se ve (0 nada, 1 toda). Crece mientras
//            la quemadura no se enfria: el enrojecimiento se extiende.
//   _Core:   cuanto se nota el centro mas profundo (segundo grado).
//   _Color:  tinte y opacidad de la etapa (lo pone BurnSkinOverlay).
Shader "Hito1/PielQuemada"
{
    Properties
    {
        _Color ("Tinte de la etapa", Color) = (1, 0.55, 0.5, 0.9)
        _MainTex ("Detalle de la piel", 2D) = "white" {}
        _CoreColor ("Centro de la quemadura", Color) = (0.72, 0.16, 0.14, 1)
        _Spread ("Extension (0 a 1)", Range(0, 1)) = 0.6
        _Core ("Centro (0 a 1)", Range(0, 1)) = 0.5
        _Softness ("Borde difuso", Range(0.02, 0.5)) = 0.3
        _Glossiness ("Brillo humedo", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _CoreColor;
        half _Spread, _Core, _Softness, _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half mask = IN.color.a;
            // Umbral que baja al crecer _Spread: primero se ve el centro y
            // despues se extiende hacia los bordes.
            half edge = 1.0 - _Spread;
            half visible = smoothstep(edge, edge + _Softness, mask);

            fixed4 detail = tex2D(_MainTex, IN.uv_MainTex);
            half core = saturate(IN.color.r * _Core);
            // Rosado palido en el borde, el tinte de la etapa hacia adentro y
            // el centro mas profundo.
            fixed3 edgeTint = lerp(fixed3(1, 0.8, 0.74), _Color.rgb, 0.45);
            fixed3 tint = lerp(edgeTint, _Color.rgb, saturate(mask * 1.4)) * detail.rgb;
            o.Albedo = lerp(tint, _CoreColor.rgb * detail.rgb, core);
            o.Smoothness = _Glossiness * (0.45 + 0.55 * core);
            o.Metallic = 0;
            // Deja ver la piel: el borde apenas tinto y el centro mas cubierto.
            o.Alpha = _Color.a * visible * lerp(0.25, 0.85, saturate(mask * 1.1 + core * 0.3));
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
