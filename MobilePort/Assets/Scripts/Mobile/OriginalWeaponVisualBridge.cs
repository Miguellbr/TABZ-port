using System;
using UnityEngine;

/// <summary>
/// Offline bridge for the original TABZ weapon mesh. Reconstructs the original
/// WeaponHandler reload movement: a Rigidbody is driven toward the recovered
/// ReloadAimTag with the same MoveRig force/torque calculation.
/// Original arm bodies/joints are a separate integration step.
/// </summary>
public class OriginalWeaponVisualBridge : MonoBehaviour
{
    [Header("Original TABZ asset")]
    [Tooltip("Resources path without extension, e.g. items/Ak74")]
    public string resourcesPrefabPath = "items/Ak74";

    [Header("Recovered AK-74 first-person mount")]
    public Camera viewCamera;
    public Vector3 localPosition = new Vector3(0.28f, -0.24f, 0.62f);
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;
    public GameObject placeholderToHide;

    [Header("Physics reload")]
    [Tooltip("Original WeaponHandler uses MoveRig(weapon, reloadAim, 5, 100, true, true).")]
    public float reloadPositionForce = 5f;
    public float reloadRotationForce = 100f;
    [Tooltip("Recovered ReloadAimTag local position in Aims, transformed through the original CameraRot 180-degree yaw.")]
    public Vector3 reloadAimLocalPosition = new Vector3(-0.214f, -0.72f, -0.471f);
    [Tooltip("Original ReloadAimTag rotation composed with CameraRot's 180-degree yaw.")]
    public Vector4 reloadAimLocalRotation = new Vector4(-0.2595866f, 0.048887484f, -0.013975345f, -0.9643804f);

    private GameObject spawnedWeapon;
    private Transform weaponVisualTransform;
    private Rigidbody weaponBody;
    private Transform reloadAim;
    private Quaternion weaponBaseRotation;
    private float recoilStartTime = -1f;
    private bool isReloadMoving;
    private float reloadMoveUntil;

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
            Debug.LogWarning("OriginalWeaponVisualBridge: no view camera assigned.");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(resourcesPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("OriginalWeaponVisualBridge: Resources/" + resourcesPrefabPath + " not found.");
            return;
        }

        spawnedWeapon = Instantiate(prefab, viewCamera.transform, false);
        spawnedWeapon.name = "TABZ Original Weapon Visual (offline physics)";
        weaponVisualTransform = spawnedWeapon.transform;
        weaponVisualTransform.localPosition = localPosition;
        weaponVisualTransform.localRotation = Quaternion.Euler(localEulerAngles);
        weaponVisualTransform.localScale = localScale;
        weaponBaseRotation = weaponVisualTransform.localRotation;

