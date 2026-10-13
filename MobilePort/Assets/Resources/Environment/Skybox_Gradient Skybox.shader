Shader "Skybox/Gradient Skybox"
{
    Properties
    {
        _Color1 ("Color 1", Color) = (1,1,1,0)
        _Color2 ("Color 2", Color) = (1,1,1,0)
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color1;
            float4 _Color2;
            float4 _UpVector;
            float _Intensity;
            float _Exponent;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 direction : TEXCOORD0;
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
                // The original DX11 vertex program forwards TEXCOORD0
                // unchanged; it does not derive direction from POSITION.
                o.direction = v.direction;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float height = dot(direction, _UpVector.xyz) * 0.5 + 0.5;
                float gradient = pow(height, _Exponent);
                float4 sky = lerp(_Color2, _Color1, gradient);
                // Original DXBC multiplies the complete RGBA result.
                sky *= _Intensity;
                return sky;
            }
            ENDCG
        }
    }
    Fallback Off
}
