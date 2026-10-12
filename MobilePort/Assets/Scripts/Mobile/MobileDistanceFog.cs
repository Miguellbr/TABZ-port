using UnityEngine;

/// <summary>
/// Lightweight built-in-render-pipeline distance haze for the Android
/// prototype. This is an approximation, not the original DeepSky Haze shader.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MobileDistanceFog : MonoBehaviour
{
    private static readonly Color TabzHazeColor =
        new Color(0.6911765f, 0.93610543f, 1f, 1f);

    private Camera targetCamera;
    private Material fogMaterial;
    private bool shaderFound;
    private string initializationError;
    private int renderImageCalls;

    [Range(0, 2)] public int profile = 1;
    public bool fogEnabled = true;

    private static readonly float[] FogStarts = { 24f, 12f, 5f };
    private static readonly float[] FogEnds = { 85f, 58f, 38f };
    private static readonly float[] SkyStrengths = { 0.22f, 0.42f, 0.62f };

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
        ApplyProfile();
    }

    public void SetFog(bool value)
    {
        fogEnabled = value;
        ApplyProfile();
    }

    public void SetProfile(int value)
    {
        profile = Mathf.Clamp(value, 0, 2);
        ApplyProfile();
    }

    private void ApplyProfile()
    {
        if (fogMaterial == null) return;
        fogMaterial.SetColor("_FogColor", TabzHazeColor);
        fogMaterial.SetFloat("_FogStart", FogStarts[profile]);
        fogMaterial.SetFloat("_FogEnd", FogEnds[profile]);
        fogMaterial.SetFloat("_SkyFogStrength", SkyStrengths[profile]);
    }

    public string GetDiagnosticStatus()
    {
        bool depthRequested = targetCamera != null &&
            (targetCamera.depthTextureMode & DepthTextureMode.Depth) != 0;
        string shaderStatus = !shaderFound ? "não" :
            (fogMaterial != null && fogMaterial.shader.isSupported ? "OK" : "erro");
        string renderStatus = renderImageCalls > 0 ? renderImageCalls.ToString() + " chamadas" : "NÃO chamado";
        string errorStatus = string.IsNullOrEmpty(initializationError) ? "nenhum" : initializationError;
        return "Câmera: " + (targetCamera != null ? "OK" : "ausente") +
            " | Shader: " + shaderStatus +
            " | Depth: " + (depthRequested ? "solicitado" : "não") +
            " | Render: " + renderStatus +
            " | Erro: " + errorStatus;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        renderImageCalls++;
        if (!fogEnabled || fogMaterial == null)
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