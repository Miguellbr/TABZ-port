using UnityEngine;

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
        if (zoneCollider == null || !zoneCollider.enabled || !zoneCollider.gameObject.activeInHierarchy)
            return false;

        Vector3 closest = zoneCollider.ClosestPoint(worldPosition);
        return (closest - worldPosition).sqrMagnitude <= 0.0001f;
    }
}
