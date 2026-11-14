// Panel redondeado para las tarjetas de la guia: relleno con degradado
// vertical, borde con degradado de izquierda a derecha y un brillo suave
// por fuera. Sin luces ni texturas (barato en el visor). El quad cubre el
// panel mas el margen del brillo; _Size es ese tamano total en metros.
Shader "Hito1/PanelRedondeado"
{
    Properties
    {
        _Color ("Relleno arriba", Color) = (0.09, 0.12, 0.2, 0.92)
        _Color2 ("Relleno abajo", Color) = (0.05, 0.07, 0.13, 0.92)
        _BorderA ("Borde izquierda", Color) = (0.4, 0.78, 1, 1)
        _BorderB ("Borde derecha", Color) = (0.7, 0.5, 1, 1)
        _Size ("Tamano total (m)", Vector) = (0.5, 0.3, 0, 0)
        _Radius ("Radio de esquinas (m)", Float) = 0.022
        _Border ("Grosor del borde (m)", Float) = 0.002
        _Glow ("Margen del brillo (m)", Float) = 0.012
        _GlowStrength ("Intensidad del brillo", Float) = 0.45
        _Sheen ("Reflejo superior", Float) = 0.07
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _Color2, _BorderA, _BorderB;
            float4 _Size;
            float _Radius, _Border, _Glow, _GlowStrength, _Sheen;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float RoundBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_Size.xy, 0.001);
                float2 p = (i.uv - 0.5) * size;
                float2 halfSize = max(size * 0.5 - _Glow, 0.0005);
                float r = min(_Radius, min(halfSize.x, halfSize.y));
                float d = RoundBox(p, halfSize, r);
                float aa = max(fwidth(d), 1e-5);

                float inside = 1.0 - smoothstep(-aa, aa, d);
                float panelT = saturate((p.y + halfSize.y) / (2.0 * halfSize.y));
                fixed4 fill = lerp(_Color2, _Color, panelT);
                fill.rgb += _Sheen * pow(panelT, 6.0);

                float edge = smoothstep(-_Border - aa, -_Border + aa, d);
                fixed4 border = lerp(_BorderA, _BorderB, saturate(i.uv.x));
                border.a *= _Border > 0.00001 ? 1.0 : 0.0;

                fixed3 rgb = lerp(fill.rgb, border.rgb, edge * border.a);
                float a = lerp(fill.a, max(fill.a, border.a), edge * border.a) * inside;

                float g = (d > 0.0 && _Glow > 0.00001) ? pow(saturate(1.0 - d / _Glow), 2.0) * _GlowStrength : 0.0;
                float outA = saturate(a + g * (1.0 - a));
                rgb = (rgb * a + border.rgb * g * (1.0 - a)) / max(outA, 1e-4);
                return fixed4(rgb, outA);
            }
            ENDCG
        }
    }
}
