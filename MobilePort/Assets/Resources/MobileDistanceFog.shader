Shader "Hidden/TABZ/MobileDistanceFog"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _MainTex_TexelSize;
            float4 _FogColor;
            float _FogStart;
            float _FogEnd;
            float _SkyFogStrength;
            float _DebugDepth;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float eyeDepth = LinearEyeDepth(rawDepth);

                if (_DebugDepth > 0.5)
                {
                    #if defined(UNITY_REVERSED_Z)
                    float isSky = step(rawDepth, 0.00001);
                    #else
                    float isSky = step(0.99999, rawDepth);
                    #endif
                    float depthView = saturate(eyeDepth / 100.0);
                    float depthGray = lerp(depthView, 1.0, isSky);
                    return fixed4(depthGray, depthGray, depthGray, 1.0);
                }

                float geometryFog = saturate((eyeDepth - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                geometryFog = geometryFog * geometryFog * (3.0 - 2.0 * geometryFog);

                // Skybox pixels have no scene depth. Add a restrained haze band
                // around the real camera horizon instead of tinting the whole sky.
                float2 clipXY = i.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0) clipXY.y = -clipXY.y;
                #endif
                float4 viewPos = mul(unity_CameraInvProjection, float4(clipXY, 1.0, 1.0));
                viewPos.xyz /= max(abs(viewPos.w), 0.00001);
                float3 worldDir = normalize(mul((float3x3)unity_CameraToWorld, viewPos.xyz));
                float horizonBand = 1.0 - smoothstep(0.015, 0.30, abs(worldDir.y));
                float skyFog = horizonBand * _SkyFogStrength;

                #if defined(UNITY_REVERSED_Z)
                float isSkyPixel = step(rawDepth, 0.00001);
                #else
                float isSkyPixel = step(0.99999, rawDepth);
                #endif
                float fogAmount = lerp(geometryFog, skyFog, isSkyPixel);
                scene.rgb = lerp(scene.rgb, _FogColor.rgb, saturate(fogAmount));
                return scene;
            }
            ENDCG
        }
    }
    Fallback Off
}