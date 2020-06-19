Shader "Convex Collider Creator/Vertex Colors" {
 
SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 100
   
	CGPROGRAM
		#pragma surface surf BlinnPhong vertex:vert

		struct Input {
			//float2 uv2_MainTex;
			//float2 uv_Detail;
			float3 vertColors;
		};
 
		void vert(inout appdata_full v, out Input o) {
			o.vertColors= v.color.rgb;
		}
 
		void surf (Input IN, inout SurfaceOutput o) {
			o.Albedo = IN.vertColors.rgb;
		}
	ENDCG
}
 
Fallback "Diffuse"
}
 