        // Strip online/gameplay components but preserve the original render hierarchy.
        Component[] components = spawnedWeapon.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || component is Transform || component is MeshFilter ||
                component is MeshRenderer || component is SkinnedMeshRenderer ||
                component is Animator || component is Animation || component is ParticleSystem ||
                component is ParticleSystemRenderer || component is TrailRenderer ||
                component is LineRenderer) continue;

            if (component is Collider collider) { collider.enabled = false; continue; }
            if (component is Rigidbody existingBody)
            {
                existingBody.isKinematic = true;
                if (existingBody.gameObject == spawnedWeapon) weaponBody = existingBody;
                continue;
            }

            if (component is MonoBehaviour)
            {
                Type type = component.GetType();
                string ns = type.Namespace ?? string.Empty;
                string typeName = type.Name;
                bool remove =
                    ns.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ns.IndexOf("FMOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("InventoryItem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("FirePoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("ADS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("NoiseSpawner", StringComparison.OrdinalIgnoreCase) >= 0;
                if (remove) Destroy(component);
            }
        }

        // The original AK root has a Rigidbody. Ensure the visual can be moved
        // by the same force/torque path even if the imported component was stripped.
        if (weaponBody == null) weaponBody = spawnedWeapon.AddComponent<Rigidbody>();
        weaponBody.isKinematic = false;
        weaponBody.useGravity = false;
        weaponBody.mass = 0.25f;
        weaponBody.drag = 0f;
        weaponBody.angularDrag = 0.05f;
        weaponBody.interpolation = RigidbodyInterpolation.Interpolate;
        weaponBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Keep the rigidbody in player space, not parented to the camera. Its
        // normal target is updated from the camera each physics tick.
        Transform playerRoot = transform;
        spawnedWeapon.transform.SetParent(playerRoot, true);
        CreateRecoveredReloadAim();

        if (placeholderToHide != null) placeholderToHide.SetActive(false);
        Debug.Log("OriginalWeaponVisualBridge: loaded AK visual and physics reload bridge.");
    }

    private void CreateRecoveredReloadAim()
    {
        GameObject aim = new GameObject("Recovered TABZ ReloadAimTag");
        reloadAim = aim.transform;
        // Original hierarchy: PlayerRoot/CameraHolder(y=2.1135998)/CameraRot(180° Y)/CameraForce/Aims/RELOAD.
        // reloadAimLocalPosition already includes CameraRot's 180° yaw; add the recovered holder height.
        reloadAim.SetParent(transform, false);
        reloadAim.localPosition = new Vector3(reloadAimLocalPosition.x,
            2.1135998f + reloadAimLocalPosition.y, reloadAimLocalPosition.z);
        reloadAim.localRotation = new Quaternion(
            reloadAimLocalRotation.x, reloadAimLocalRotation.y,
            reloadAimLocalRotation.z, reloadAimLocalRotation.w);
        reloadAim.localScale = Vector3.one;
    }

    public void PlayFireRecoil()
    {
        if (weaponVisualTransform == null) return;
        recoilStartTime = Time.time;
    }

    public void BeginPhysicsReload(float duration)
    {
        if (weaponBody == null || reloadAim == null) return;
        isReloadMoving = true;
        reloadMoveUntil = Time.time + Mathf.Max(0.1f, duration);
    }

    private void FixedUpdate()
    {
        if (weaponBody == null || viewCamera == null || weaponVisualTransform == null) return;

        if (isReloadMoving && Time.time < reloadMoveUntil)
        {
            MoveRig(weaponBody, reloadAim, reloadPositionForce, reloadRotationForce, true, true);
        }
        else
        {
            isReloadMoving = false;
            Vector3 mountPosition = viewCamera.transform.TransformPoint(localPosition);
            Quaternion mountRotation = viewCamera.transform.rotation * Quaternion.Euler(localEulerAngles);
            MoveRigToPose(weaponBody, mountPosition, mountRotation, 5f, 100f, false);
        }
    }

    // This is the original WeaponHandler.MoveRig calculation, retained verbatim
    // in its force/torque structure (including the first-child forward vector).
    private void MoveRig(Rigidbody rig, Transform target, float force, float torque, bool fixer, bool fixedTime)
    {
        float delta = fixedTime ? Time.fixedDeltaTime : 0.0166f;
        Vector3 forward = rig.transform.forward;
        if (fixer && rig.transform.childCount > 0)
            forward = rig.transform.GetChild(0).forward;
        float angle = Vector3.Angle(forward, target.forward);
        Vector3 axis = Vector3.Cross(forward, target.forward);
        rig.AddTorque(axis * angle * torque * delta, ForceMode.VelocityChange);
        Vector3 offset = target.position - rig.position;
        rig.AddForce(offset * force, ForceMode.VelocityChange);
    }

    private void MoveRigToPose(Rigidbody rig, Vector3 position, Quaternion rotation,
        float force, float torque, bool fixedTime)
    {
        float delta = fixedTime ? Time.fixedDeltaTime : 0.0166f;
        Vector3 forward = rig.transform.childCount > 0
            ? rig.transform.GetChild(0).forward : rig.transform.forward;
        Vector3 targetForward = rotation * Vector3.forward;
        float angle = Vector3.Angle(forward, targetForward);
        Vector3 axis = Vector3.Cross(forward, targetForward);
        rig.AddTorque(axis * angle * torque * delta, ForceMode.VelocityChange);
        rig.AddForce((position - rig.position) * force, ForceMode.VelocityChange);
    }

    private void Update()
    {
        if (weaponVisualTransform == null || recoilStartTime < 0f) return;
        // Recoil remains a recovered transient camera/weapon effect. It is
        // temporarily skipped while the rigidbody is performing the reload.
        if (isReloadMoving) return;

        float elapsed = Time.time - recoilStartTime;
        float normalizedTime = Mathf.Clamp01(elapsed / OriginalAk74ForceShakeTime);
        float curveValue = originalAk74RecoilCurve.Evaluate(normalizedTime);
        weaponBody.AddTorque(-viewCamera.transform.right *
            originalAk74ForceShake.x * curveValue, ForceMode.VelocityChange);

        if (elapsed >= OriginalAk74ForceShakeTime)
        {
            recoilStartTime = -1f;
        }
    }

    private void OnDestroy()
    {
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
        if (reloadAim != null) Destroy(reloadAim.gameObject);
    }
}
