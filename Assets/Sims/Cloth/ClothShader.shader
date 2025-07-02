Shader "Custom/ClothFromBuffer"
{
    Properties
    {
        _GradientTex("GradientTex", 2D) = "white"{}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            sampler2D _GradientTex;
            float4 _Color;
            
            struct appdata
            {
                float3 vertex   : POSITION;
                float3 normal   : NORMAL;
                float2 uv       : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
            };
            
            v2f vert(appdata v)
            {
                v2f o;

                o.uv = v.uv;
                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz, 1.0));
                o.worldNormal = v.normal;

                return o;
            }

            // Lambert Lighting
            float4 frag(v2f i) : SV_Target
            {  
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = dot(i.worldNormal, lightDir);
                float3 color = tex2D(_GradientTex, i.uv) * ndotl;
                return float4(color, 1);
            }

            ENDCG
        }
    }
}
