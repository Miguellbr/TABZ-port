using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reports the graphics capabilities relevant to a depth-based mobile fog
/// effect. This component only diagnoses support; it does not change quality
/// settings or enable an image effect.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MobileFogDiagnostics : MonoBehaviour
{
    private void Start()
    {
        Camera targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.Depth;

        Debug.Log(
            "[MobileFogDiagnostics] " +
            "Device=" + SystemInfo.deviceModel +
            "; GraphicsAPI=" + SystemInfo.graphicsDeviceType +
            "; ShaderLevel=" + SystemInfo.graphicsShaderLevel +
            "; ImageEffects=" + SystemInfo.supportsImageEffects +
            "; RenderTextures=" + SystemInfo.supportsRenderTextures +
            "; RFloat=" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) +
            "; ARGBHalf=" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) +
            "; Depth=" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth) +
            "; MSAA=" + QualitySettings.antiAliasing +
            "; CameraDepthMode=" + targetCamera.depthTextureMode);
    }
}
