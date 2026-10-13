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
            float _HazeScattering;
            float _HazeHeightFalloff;
            float _HazeDirection;
            float _HazeSecondaryRatio;
            float _ScatteringEnabled;
            float3 _LightDirection;
            float3 _LightColour;

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

                // Reduced unshadowed directional haze: the original DX11
                // pass uses 15 ray steps with cascaded shadow comparisons.
                // This mobile path keeps the recovered phase and height law,
                // but uses 8 midpoint samples and has no cascade visibility.
                float3 hazeRadiance = 0.0;
                if (_ScatteringEnabled > 0.5 && _HazeScattering > 0.0 &&
                    _HazeSecondaryRatio > 0.0)
                {
                    float3 viewRay = mul(unity_CameraInvProjection,
                        float4(i.uv * 2.0 - 1.0, 1.0, 1.0)).xyz;
                    viewRay /= max(-viewRay.z, 0.00001);
                    float3 worldRay = normalize(
                        mul((float3x3)unity_CameraToWorld, viewRay));
                    float rayLength = eyeDepth * length(viewRay);
                    float segmentLength = max(rayLength - _FogStartDistance, 0.0);
                    float stepLength = segmentLength * 0.125;
                    float g = clamp(_HazeDirection, -0.99, 0.99);
                    float cosine = dot(normalize(_LightDirection), worldRay);
                    float phaseDenominator = max(1.0 - g * cosine, 0.0001);
                    float phase = (1.0 - g * g) /
                        (phaseDenominator * phaseDenominator) * 0.0795774683;
                    float densityIntegral = 0.0;

                    [unroll]
                    for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                    {
                        float sampleDistance = (sampleIndex + 0.5) * stepLength;
                        float sampleHeight = _WorldSpaceCameraPos.y +
                            worldRay.y * sampleDistance;
                        float sampleDensity = exp(-_HazeHeightFalloff * sampleHeight);
                        densityIntegral += sampleDensity * stepLength;
                    }

                    hazeRadiance = _LightColour * (phase * _HazeScattering *
                        _HazeSecondaryRatio * densityIntegral);
                }

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
