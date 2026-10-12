using UnityEngine;

/// <summary>
/// Mobile-friendly reconstruction of TABZ's original sunnyDay sky and
/// atmospheric fog. The original skybox material and its serialized colour
/// values are preserved. AssetRipper exported the original gradient shader
/// as a white dummy, so the gradient shader in this project is reconstructed
/// from the material's exposed properties.
/// The original DeepSky Haze shader's functional ShaderLab/HLSL source was not
/// recoverable as a Unity asset. MobileDistanceFog uses the recovered serialized
/// contexts with a low-cost depth-based geometry approximation.
/// </summary>
public class OriginalTabzEnvironment : MonoBehaviour
{
    private const string SkyboxResourcePath = "Environment/sunnyDay";

    private void Awake()
    {
        Material skybox = Resources.Load<Material>(SkyboxResourcePath);
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }
        else
        {
            Debug.LogWarning("OriginalTabzEnvironment: original sunnyDay skybox material was not found.");
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(1.5f, 1.2838235f, 0.95955884f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        RenderSettings.ambientIntensity = 1f;

        // The prototype uses MobileDistanceFog on the camera. Disable Unity's
        // built-in fog so the two effects do not stack.
        RenderSettings.fog = false;

        DynamicGI.UpdateEnvironment();
    }
}