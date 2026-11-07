// Quemadura por liquido caliente sobre la piel de la mano (capa de
// BurnSkinOverlay). La forma de la quemadura viene en los colores de los
// vertices, que arma BurnSkinPattern:
//   r: intensidad (1 en el centro, donde cayo el agua; 0 fuera)
//   g: zona de segundo grado (piel levantada, humeda y brillante)
//   b: valor al azar por vertice (manchas)
// Las texturas de ruido (_NoiseTex) y de relieve (_NormalTex) tambien se
// generan por codigo. El script cambia _Heat (rojo vivo -> rosado al
// enfriar), _Degree (0 primer grado, 1 segundo) y _Alpha.
Shader "Hito1/PielQuemada"
{
    Properties
    {
        _NoiseTex ("Ruido (r grande, g fino, b manchas)", 2D) = "gray" {}
        _NormalTex ("Relieve (rg = normal)", 2D) = "bump" {}
        _Heat ("Calor (0 enfriada, 1 recien quemada)", Range(0, 1.3)) = 1
        _Degree ("Grado (0 primero, 1 segundo)", Range(0, 1)) = 1
        _Alpha ("Visibilidad", Range(0, 1)) = 1
        _EdgeNoise ("Borde irregular", Range(0, 1)) = 0.45
        _BumpStrength ("Fuerza del relieve", Range(0, 2)) = 0.45
        _Erythema ("Eritema (borde)", Color) = (0.84, 0.46, 0.42, 1)
        _DeepRed ("Rojo intenso (centro)", Color) = (0.6, 0.12, 0.11, 1)
        _Cooled ("Rosado (enfriada)", Color) = (0.9, 0.58, 0.54, 1)
        _WoundBase ("Lecho humedo (segundo grado)", Color) = (0.66, 0.16, 0.15, 1)
        _Slough ("Piel levantada (segundo grado)", Color) = (0.9, 0.78, 0.72, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0

        sampler2D _NoiseTex;
        sampler2D _NormalTex;
        half _Heat;
        half _Degree;
        half _Alpha;
        half _EdgeNoise;
        half _BumpStrength;
        fixed4 _Erythema;
        fixed4 _DeepRed;
        fixed4 _Cooled;
        fixed4 _WoundBase;
        fixed4 _Slough;

        struct Input
        {
            float2 uv_NoiseTex;
            float4 burn;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.burn = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_NoiseTex;
            fixed4 big = tex2D(_NoiseTex, uv * 0.25);
            fixed4 fine = tex2D(_NoiseTex, uv * 0.8 + 0.37);

            // Borde irregular: el ruido grande corre el limite de la mancha.
            half mask = saturate(IN.burn.r + (big.r - 0.5) * _EdgeNoise);
            half edge = smoothstep(0.08, 0.42, mask);
            half core = smoothstep(0.45, 0.95, mask);

            // Primer grado: rojo que se intensifica hacia el centro, con
            // manchas (la piel no enrojece pareja).
            half heat = saturate(_Heat);
            half mottle = saturate(fine.b * 0.7 + IN.burn.b * 0.3);
            fixed3 hot = lerp(_Erythema.rgb, _DeepRed.rgb, saturate(core * 0.85 + (mottle - 0.5) * 0.5));
            fixed3 col = lerp(_Cooled.rgb, hot, heat);

            // Segundo grado: lecho rojo y humedo, con pocos jirones de piel
            // levantada (blanquecina), sobre todo hacia el borde de la zona.
            half zone = saturate(IN.burn.g + (fine.r - 0.5) * 0.6) * _Degree;
            half rim = 1 - smoothstep(0.6, 0.95, zone);
            half slough = smoothstep(0.66, 0.8, big.b * 0.75 + fine.g * 0.25) * rim;
            fixed3 wound = lerp(_WoundBase.rgb, _Slough.rgb, slough);
            wound = lerp(wound, _DeepRed.rgb, (1 - slough) * mottle * 0.4);
            wound = lerp(lerp(_Cooled.rgb, _WoundBase.rgb, 0.6), wound, saturate(heat + 0.35));
            col = lerp(col, wound, smoothstep(0.15, 0.85, zone));

            o.Albedo = col;

            // Relieve: arrugas finas en toda la quemadura y la piel levantada
            // mas marcada en la zona de segundo grado.
            fixed4 nt = tex2D(_NormalTex, uv * 0.8 + 0.37);
            half3 n;
            n.xy = (nt.rg * 2 - 1) * _BumpStrength * (0.3 + edge * 0.3 + zone * 0.5);
            n.z = sqrt(saturate(1 - dot(n.xy, n.xy)));
            o.Normal = normalize(n);

            // La piel enrojecida queda algo brillante; la zona de segundo
            // grado, humeda (brilla mucho).
            o.Smoothness = lerp(0.35, 0.78, smoothstep(0.15, 0.85, zone) * (1 - slough * 0.45));
            o.Metallic = 0;

            // Se funde con la piel sana en el borde.
            o.Alpha = edge * _Alpha * lerp(0.45, 0.92, max(core, zone));
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
