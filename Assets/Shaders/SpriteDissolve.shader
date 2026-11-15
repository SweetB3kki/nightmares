Shader "Custom/Sprite Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DissolveTexture ("Dissolve Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _DissolveTiling ("Dissolve Tiling", Vector) = (4,4,0,0)
        _EdgeWidth ("Edge Width", Range(0.001,0.3)) = 0.06
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _UseRadial ("Use Radial", Float) = 0
        _InvertRadial ("Invert Radial", Float) = 0
        _RadialProgress ("Radial Progress", Range(0,1)) = 1
        _RadialRadius ("Radial Radius", Float) = 5
        _RadialSoftness ("Radial Softness", Float) = 0.35
        _WorldCenter ("World Center", Vector) = (0,0,0,0)
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
            sampler2D _DissolveTexture;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _DissolveAmount;
            float4 _DissolveTiling;
            float _EdgeWidth;
            fixed4 _EdgeColor;
            float _UseRadial;
            float _InvertRadial;
            float _RadialProgress;
            float _RadialRadius;
            float _RadialSoftness;
            float4 _WorldCenter;

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
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
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

                float2 dissolveUV = frac(IN.texcoord * _DissolveTiling.xy + _DissolveTiling.zw);
                float dissolveValue = tex2D(_DissolveTexture, dissolveUV).r;
                float dissolveThreshold = saturate(_DissolveAmount);

                if (_UseRadial > 0.5)
                {
                    float progressRadius = saturate(_RadialProgress) * max(0.001, _RadialRadius);
                    float radialDistance = distance(IN.worldPos, _WorldCenter.xy);
                    float softness = max(0.001, _RadialSoftness);
                    float radialMask = smoothstep(progressRadius - softness, progressRadius + softness, radialDistance);

                    if (_InvertRadial > 0.5)
                        radialMask = 1.0 - radialMask;

                    dissolveThreshold = saturate(max(dissolveThreshold, radialMask));
                }

                float clipValue = dissolveValue - dissolveThreshold;
                clip(clipValue);

                float edgeWidth = max(0.001, _EdgeWidth);
                float edge = smoothstep(0.0, edgeWidth, clipValue);
                fixed4 finalColor = lerp(_EdgeColor, c, edge);

                if (finalColor.a <= 0.001)
                    discard;

                return finalColor;
            }
            ENDCG
        }
    }
}
