using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mobile Built-in Render Pipeline implementation driven by recovered TABZ
/// DeepSky Haze context values. It reproduces the context data and a cheap
/// depth-based extinction pass; it is not the original multi-pass PC shader.
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
    private float lastAppliedTime = -1f;

    [Range(0f, 1f)]
    public float normalizedDayTime = 0.5f;

    public bool fogEnabled = true;
    [Range(0f, 2f)] public float intensityMultiplier = 1f;

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

        fogMaterial = new Material(shader);
        fogMaterial.hideFlags = HideFlags.HideAndDontSave;
        zones = FindObjectsOfType<TabzHazeZone>();
        RefreshContext(true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextContextRefresh)
        {
            nextContextRefresh = Time.unscaledTime + 0.25f;
            RefreshContext(false);
        }
    }

    public void SetFog(bool value)
    {
        fogEnabled = value;
        ApplySettings();
    }

    public void SetIntensity(float value)
    {
        intensityMultiplier = Mathf.Clamp(value, 0f, 2f);
        ApplySettings();
    }

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

        Vector3 cameraPosition = targetCamera.transform.position;
        matchingZones.Clear();

        for (int i = 0; i < zones.Length; i++)
        {
            TabzHazeZone candidate = zones[i];
            if (candidate != null && candidate.Contains(cameraPosition))
                matchingZones.Add(candidate);
        }

        TabzHazeContextData.Settings nextSettings;
        TabzHazeContextData.ZoneKind selectedZone;

        if (matchingZones.Count == 0)
        {
            // The offline sandbox has a general-context fallback if no
            // recovered zone marker is present in a scene.
            selectedZone = TabzHazeContextData.ZoneKind.General;
            nextSettings = TabzHazeContextData.Evaluate(
                selectedZone, normalizedDayTime);
        }
        else
        {
            // DS_HazeCore sorts by ascending priority, then ascending zone
            // size. It starts with the first context and sequentially blends
            // every later overlapping zone using that zone's edge weight.
            matchingZones.Sort(TabzHazeZone.ComparePriorityAndSize);

            TabzHazeZone firstZone = matchingZones[0];
            selectedZone = firstZone.zone;
            nextSettings = TabzHazeContextData.Evaluate(
                firstZone.zone, normalizedDayTime);

            for (int i = 1; i < matchingZones.Count; i++)
            {
                TabzHazeZone overlayZone = matchingZones[i];
                float blendWeight = overlayZone.GetBlendWeight(cameraPosition);
                TabzHazeContextData.Settings overlaySettings =
                    TabzHazeContextData.Evaluate(
                        overlayZone.zone, normalizedDayTime);

                nextSettings = TabzHazeContextData.Settings.Lerp(
                    nextSettings, overlaySettings, blendWeight);
                selectedZone = overlayZone.zone;
            }
        }

        if (!force && selectedZone == activeZone &&
            Mathf.Approximately(lastAppliedTime, normalizedDayTime))
            return;

        activeZone = selectedZone;
        activeSettings = nextSettings;
        lastAppliedTime = normalizedDayTime;
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (fogMaterial == null) return;

        fogMaterial.SetColor("_FogColor", activeSettings.ambient);
        fogMaterial.SetFloat("_FogExtinction", activeSettings.extinction * intensityMultiplier);
        fogMaterial.SetFloat("_FogHeightFalloff", activeSettings.heightFalloff);
        fogMaterial.SetFloat("_FogOpacity", activeSettings.opacity);
        // The original fogoff command changes the start distance to 5; it
        // does not remove the entire atmosphere.
        fogMaterial.SetFloat("_FogStartDistance",
            fogEnabled ? activeSettings.startDistance : 5f);
        fogMaterial.SetFloat("_CameraHeight", targetCamera != null
            ? targetCamera.transform.position.y : 0f);
    }

    public string GetDiagnosticStatus()
    {
        bool depthRequested = targetCamera != null &&
            (targetCamera.depthTextureMode & DepthTextureMode.Depth) != 0;
        string shaderStatus = !shaderFound ? "não" :
            (fogMaterial != null && fogMaterial.shader.isSupported ? "OK" : "erro");
        string renderStatus = renderImageCalls > 0
            ? renderImageCalls.ToString() + " chamadas" : "NÃO chamado";
        string errorStatus = string.IsNullOrEmpty(initializationError)
            ? "nenhum" : initializationError;
        return "Modo: " + (depthDebugEnabled ? "VISUALIZANDO DEPTH" : "névoa") +
            " | Zona: " + activeZone +
            " | Hora normalizada: " + normalizedDayTime.ToString("0.000") +
            " | Intensidade: " + (intensityMultiplier * 100f).ToString("0") + "%"
            + " | Extinção: " + (activeSettings.extinction * intensityMultiplier).ToString("0.0000") +
            " | Densidade vertical: " + activeSettings.heightFalloff.ToString("0.0000") +
            " | Câmera: " + (targetCamera != null ? "OK" : "ausente") +
            " | Shader: " + shaderStatus +
            " | Depth: " + (depthRequested ? "solicitado" : "não") +
            " | Render: " + renderStatus +
            " | Erro: " + errorStatus;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        renderImageCalls++;
        if (fogMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // A zero-intensity setting is a true bypass, not merely zero extinction.
        // Keep the depth diagnostic available even when the fog slider is at 0%.
        if (intensityMultiplier <= 0f && !depthDebugEnabled)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Graphics.Blit(source, destination, fogMaterial);
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
