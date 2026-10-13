using UnityEngine;

/// <summary>
/// Reproduction of the original DeepSky DS_HazeZone volume.
/// The original uses the Transform's localScale as its box size; it does not
/// use a Collider. Priority and boundary blending match DS_HazeZone.cs.
/// </summary>
public sealed class TabzHazeZone : MonoBehaviour
{
    public TabzHazeContextData.ZoneKind zone = TabzHazeContextData.ZoneKind.General;

    [Min(0)]
    public int priority;

    [Range(0.001f, 1f)]
    public float blendRange = 0.1f;

    public bool Contains(Vector3 worldPosition)
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return false;

        Vector3 point = transform.InverseTransformPoint(worldPosition);
        point = Vector3.Scale(point, transform.localScale);

        Vector3 extents = transform.localScale * 0.5f;
        return Mathf.Abs(point.x) <= extents.x &&
            Mathf.Abs(point.y) <= extents.y &&
            Mathf.Abs(point.z) <= extents.z;
    }

    public float GetBlendWeight(Vector3 worldPosition)
    {
        Vector3 point = transform.InverseTransformPoint(worldPosition);
        point = Vector3.Scale(point, transform.localScale);

        Vector3 extents = transform.localScale * 0.5f;
        float distanceToEdgeX = Mathf.Abs(extents.x - Mathf.Abs(point.x));
        float distanceToEdgeY = Mathf.Abs(extents.y - Mathf.Abs(point.y));
        float distanceToEdgeZ = Mathf.Abs(extents.z - Mathf.Abs(point.z));
        float distanceToEdge = Mathf.Min(distanceToEdgeX,
            Mathf.Min(distanceToEdgeY, distanceToEdgeZ));

        float blendDistance = Mathf.Max(
            Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z)) * blendRange,
            Mathf.Epsilon);

        return Mathf.Clamp01(distanceToEdge / blendDistance);
    }

    public float GetSizeScore()
    {
        Vector3 extents = transform.localScale * 0.5f;
        return Vector3.Dot(extents, extents);
    }

    public static int ComparePriorityAndSize(TabzHazeZone a, TabzHazeZone b)
    {
        int priorityOrder = a.priority.CompareTo(b.priority);
        if (priorityOrder != 0)
            return priorityOrder;

        return a.GetSizeScore().CompareTo(b.GetSizeScore());
    }
}
