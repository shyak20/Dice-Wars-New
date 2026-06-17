Shader "DiceWars/MainTexScrollLoop"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed (XY)", Vector) = (1, 0, 0, 0)
        _Tint ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ScrollSpeed;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // frac() wraps UVs 0..1 so tiled textures loop seamlessly.
                float2 uv = frac(i.uv + (_ScrollSpeed.xy * _Time.y));
                fixed4 c = tex2D(_MainTex, uv) * _Tint;
                return c;
            }
            ENDCG
        }
    }
}
