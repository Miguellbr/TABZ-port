using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Four-pass mobile approximation of TABZ DeepSky Haze. It generates
/// half-resolution directional radiance, filters it with depth awareness,
/// then composites with full-resolution camera colour and depth.
/// Cascaded shadows and temporal accumulation are not implemented.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MobileDistanceFog : MonoBehaviour
{
    private Camera targetCamera;
    private Material fogMaterial;
    private bool shaderFound;
    private string initializationError;
    private int renderImageCalls;
    private bool depthDebugEnabled;
    private TabzHazeContextData.Settings activeSettings;
    private TabzHazeContextData.ZoneKind activeZone = TabzHazeContextData.ZoneKind.General;
    private TabzHazeZone[] zones;
    private readonly List<TabzHazeZone> matchingZones = new List<TabzHazeZone>(8);
    private float nextContextRefresh;

    [Range(0f, 1f)] public float normalizedDayTime = 0.5f;
    public bool fogEnabled = true;
    [Range(0f, 2f)] public float intensityMultiplier = 1f;
    [Tooltip("Espalhamento mobile aproximado, sem sombras em cascata.")]
    public bool scatteringEnabled = false;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        Shader shader = Resources.Load<Shader>("MobileDistanceFog");
        if (shader == null)
        {
            initializationError = "Shader não encontrado em Resources";
            Debug.LogError("MobileDistanceFog: " + initializationError + ".");
            enabled = false;
            return;
        }
        shaderFound = true;
        if (!shader.isSupported)
        {
            initializationError = "Shader não suportado neste aparelho";
            Debug.LogError("MobileDistanceFog: " + initializationError + ".");
            enabled = false;
            return;
        }
        fogMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        zones = FindObjectsOfType<TabzHazeZone>();
        RefreshContext(true);
    }

    private void Update()
    {
        UpdateDirectionalLight();
        if (Time.unscaledTime >= nextContextRefresh)
        {
            nextContextRefresh = Time.unscaledTime + 0.25f;
            zones = FindObjectsOfType<TabzHazeZone>();
            RefreshContext(false);
        }
    }

    public void SetFog(bool value) { fogEnabled = value; ApplySettings(); }
    public void SetIntensity(float value)
    {
        intensityMultiplier = Mathf.Clamp(value, 0f, 2f);
        ApplySettings();
    }
    public void SetScattering(bool value) { scatteringEnabled = value; ApplySettings(); }
    public void SetContextTime(float value)
    {
        normalizedDayTime = Mathf.Clamp01(value);
        RefreshContext(true);
    }
    public void ToggleDepthDebug()
    {
        depthDebugEnabled = !depthDebugEnabled;
        if (fogMaterial != null)
            fogMaterial.SetFloat("_DebugDepth", depthDebugEnabled ? 1f : 0f);
    }

    private void RefreshContext(bool force)
    {
        if (targetCamera == null || fogMaterial == null) return;
        if (zones == null) zones = FindObjectsOfType<TabzHazeZone>();
        Vector3 position = targetCamera.transform.position;
        matchingZones.Clear();
        for (int i = 0; i < zones.Length; i++)
            if (zones[i] != null && zones[i].Contains(position)) matchingZones.Add(zones[i]);

        TabzHazeContextData.Settings next;
        TabzHazeContextData.ZoneKind selected;
        if (matchingZones.Count == 0)
        {
            selected = TabzHazeContextData.ZoneKind.General;
            next = TabzHazeContextData.Evaluate(selected, normalizedDayTime);
        }
        else
        {
            matchingZones.Sort(TabzHazeZone.ComparePriorityAndSize);
            selected = matchingZones[0].zone;
            next = TabzHazeContextData.Evaluate(selected, normalizedDayTime);
            for (int i = 1; i < matchingZones.Count; i++)
            {
                TabzHazeZone zone = matchingZones[i];
                next = TabzHazeContextData.Settings.Lerp(next,
                    TabzHazeContextData.Evaluate(zone.zone, normalizedDayTime),
                    zone.GetBlendWeight(position));
                selected = zone.zone;
            }
        }
        if (!force && selected == activeZone && SettingsApproximatelyEqual(activeSettings, next))
            return;
        activeZone = selected;
        activeSettings = next;
        ApplySettings();
    }

    private static bool SettingsApproximatelyEqual(
        TabzHazeContextData.Settings a, TabzHazeContextData.Settings b)
    {
        return Mathf.Approximately(a.airScatteringScale, b.airScatteringScale) &&
            Mathf.Approximately(a.airDensityHeightFalloff, b.airDensityHeightFalloff) &&
            Mathf.Approximately(a.hazeScatteringScale, b.hazeScatteringScale) &&
            Mathf.Approximately(a.hazeDensityHeightFalloff, b.hazeDensityHeightFalloff) &&
            Mathf.Approximately(a.hazeScatteringDirection, b.hazeScatteringDirection) &&
            Mathf.Approximately(a.hazeSecondaryScatteringRatio, b.hazeSecondaryScatteringRatio) &&
            Mathf.Approximately(a.extinction, b.extinction) &&
            Mathf.Approximately(a.heightFalloff, b.heightFalloff) &&
            Mathf.Approximately(a.fogScatteringScale, b.fogScatteringScale) &&
            Mathf.Approximately(a.fogScatteringDirection, b.fogScatteringDirection) &&
            Approximately(a.ambient, b.ambient) && Approximately(a.lightColour, b.lightColour) &&
            Mathf.Approximately(a.opacity, b.opacity) &&
            Mathf.Approximately(a.startDistance, b.startDistance);
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) &&
            Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
    }

    private void ApplySettings()
    {
        if (fogMaterial == null) return;
        // DS_HazeView's original m_GaussianDepthFalloff default (_SamplingParams.x).
        fogMaterial.SetFloat("_SamplingFalloff", 500f);
        fogMaterial.SetColor("_FogColor", activeSettings.ambient);
        fogMaterial.SetFloat("_FogExtinction", activeSettings.extinction * intensityMultiplier);
        fogMaterial.SetFloat("_FogHeightFalloff", activeSettings.heightFalloff);
        fogMaterial.SetFloat("_FogOpacity", activeSettings.opacity);
        fogMaterial.SetFloat("_FogStartDistance", fogEnabled ? activeSettings.startDistance : 5f);
        fogMaterial.SetFloat("_HazeScattering",
            0.0021f * activeSettings.hazeScatteringScale * intensityMultiplier);
        fogMaterial.SetFloat("_HazeHeightFalloff", activeSettings.hazeDensityHeightFalloff);
        fogMaterial.SetFloat("_HazeDirection", activeSettings.hazeScatteringDirection);
        fogMaterial.SetFloat("_HazeSecondaryRatio", activeSettings.hazeSecondaryScatteringRatio);
        fogMaterial.SetFloat("_AirScatteringScale", activeSettings.airScatteringScale);
        fogMaterial.SetFloat("_AirHeightFalloff", activeSettings.airDensityHeightFalloff);
        fogMaterial.SetFloat("_FogScatteringScale", activeSettings.fogScatteringScale);
        fogMaterial.SetFloat("_FogScatteringDirection", activeSettings.fogScatteringDirection);
        fogMaterial.SetFloat("_ScatteringEnabled", scatteringEnabled ? 1f : 0f);
        UpdateDirectionalLight();
    }

    private void UpdateDirectionalLight()
    {
        if (fogMaterial == null) return;
        Light sun = RenderSettings.sun;
        if (sun != null && sun.type == LightType.Directional)
        {
            fogMaterial.SetVector("_LightDirection", -sun.transform.forward);
            fogMaterial.SetVector("_LightColour", sun.color * sun.intensity);
        }
        else
        {
            fogMaterial.SetVector("_LightDirection", Vector3.down);
            fogMaterial.SetVector("_LightColour", Vector3.zero);
        }
    }

    public string GetDiagnosticStatus()
    {
        bool depth = targetCamera != null &&
            (targetCamera.depthTextureMode & DepthTextureMode.Depth) != 0;
        string shader = !shaderFound ? "não" :
            (fogMaterial != null && fogMaterial.shader.isSupported ? "OK" : "erro");
        return "Pipeline: radiância 1/2 + blur bilateral (falloff original 500; kernel espacial mobile) + composite | Zona: " +
            activeZone + " | Intensidade: " + (intensityMultiplier * 100f).ToString("0") +
            "% | Espalhamento: " + (scatteringEnabled ? "ativo" : "desativado") +
            " | Shader: " + shader + " | Depth: " + (depth ? "solicitado" : "não") +
            " | Render: " + (renderImageCalls > 0 ? renderImageCalls + " chamadas" : "NÃO chamado") +
            " | Erro: " + (string.IsNullOrEmpty(initializationError) ? "nenhum" : initializationError);
    }

    private void UpdateOriginalViewportRays()
    {
        if (fogMaterial == null || targetCamera == null) return;
        float far = targetCamera.farClipPlane;
        float halfHeight = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * far;
        float halfWidth = halfHeight * targetCamera.aspect;
        Transform t = targetCamera.transform;
        Vector3 forward = t.forward * far;
        Vector3 right = t.right * halfWidth;
        Vector3 up = t.up * halfHeight;
        fogMaterial.SetVector("_ViewportCorner", forward - right - up);
        fogMaterial.SetVector("_ViewportRight", right * 2f);
        fogMaterial.SetVector("_ViewportUp", up * 2f);
        fogMaterial.SetFloat("_ViewportFarClip", far);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        renderImageCalls++;
        if (fogMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        UpdateOriginalViewportRays();
        if (intensityMultiplier <= 0f && !depthDebugEnabled)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (depthDebugEnabled)
        {
            Graphics.Blit(source, destination, fogMaterial, 3);
            return;
        }

        int width = Mathf.Max(1, source.width / 2);
        int height = Mathf.Max(1, source.height / 2);
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
        RenderTexture radiance = null;
        RenderTexture blur = null;
        try
        {
            radiance = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
            blur = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
            radiance.filterMode = FilterMode.Point;
            blur.filterMode = FilterMode.Point;
            radiance.wrapMode = TextureWrapMode.Clamp;
            blur.wrapMode = TextureWrapMode.Clamp;

            Graphics.Blit(source, radiance, fogMaterial, 0);
            Graphics.Blit(radiance, blur, fogMaterial, 1);
            fogMaterial.SetTexture("_RadianceBuffer", blur);
            Graphics.Blit(blur, radiance, fogMaterial, 2);
            fogMaterial.SetTexture("_RadianceBuffer", radiance);
            Graphics.Blit(source, destination, fogMaterial, 3);
        }
        finally
        {
            fogMaterial.SetTexture("_RadianceBuffer", null);
            if (radiance != null) RenderTexture.ReleaseTemporary(radiance);
            if (blur != null) RenderTexture.ReleaseTemporary(blur);
        }
    }

    private void OnDestroy()
    {
        if (fogMaterial != null)
        {
            if (Application.isPlaying) Destroy(fogMaterial);
            else DestroyImmediate(fogMaterial);
        }
    }
}