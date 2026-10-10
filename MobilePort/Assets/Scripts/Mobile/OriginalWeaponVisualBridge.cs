using System;
using UnityEngine;

/// <summary>
/// Optional visual-only bridge for an original TABZ weapon prefab imported under
/// Resources/items. It deliberately does not use the original Weapon/Photon logic:
/// OfflinePrototypeWeapon remains responsible for local hitscan and ammunition.
/// </summary>
public class OriginalWeaponVisualBridge : MonoBehaviour
{
    [Header("Original TABZ asset")]
    [Tooltip("Resources path without extension, e.g. items/Ak74")]
    public string resourcesPrefabPath = "items/Ak74";

    [Header("First-person mount")]
    public Camera viewCamera;
    public Vector3 localPosition = new Vector3(0.28f, -0.24f, 0.48f);
    public Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
    public Vector3 localScale = Vector3.one;

    [Tooltip("If true, hides the prototype's placeholder weapon when the original model loads.")]
    public GameObject placeholderToHide;

    private GameObject spawnedWeapon;

    private void Start()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null)
        {
            Debug.LogWarning("OriginalWeaponVisualBridge: no view camera assigned; keeping prototype weapon.");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(resourcesPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("OriginalWeaponVisualBridge: Resources/" + resourcesPrefabPath +
                " was not found. Import the recovered TABZ prefab and its mesh/material dependencies to enable it.");
            return;
        }

        spawnedWeapon = Instantiate(prefab, viewCamera.transform, false);
        spawnedWeapon.name = "TABZ Original Weapon Visual (offline)";
        Transform t = spawnedWeapon.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = localScale;

        // The recovered prefab includes colliders and network/gameplay components.
        // Keep its render hierarchy only; local combat is handled by OfflinePrototypeWeapon.
        Component[] components = spawnedWeapon.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || component is Transform || component is MeshFilter ||
                component is MeshRenderer || component is SkinnedMeshRenderer ||
                component is Animator || component is Animation || component is ParticleSystem ||
                component is ParticleSystemRenderer || component is TrailRenderer ||
                component is LineRenderer)
                continue;

            if (component is Collider || component is Rigidbody)
            {
                if (component is Collider collider) collider.enabled = false;
                if (component is Rigidbody body) body.isKinematic = true;
                continue;
            }

            // Remove MonoBehaviours from the original game/online stack without a hard
            // reference to Photon or FMOD assemblies, which are not part of this prototype.
            if (component is MonoBehaviour)
            {
                Type type = component.GetType();
                string ns = type.Namespace ?? string.Empty;
                string typeName = type.Name;
                bool networkOrOriginalGameplay =
                    ns.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ns.IndexOf("FMOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("InventoryItem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("FirePoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("ADS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("NoiseSpawner", StringComparison.OrdinalIgnoreCase) >= 0;
                if (networkOrOriginalGameplay) Destroy(component);
            }
        }

        if (placeholderToHide != null) placeholderToHide.SetActive(false);
        Debug.Log("OriginalWeaponVisualBridge: loaded visual prefab Resources/" + resourcesPrefabPath);
    }

    private void OnDestroy()
    {
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
    }
}
