Shader "Custom/Terrain"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
        
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        float4 _StartCols[3];
        float4 _EndCols[3];

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            int colIndex = (int)IN.uv_MainTex.x;
            float t = IN.uv_MainTex.y;
            o.Albedo = lerp(_StartCols[colIndex],_EndCols[colIndex],t);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
