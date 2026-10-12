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
    private float nextContextRefresh;
    private float lastAppliedTime = -1f;

    [Range(0f, 1f)]
    public float normalizedDayTime = 0.5f;

    public bool fogEnabled = true;

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

        TabzHazeContextData.ZoneKind selectedZone = TabzHazeContextData.ZoneKind.General;
        int selectedPriority = int.MinValue;

        if (zones == null) zones = FindObjectsOfType<TabzHazeZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            TabzHazeZone candidate = zones[i];
            if (candidate == null || !candidate.Contains(targetCamera.transform.position))
                continue;

            TabzHazeContextData.ZoneInfo info =
                TabzHazeContextData.GetZoneInfo(candidate.zone);
            if (info.priority > selectedPriority)
            {
                selectedPriority = info.priority;
                selectedZone = candidate.zone;
            }
        }

        if (!force && selectedZone == activeZone &&
            Mathf.Approximately(lastAppliedTime, normalizedDayTime))
            return;

        activeZone = selectedZone;
        activeSettings = TabzHazeContextData.Evaluate(activeZone, normalizedDayTime);
        lastAppliedTime = normalizedDayTime;
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (fogMaterial == null) return;

        fogMaterial.SetColor("_FogColor", activeSettings.ambient);
        fogMaterial.SetFloat("_FogExtinction", activeSettings.extinction);
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
            " | Extinção: " + activeSettings.extinction.ToString("0.0000") +
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
