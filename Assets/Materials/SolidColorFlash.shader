Shader "Custom/SpriteFlash Overlay"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_FlashColor("Flash Color", Color) = (1,1,1,1)
		_FlashAmount("Flash Amount", Range(0, 1)) = 0
		[HideInInspector] _Color("Tint", Color) = (1,1,1,1)
	}

		SubShader
		{
			Tags
			{
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
				"RenderType" = "Transparent"
				"PreviewType" = "Plane"
				"CanUseSpriteAtlas" = "True"
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
					fixed4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				sampler2D _MainTex;
				fixed4 _FlashColor;
				float _FlashAmount;

				v2f vert(appdata_t IN)
				{
					v2f OUT;
					OUT.vertex = UnityObjectToClipPos(IN.vertex);
					OUT.texcoord = IN.texcoord;
					OUT.color = IN.color;
					return OUT;
				}

				fixed4 frag(v2f IN) : SV_Target
				{
					fixed4 texColor = tex2D(_MainTex, IN.texcoord);

				// The Magic Logic:
				// We interpolate (Lerp) between the original texture RGB 
				// and the Flash Color RGB, based on the _FlashAmount.
				fixed3 finalRGB = lerp(texColor.rgb, _FlashColor.rgb, _FlashAmount);

				// We keep the original alpha so the sprite shape doesn't change
				return fixed4(finalRGB, texColor.a);
			}
			ENDCG
		}
		}
}