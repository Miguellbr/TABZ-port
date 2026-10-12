using UnityEngine;

/// <summary>
/// Data-driven reconstruction of the serialized TABZ DeepSky Haze contexts.
/// Values and animation-curve keys are taken from the recovered TABZ_World scene.
/// This is context data, not a port of the original multi-pass DeepSky shader.
/// </summary>
public static class TabzHazeContextData
{
    public enum ZoneKind
    {
        General,
        Snow2,
        LightHouse,
        Snow,
        Darkness
    }

    public struct Settings
    {
        public float extinction;
        public float heightFalloff;
        public Color ambient;
        public Color lightColour;
        public float opacity;
        public float startDistance;
        public float scatteringScale;
        public float scatteringDirection;

        public static Settings Lerp(Settings a, Settings b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Settings
            {
                extinction = Mathf.Lerp(a.extinction, b.extinction, t),
                heightFalloff = Mathf.Lerp(a.heightFalloff, b.heightFalloff, t),
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                lightColour = Color.Lerp(a.lightColour, b.lightColour, t),
                opacity = Mathf.Lerp(a.opacity, b.opacity, t),
                startDistance = Mathf.Lerp(a.startDistance, b.startDistance, t),
                scatteringScale = Mathf.Lerp(a.scatteringScale, b.scatteringScale, t),
                scatteringDirection = Mathf.Lerp(a.scatteringDirection, b.scatteringDirection, t)
            };
        }
    }

    public struct ZoneInfo
    {
        public ZoneKind kind;
        public int priority;
        public float blendRange;

        public ZoneInfo(ZoneKind zone, int zonePriority, float range)
        {
            kind = zone;
            priority = zonePriority;
            blendRange = range;
        }
    }

    private static readonly Color FogLight =
        new Color(1f, 0.6653145f, 0.44852942f, 1f);

    private static Settings Make(float extinction, float heightFalloff, Color ambient)
    {
        return new Settings
        {
            extinction = extinction * 0.01f,
            heightFalloff = heightFalloff,
            ambient = ambient,
            lightColour = FogLight,
            opacity = 1f,
            startDistance = 0f,
            scatteringScale = 0f,
            scatteringDirection = 0.7f
        };
    }

    public static ZoneInfo GetZoneInfo(ZoneKind zone)
    {
        switch (zone)
        {
            case ZoneKind.Snow2: return new ZoneInfo(zone, 100, 0.531f);
            case ZoneKind.LightHouse: return new ZoneInfo(zone, 250, 0.1f);
            case ZoneKind.Snow: return new ZoneInfo(zone, 250, 0.531f);
            case ZoneKind.Darkness: return new ZoneInfo(zone, 250, 0.05f);
            default: return new ZoneInfo(ZoneKind.General, 0, 0.1f);
        }
    }

    public static Settings Evaluate(ZoneKind zone, float normalizedTime)
    {
        normalizedTime = Mathf.Repeat(normalizedTime, 1f);

        Color generalDay = new Color(0.6911765f, 0.93610543f, 1f, 1f);
        Color generalWarm = new Color(1f, 0.7380324f, 0.53676474f, 1f);
        Color generalSunset = new Color(1f, 0.42647058f, 0.50953335f, 1f);
        Color snowDay = new Color(0.875f, 0.9741379f, 1f, 1f);
        Color snowWarm = new Color(1f, 0.92099386f, 0.8602941f, 1f);
        Color snowSunset = new Color(1f, 0.78676474f, 0.81764704f, 1f);

        Settings day;
        Settings warm;
        Settings sunset;
        Settings night;

        switch (zone)
        {
            case ZoneKind.Snow2:
                day = Make(3f, 0.0001f, snowDay);
                warm = Make(3f, 0.0001f, snowWarm);
                sunset = Make(3f, 0.0001f, snowSunset);
                night = Make(4.75f, 0.0001f,
                    new Color(0.4705882f, 0.4705882f, 0.4705882f, 1f));
                break;

            case ZoneKind.LightHouse:
                day = Make(5f, 0.0001f, generalDay);
                warm = Make(5f, 0.0001f, generalWarm);
                sunset = Make(5f, 0.0001f, generalSunset);
                night = Make(4.75f, 0.0001f,
                    new Color(0.25f, 0.25f, 0.25f, 1f));
                break;

            case ZoneKind.Snow:
                day = Make(5f, 0.0001f, snowDay);
                warm = Make(5f, 0.0001f, snowWarm);
                sunset = Make(5f, 0.0001f, snowSunset);
                night = Make(4.75f, 0.0001f,
                    new Color(0.4705882f, 0.4705882f, 0.4705882f, 1f));
                break;

            case ZoneKind.Darkness:
                return Make(8f, 0.0001f,
                    new Color(1f, 0.11764705f, 0.11764705f, 1f));

            default:
                day = Make(5f, 0.015f, generalDay);
                warm = Make(5f, 0.015f, generalWarm);
                sunset = Make(5f, 0.015f, generalSunset);
                night = Make(4.75f, 0.0001f,
                    new Color(0.25f, 0.25f, 0.25f, 1f));
                break;
        }

        // DS_HazeContext.GetContextItemBlended uses MyDay as its base,
        // then lerps the remaining items in serialized order by each curve.
        Settings result = day;
        result = Settings.Lerp(result, warm, EvaluateWarmCurve(normalizedTime));
        result = Settings.Lerp(result, sunset, EvaluateSunsetCurve(normalizedTime));
        result = Settings.Lerp(result, night, EvaluateNightCurve(normalizedTime));
        return result;
    }

    private static float EvaluateWarmCurve(float t)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f, 14.749998f, 14.749998f),
            new Keyframe(0.15f, 1f, 0f, 0f),
            new Keyframe(0.35f, 0f, -0.024193546f, -0.024193546f),
            new Keyframe(1f, 0f, 0.016614795f, 0.016614795f));
        return curve.Evaluate(t);
    }

    private static float EvaluateSunsetCurve(float t)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.012499999f, 0f, 0f),
            new Keyframe(0.65000004f, 0f, 0f, -0.02970297f),
            new Keyframe(0.8666687f, 0.97500074f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, -8.399995f));
        return curve.Evaluate(t);
    }

    private static float EvaluateNightCurve(float t)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0.02848094f),
            new Keyframe(0.1f, 0f, 0f, 0f),
            new Keyframe(0.90000004f, 0.0000011436641f, 0f, -0.0000035189667f),
            new Keyframe(1f, 1f, 0f, 0f));
        return curve.Evaluate(t);
    }
}

/// <summary>
/// Optional marker for a recovered map zone. Assign a collider matching the
/// original zone volume when that geometry is available. No bounds are
/// fabricated for the current flat offline test scene.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TabzHazeZone : MonoBehaviour
{
    public TabzHazeContextData.ZoneKind zone = TabzHazeContextData.ZoneKind.General;

    public bool Contains(Vector3 worldPosition)
    {
        Collider zoneCollider = GetComponent<Collider>();
        return zoneCollider != null && zoneCollider.bounds.Contains(worldPosition);
    }
}
