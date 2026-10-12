Shader "Skybox/Gradient Skybox"
{
    Properties
    {
        _Color1 ("Color 1", Color) = (0.6544118,0.8855981,1,1)
        _Color2 ("Color 2", Color) = (0.020545002,0.4725345,0.6985294,1)
        _UpVector ("Up Vector", Vector) = (0,1,0,0)
        _Intensity ("Intensity", Float) = 1
        _Exponent ("Exponent", Float) = 1
        _UpVectorPitch ("Up Vector Pitch", Float) = 0
        _UpVectorYaw ("Up Vector Yaw", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color1;
            fixed4 _Color2;
            float4 _UpVector;
            float _Intensity;
            float _Exponent;
            float _UpVectorPitch;
            float _UpVectorYaw;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float pitch = radians(_UpVectorPitch);
                float yaw = radians(_UpVectorYaw);
                float3 up = normalize(_UpVector.xyz);
                float3 pitchedUp = float3(
                    up.x * cos(yaw) - up.z * sin(yaw),
                    up.y * cos(pitch) - up.x * sin(pitch),
                    up.z * cos(yaw) + up.x * sin(yaw));
                float height = saturate(dot(direction, normalize(pitchedUp)) * 0.5 + 0.5);
                float gradient = pow(height, max(0.0001, _Exponent));
                fixed4 sky = lerp(_Color2, _Color1, gradient);
                sky.rgb *= _Intensity;
                return sky;
            }
            ENDCG
        }
    }
    Fallback Off
}