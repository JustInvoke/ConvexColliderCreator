Shader "Convex Collider Creator/Mesh Preview Wire" {

SubShader {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	LOD 100

	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha
	
	Pass {  
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			
			#include "UnityCG.cginc"

			struct v2f {
				float4 pos : SV_POSITION;
                fixed4 color : COLOR;
			};
			
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color.xyz = pow(v.normal * 0.5 + 0.5, 0.5);
                o.color.w = 1.0;
                return o;
            }
			
			fixed4 frag (v2f i) : SV_Target { return i.color; }
		ENDCG
	}
}

}