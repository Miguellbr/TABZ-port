using UnityEngine;

/// <summary>
/// Mobile-friendly reconstruction of TABZ's original sunnyDay sky and
/// atmospheric fog. The original skybox material and its serialized colour
/// values are preserved. AssetRipper exported the original gradient shader
/// as a white dummy, so the gradient shader in this project is reconstructed
/// from the material's exposed properties.
/// The original DeepSky Haze image-effect shader was not present in the export;
/// exponential fog is therefore a lightweight mobile fallback using the
/// original MyDay extinction coefficient and fog ambient colour.
/// </summary>
public class OriginalTabzEnvironment : MonoBehaviour
{
    private const string SkyboxResourcePath = "Environment/sunnyDay";

    // Original TABZ MyDay context: m_FogExtinctionScale = 5.
    // DS_HazeView scales that value by 0.01 before passing it to the haze shader.
    private const float OriginalFogExtinction = 5f * 0.01f;

    private static readonly Color OriginalFogAmbient =
        new Color(0.6911765f, 0.93610543f, 1f, 1f);

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

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = OriginalFogAmbient;
        RenderSettings.fogDensity = OriginalFogExtinction;

        DynamicGI.UpdateEnvironment();
    }
}