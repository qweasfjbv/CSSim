Shader "Custom/WaveShader"
{
    Properties
    {
        _GradientTex("GradientTex", 2D) = "white"{}
        _Color ("Color", Color) = (1,1,1,1)
        _Resolution ("Resolution", int) = 64
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            StructuredBuffer<float4x4> matrixBuffer;
            
            sampler2D _GradientTex;
            float4 _Color;
            float _EdgeLength;

            struct appdata
            {
                float3 vertex : POSITION;
                float3 normal : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };
            
            v2f vert(appdata v)
            {
                v2f o;

                float4x4 modelMatrix = matrixBuffer[v.instanceID];
                float3 worldPos = mul(modelMatrix, float4(v.vertex, 1.0)).xyz;

                float u = worldPos.x / _EdgeLength;
                float v1 = worldPos.z / _EdgeLength;
                o.uv = saturate(float2(u, v1));
                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0));
                
                float3 worldNormal = normalize(mul((float3x3)modelMatrix, v.normal));
                o.worldNormal = worldNormal;
                return o;
            }

            // Lambert Lighting
            float4 frag(v2f i) : SV_Target
            {  
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(i.worldNormal, lightDir));

                float3 color = tex2D(_GradientTex, i.uv) * NdotL;
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
