Shader "Custom/Boss Vertical Destruction"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _FrontY ("Front Y", Float) = 0
        _NoiseScale ("Noise Scale", Float) = 0.18
        _NoiseAmplitude ("Noise Amplitude", Float) = 0.5
        _Softness ("Softness", Float) = 0.2
        _EdgeWidth ("Edge Width", Float) = 0.08
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float _FrontY;
            float _NoiseScale;
            float _NoiseAmplitude;
            float _Softness;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                float4 world = mul(unity_ObjectToWorld, IN.vertex);
                OUT.worldPos = world.xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                if (c.a <= 0.001)
                    discard;

                float2 noiseUV = IN.worldPos.xy * _NoiseScale;
                float noiseValue = tex2D(_NoiseTex, noiseUV).r * 2.0 - 1.0;
                float threshold = _FrontY + noiseValue * _NoiseAmplitude;
                float softness = max(0.001, _Softness);
                float edgeWidth = max(0.001, _EdgeWidth);

                float cutoff = threshold + noiseValue * 0.001;
                clip(IN.worldPos.y - cutoff);
                return c;
            }
            ENDCG
        }
    }
}
