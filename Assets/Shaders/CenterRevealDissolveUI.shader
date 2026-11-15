Shader "UI/Center Reveal Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _Center ("Center", Vector) = (0.5,0.5,0,0)
        _EdgeSoftness ("Edge Softness", Range(0.001,0.25)) = 0.045
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 5
        _NoiseStrength ("Noise Strength", Range(0,0.15)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            float4 _Center;
            float _Progress;
            float _EdgeSoftness;
            float _NoiseScale;
            float _NoiseStrength;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 centered = uv - _Center.xy;
                float radius = length(centered) / 0.70710678;

                float noise = tex2D(_NoiseTex, uv * _NoiseScale).r;
                noise = (noise - 0.5) * _NoiseStrength;

                float threshold = lerp(-_EdgeSoftness * 2.0, 1.2, saturate(_Progress)) + noise;
                float alpha = smoothstep(threshold - _EdgeSoftness, threshold + _EdgeSoftness, radius);

                fixed4 color = IN.color;
                color.rgb = 1.0;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
