Shader "Custom/Boid_SurfaceShader"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            struct Boid
            {
                float3 pos;
                float3 dir;
            };

            StructuredBuffer<Boid> boidBuffer;

            v2f vert(appdata_t i, uint instanceID : SV_InstanceID)
            {
                //Zero initialise
                v2f o = (v2f)0;

                //Cache
                float3 boidPos = boidBuffer[instanceID].pos;
                float3 boidDir = boidBuffer[instanceID].dir;

                //Calculate rotation
                float3 zAxis = normalize((boidPos + boidDir) - boidPos);
                float3 xAxis = normalize(cross(float3(0.f, 1.f, 0.f), zAxis));
                float3 yAxis = cross(zAxis, xAxis); 
                float4x4 lookAt =
                {
                    xAxis.x,    yAxis.x,    zAxis.x,    0.f,
                    xAxis.y,    yAxis.y,    zAxis.y,    0.f,
                    xAxis.z,    yAxis.z,    zAxis.z,    0.f,
                    0.f,        0.f,        0.f,        1.f
                };

                //Calculate position
                float4 pos = mul(lookAt, i.vertex);
                pos.xyz += boidPos;
                o.vertex = UnityObjectToClipPos(pos);

                //Colour is determined by direction
                o.color = float4( ((boidBuffer[instanceID].dir + 1.f) * 0.5f), 1.f);

                //Finished
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return saturate(i.color);
            }

            /*void surf(Input IN, inout SurfaceOutputStandard o)
            {
                // Albedo comes from a texture tinted by color
                fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
                o.Albedo = c.rgb;
                // Metallic and smoothness come from slider variables
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = c.a;
            }*/

            ENDCG
        }
    }
}