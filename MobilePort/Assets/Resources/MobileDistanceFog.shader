Shader "Hidden/TABZ/MobileDistanceFog"
{
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex, _RadianceBuffer;
  UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
  float4 _MainTex_TexelSize, _RadianceBuffer_TexelSize, _CameraDepthTexture_TexelSize, _FogColor;
  float _FogExtinction, _FogHeightFalloff, _FogOpacity, _FogStartDistance, _DebugDepth, _ViewportFarClip, _SamplingFalloff;
  float3 _ViewportCorner, _ViewportRight, _ViewportUp;
  float _HazeScattering, _HazeHeightFalloff, _HazeDirection, _HazeSecondaryRatio;
  float _AirScatteringScale, _AirHeightFalloff, _FogScatteringScale, _FogScatteringDirection, _ScatteringEnabled;
  float3 _LightDirection, _LightColour;

  float IntegrateExponentialDensity(float rho, float k, float dy, float len)
  {
   if (len <= 0.0) return 0.0;
   float x = max(k, 0.0) * dy;
   if (abs(x) < 0.0001) return rho * len;
   return rho * len * (1.0 - exp(-x)) / x;
  }
  float PhaseFunction(float g, float mu)
  {
   g = clamp(g, -0.99, 0.99);
   float d = max(1.0 - g * mu, 0.0001);
   return (1.0 - g * g) / (d * d) * 0.0795774683;
  }
  float3 ViewportRay(float2 uv)
  {
   float v = (_MainTex_TexelSize.y < 0.0) ? 1.0 - uv.y : uv.y;
   return (_ViewportCorner + _ViewportRight * uv.x + _ViewportUp * v) / max(_ViewportFarClip, 0.00001);
  }
  float IsSkyDepth(float z)
  {
   #if defined(UNITY_REVERSED_Z)
   return step(z, 0.00001);
   #else
   return step(0.99999, z);
   #endif
  }
  float4 fragRadiance(v2f_img i) : SV_Target
  {
   float2 depthOffset = _CameraDepthTexture_TexelSize.xy * 0.5;
   float raw0 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + float2(-depthOffset.x, -depthOffset.y));
   float raw1 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + float2(depthOffset.x, -depthOffset.y));
   float raw2 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + float2(-depthOffset.x, depthOffset.y));
   float raw3 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv + depthOffset);
   // Original DS_DEPTHDOWNSAMPLE writes the maximum of four raw depth samples.
   float raw = max(max(raw0, raw1), max(raw2, raw3));
   float eye = LinearEyeDepth(raw);
   if (IsSkyDepth(raw) > 0.5) return float4(0,0,0,1.0);
   if (_ScatteringEnabled < 0.5) return float4(0,0,0,eye);
   float3 ray = ViewportRay(i.uv), dir = normalize(ray);
   float rayLength = eye * length(ray);
   float begin = min(_FogStartDistance, rayLength);
   float segment = max(rayLength - begin, 0.0);
   float phase = PhaseFunction(_HazeDirection, dot(normalize(_LightDirection), dir));
   float3 radiance = 0.0;
   // Original pass uses 15 samples at 1/16 intervals. Cascaded shadows are unavailable.
   [unroll] for (int sampleIndex = 1; sampleIndex <= 15; sampleIndex++)
   {
    float distanceAlongRay = begin + segment * ((float)sampleIndex / 16.0);
    float3 p = _WorldSpaceCameraPos + dir * distanceAlongRay;
    float density = exp(-max(_HazeHeightFalloff, 0.0) * abs(p.y));
    radiance += _LightColour * phase * max(_HazeScattering, 0.0) *
     saturate(_HazeSecondaryRatio) * density * (segment / 16.0);
   }
   return float4(radiance, eye);
  }
  float4 BlurAxis(float2 uv, float2 axis)
  {
   float4 center = tex2D(_MainTex,uv);
   float2 delta = _MainTex_TexelSize.xy * axis;
   float4 a=tex2D(_MainTex,uv-delta*2.0), b=tex2D(_MainTex,uv-delta);
   float4 c=tex2D(_MainTex,uv+delta), d=tex2D(_MainTex,uv+delta*2.0);
   float da = a.a - center.a, db = b.a - center.a;
   float dc = c.a - center.a, dd = d.a - center.a;
   // Original DS_Haze uses exp(-GaussianDepthFalloff * depthDifference^2).
   float wa=exp(-_SamplingFalloff*da*da)*0.0625;
   float wb=exp(-_SamplingFalloff*db*db)*0.25;
   float wc=0.375;
   float wd=exp(-_SamplingFalloff*dd*dd)*0.0625;
   float we=exp(-_SamplingFalloff*dc*dc)*0.25;
   float total=wa+wb+wc+wd+we;
   return float4((a.rgb*wa+b.rgb*wb+center.rgb*wc+c.rgb*we+d.rgb*wd)/max(total,0.00001),center.a);
  }
  float4 fragBlurHorizontal(v2f_img i):SV_Target{return BlurAxis(i.uv,float2(1,0));}
  float4 fragBlurVertical(v2f_img i):SV_Target{return BlurAxis(i.uv,float2(0,1));}
  float4 fragComposite(v2f_img i):SV_Target
  {
   float4 scene=tex2D(_MainTex,i.uv);
   float raw=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv);
   float eye=LinearEyeDepth(raw), sky=IsSkyDepth(raw);
   if(_DebugDepth>0.5){float g=lerp(saturate(eye/100.0),1.0,sky);return float4(g,g,g,1);}
   float3 ray=ViewportRay(i.uv),dir=normalize(ray);
   float rayLength=eye*length(ray),begin=min(_FogStartDistance,rayLength);
   float segment=max(rayLength-begin,0.0),dy=dir.y*segment,camY=_WorldSpaceCameraPos.y;
   float fogDensity=exp(-max(_FogHeightFalloff,0.0)*abs(camY))*
    exp(-max(_FogHeightFalloff,0.0)*dir.y*begin);
   float fogIntegral=IntegrateExponentialDensity(fogDensity,_FogHeightFalloff,dy,segment);
   float transmittance=exp(-max(_FogExtinction,0.0)*fogIntegral);
   float fogAmount=saturate(_FogOpacity*(1.0-transmittance));

   // Depth-aware upsample: select the closest of four half-resolution depth samples.
   float2 texel=_RadianceBuffer_TexelSize.xy;
   float2 baseUV=(floor(i.uv/texel)+0.5)*texel;
   float4 r0=tex2D(_RadianceBuffer,baseUV);
   float4 r1=tex2D(_RadianceBuffer,baseUV+float2(texel.x,0));
   float4 r2=tex2D(_RadianceBuffer,baseUV+float2(0,texel.y));
   float4 r3=tex2D(_RadianceBuffer,baseUV+texel);
   float e0=abs(r0.a-eye),e1=abs(r1.a-eye),e2=abs(r2.a-eye),e3=abs(r3.a-eye);
   float3 directionalRadiance=r0.rgb; float closest=e0;
   if(e1<closest){closest=e1;directionalRadiance=r1.rgb;}
   if(e2<closest){closest=e2;directionalRadiance=r2.rgb;}
   if(e3<closest)directionalRadiance=r3.rgb;
   float3 atmosphere=directionalRadiance;
   if(_ScatteringEnabled>0.5 && segment>0.0)
   {
    float mu=dot(normalize(_LightDirection),dir);
    float airDensity=exp(-max(_AirHeightFalloff,0.0)*abs(camY))*
     exp(-max(_AirHeightFalloff,0.0)*dir.y*begin);
    float airIntegral=IntegrateExponentialDensity(airDensity,_AirHeightFalloff,dy,segment);
    atmosphere+=_LightColour*(float3(0.00116,0.0027,0.00662)*max(_AirScatteringScale,0.0))*
     (airIntegral*0.0795774683);
    float hazeDensity=exp(-max(_HazeHeightFalloff,0.0)*abs(camY))*
     exp(-max(_HazeHeightFalloff,0.0)*dir.y*begin);
    float hazeIntegral=IntegrateExponentialDensity(hazeDensity,_HazeHeightFalloff,dy,segment);
    atmosphere+=_LightColour*PhaseFunction(_HazeDirection,mu)*max(_HazeScattering,0.0)*
     (1.0-saturate(_HazeSecondaryRatio))*hazeIntegral;
    atmosphere+=_LightColour*PhaseFunction(_FogScatteringDirection,mu)*
     max(_FogScatteringScale,0.0)*_FogOpacity*fogIntegral;
   }
   float geometry=1.0-sky;
   scene.rgb=lerp(scene.rgb,_FogColor.rgb,fogAmount*geometry);
   scene.rgb+=atmosphere*geometry;
   return scene;
  }
  ENDCG
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment fragRadiance
   #pragma target 3.0
  ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment fragBlurHorizontal
   #pragma target 3.0
  ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment fragBlurVertical
   #pragma target 3.0
  ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment fragComposite
   #pragma target 3.0
  ENDCG }
 }
 Fallback Off
}