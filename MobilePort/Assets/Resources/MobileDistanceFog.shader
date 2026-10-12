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
            float _FogExtinction;
            float _FogHeightFalloff;
            float _FogOpacity;
            float _FogStartDistance;
            float _CameraHeight;
            float _DebugDepth;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float eyeDepth = LinearEyeDepth(rawDepth);

                #if defined(UNITY_REVERSED_Z)
                float isSky = step(rawDepth, 0.00001);
                #else
                float isSky = step(0.99999, rawDepth);
                #endif

                if (_DebugDepth > 0.5)
                {
                    float depthView = saturate(eyeDepth / 100.0);
                    float depthGray = lerp(depthView, 1.0, isSky);
                    return fixed4(depthGray, depthGray, depthGray, 1.0);
                }

                // DS_HazeView converts the serialized extinction value to
                // shader units with * 0.01 before assigning _DS_BetaParams.w.
                // The original controller also derives the initial fog
                // density from exp(-heightFalloff * abs(cameraWorldY)).
                float heightDensity = exp(-max(_FogHeightFalloff, 0.0) * abs(_CameraHeight));
                float pathLength = max(eyeDepth - _FogStartDistance, 0.0);
                float opticalDepth = max(_FogExtinction, 0.0) * pathLength * heightDensity;
                float transmittance = exp(-opticalDepth);
                float fogAmount = saturate(_FogOpacity * (1.0 - transmittance));

                // The original DeepSky view has a separate skybox integration
                // path. Keep sky pixels untouched in this low-cost geometry
                // pass rather than applying a screen-wide colour wash.
                fogAmount *= (1.0 - isSky);
                scene.rgb = lerp(scene.rgb, _FogColor.rgb, fogAmount);
                return scene;
            }
            ENDCG
        }
    }
    Fallback Off
}
