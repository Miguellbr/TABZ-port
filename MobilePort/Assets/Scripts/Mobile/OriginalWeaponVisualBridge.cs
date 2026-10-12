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
    // The AK mesh AABB extends 0.68 units along its long axis. In this prefab,
    // that axis is rotated into depth and its child origin is at z=0.11185449.
    // With the camera near plane at 0.05, z=0.62 keeps the rear extent just
    // beyond the near plane instead of slicing through the mesh.
    public Vector3 localPosition = new Vector3(0.28f, -0.24f, 0.62f);

    // The AK mesh's long axis is local X. Its prefab child transform rotates that
    // axis onto local +Z, which is the camera's forward direction. An extra 180°
    // yaw here reverses it toward the camera, so keep the mount rotation neutral.
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Tooltip("If true, hides the prototype's placeholder weapon when the original model loads.")]
    public GameObject placeholderToHide;

    private GameObject spawnedWeapon;
    private Transform weaponVisualTransform;
    private Quaternion weaponBaseRotation;
    private float recoilStartTime = -1f;

    // Recovered from the original TABS_Player_Network prefab's CameraMovement.forceCurve.
    // AK-74 settings: forceShake=(5,0,0), forceShakeTime=0.3.
    private readonly AnimationCurve originalAk74RecoilCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 15.573248f, 15.573248f),
        new Keyframe(0.08486544f, 0.5940055f, 1.4796853f, 1.4796853f),
        new Keyframe(0.4710793f, 0.34228253f, -1.4810432f, -1.4810432f),
        new Keyframe(1f, 0f, 0f, 0f));
    private readonly Vector3 originalAk74ForceShake = new Vector3(5f, 0f, 0f);
    private const float OriginalAk74ForceShakeTime = 0.3f;

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
        weaponVisualTransform = t;
        weaponBaseRotation = t.localRotation;

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

    /// <summary>
    /// Plays the recovered AK-74 transient recoil curve on the first-person weapon.
    /// TABZ applies this force to a camera-force child; this offline port applies
    /// the same curve to the weapon visual because it has no original camera rig.
    /// </summary>
    public void PlayFireRecoil()
    {
        if (weaponVisualTransform == null) return;
        recoilStartTime = Time.time;
    }

    private void Update()
    {
        if (weaponVisualTransform == null || recoilStartTime < 0f) return;

        float elapsed = Time.time - recoilStartTime;
        float normalizedTime = Mathf.Clamp01(elapsed / OriginalAk74ForceShakeTime);
        float curveValue = originalAk74RecoilCurve.Evaluate(normalizedTime);
        weaponVisualTransform.localRotation =
            weaponBaseRotation * Quaternion.Euler(-originalAk74ForceShake * curveValue);

        if (elapsed >= OriginalAk74ForceShakeTime)
        {
            weaponVisualTransform.localRotation = weaponBaseRotation;
            recoilStartTime = -1f;
        }
    }

    private void OnDestroy()
    {
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
    }
}
