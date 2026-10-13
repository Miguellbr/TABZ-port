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
            float3 _ViewportCorner;
            float3 _ViewportRight;
            float3 _ViewportUp;
            float _ViewportFarClip;
            float _HazeScattering;
            float _HazeHeightFalloff;
            float _HazeDirection;
            float _HazeSecondaryRatio;
            float _AirScatteringScale;
            float _AirHeightFalloff;
            float _FogScatteringScale;
            float _FogScatteringDirection;
            float _ScatteringEnabled;
            float3 _LightDirection;
            float3 _LightColour;

            // Stable integral of rho(y)=rhoStart*exp(-k*deltaY)
            // over a straight ray segment of length L.
            float IntegrateExponentialDensity(float densityAtStart, float falloff,
                float verticalDelta, float segmentLength)
            {
                if (segmentLength <= 0.0) return 0.0;
                float x = max(falloff, 0.0) * verticalDelta;
                if (abs(x) < 0.0001)
                    return densityAtStart * segmentLength;
                return densityAtStart * segmentLength * (1.0 - exp(-x)) / x;
            }

            float PhaseFunction(float anisotropy, float cosine)
            {
                float g = clamp(anisotropy, -0.99, 0.99);
                float denominator = max(1.0 - g * cosine, 0.0001);
                return (1.0 - g * g) /
                    (denominator * denominator) * 0.0795774683;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 scene = tex2D(_MainTex, i.uv);
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
                    return float4(depthGray, depthGray, depthGray, 1.0);
                }

                // Match the original DS_HazeView vertex shader:
                // corner + right*u + up*v, including its render-texture Y flip.
                float rayV = (_MainTex_TexelSize.y < 0.0)
                    ? (1.0 - i.uv.y) : i.uv.y;
                float3 viewportRay = _ViewportCorner +
                    _ViewportRight * i.uv.x + _ViewportUp * rayV;
                float3 viewRay = viewportRay /
                    max(_ViewportFarClip, 0.00001);
                float3 worldRay = normalize(viewportRay);
                // Original v3 is far-clip sized and is multiplied by
                // Linear01Depth; this is equivalent to eyeDepth * viewRay.
                float rayLength = eyeDepth * length(viewRay);
                float segmentLength = max(rayLength - _FogStartDistance, 0.0);
                float startDistance = min(_FogStartDistance, rayLength);
                float3 cameraPosition = _WorldSpaceCameraPos;
                float cameraHeight = cameraPosition.y;
                float verticalDelta = worldRay.y * segmentLength;

                // The original controller supplies initial density at the
                // camera height as exp(-falloff*abs(cameraY)). Advance that
                // density to the fog-start point, then integrate it exactly
                // over the remaining straight ray segment.
                float fogStartDensity = exp(-max(_FogHeightFalloff, 0.0) *
                    abs(cameraHeight)) * exp(-max(_FogHeightFalloff, 0.0) *
                    worldRay.y * startDistance);
                float fogDensityIntegral = IntegrateExponentialDensity(
                    fogStartDensity, _FogHeightFalloff, verticalDelta, segmentLength);
                float opticalDepth = max(_FogExtinction, 0.0) * fogDensityIntegral;
                float transmittance = exp(-opticalDepth);
                float fogAmount = saturate(_FogOpacity * (1.0 - transmittance));

                float3 atmosphereRadiance = 0.0;
                if (_ScatteringEnabled > 0.5 && segmentLength > 0.0)
                {
                    float cosine = dot(normalize(_LightDirection), worldRay);

                    // Air/Rayleigh is a separate RGB contribution in the
                    // original composite; it does not use the haze beta.
                    float airStartDensity = exp(-max(_AirHeightFalloff, 0.0) *
                        abs(cameraHeight)) * exp(-max(_AirHeightFalloff, 0.0) *
                        worldRay.y * startDistance);
                    float airIntegral = IntegrateExponentialDensity(
                        airStartDensity, _AirHeightFalloff, verticalDelta, segmentLength);
                    float3 rayleighBeta = float3(0.00116, 0.0027, 0.00662) *
                        max(_AirScatteringScale, 0.0);
                    atmosphereRadiance += _LightColour * rayleighBeta *
                        (airIntegral * 0.0795774683);

                    // The main composite's local haze term uses (1-ratio).
                    // The complementary ratio belongs to the separate,
                    // shadow-cascade directional-radiance pass, not this
                    // unshadowed approximation.
                    float hazeStartDensity = exp(-max(_HazeHeightFalloff, 0.0) *
                        abs(cameraHeight)) * exp(-max(_HazeHeightFalloff, 0.0) *
                        worldRay.y * startDistance);
                    float hazeIntegral = IntegrateExponentialDensity(
                        hazeStartDensity, _HazeHeightFalloff, verticalDelta, segmentLength);
                    float hazePhase = PhaseFunction(_HazeDirection, cosine);
                    atmosphereRadiance += _LightColour * (hazePhase *
                        max(_HazeScattering, 0.0) *
                        (1.0 - saturate(_HazeSecondaryRatio)) * hazeIntegral);

                    // Fog scattering is independent from fog extinction and
                    // has its own anisotropy. Gameplay zones currently set
                    // its scale to zero, but the channel remains represented.
                    float fogStartScatteringDensity = fogStartDensity;
                    float fogScatteringIntegral = IntegrateExponentialDensity(
                        fogStartScatteringDensity, _FogHeightFalloff,
                        verticalDelta, segmentLength);
                    float fogPhase = PhaseFunction(_FogScatteringDirection, cosine);
                    atmosphereRadiance += _LightColour * (fogPhase *
                        max(_FogScatteringScale, 0.0) * _FogOpacity *
                        fogScatteringIntegral);
                }

                // This pass still lacks the original shadowed radiance target,
                // bilateral filtering and exact fog ambient/direct composition.
                // Keep the known extinction and unshadowed scattering terms
                // isolated from sky pixels.
                float geometryMask = 1.0 - isSky;
                fogAmount *= geometryMask;
                scene.rgb = lerp(scene.rgb, _FogColor.rgb, fogAmount);
                scene.rgb += atmosphereRadiance * geometryMask;
                return scene;
            }
            ENDCG
        }
    }
    Fallback Off
}
