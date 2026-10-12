using System;
using UnityEngine;

/// <summary>
/// Bridges the original AK-74 visual to the recovered TABZ v5.1 physics rig.
/// The weapon is driven by the original Rifle aim and ReloadAim targets; both
/// hands are connected to the weapon through ConfigurableJoints by the arms rig.
/// </summary>
public class OriginalWeaponVisualBridge : MonoBehaviour
{
    [Header("Original TABZ asset")]
    [Tooltip("Resources path without extension, e.g. items/Ak74")]
    public string resourcesPrefabPath = "items/Ak74";
    public Camera viewCamera;
    public GameObject placeholderToHide;

    [Header("Original WeaponHandler forces")]
    [Tooltip("AK-74 mReloadTime is 1.5 seconds in the v5.1 player prefab.")]
    public float reloadPositionForce = 5f;
    public float reloadRotationForce = 100f;
    public float weaponPositionForce = 15f;
    public float weaponRotationForce = 200f;

    private GameObject spawnedWeapon;
    private Transform weaponVisualTransform;
    private Rigidbody weaponBody;
    private OriginalTabzArmsRig armsRig;
    private Transform reloadAim;
    private Transform rifleAim;
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

        // The Resources item prefab contains the original AK mesh hierarchy.
        // The online Weapon component is intentionally not carried into this
        // offline prototype; the physical root is rebuilt from player-prefab data.
        spawnedWeapon = Instantiate(prefab, transform, false);
        spawnedWeapon.name = "TABZ Original AK-74 (offline physics)";
        weaponVisualTransform = spawnedWeapon.transform;
        weaponVisualTransform.localPosition = Vector3.zero;
        weaponVisualTransform.localRotation = Quaternion.identity;
        weaponVisualTransform.localScale = Vector3.one;

        Component[] components = spawnedWeapon.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || component is Transform || component is MeshFilter ||
                component is MeshRenderer || component is SkinnedMeshRenderer ||
                component is Animator || component is Animation || component is ParticleSystem ||
                component is ParticleSystemRenderer || component is TrailRenderer ||
                component is LineRenderer) continue;

            if (component is Collider collider)
            {
                collider.enabled = false;
                continue;
            }

            if (component is Rigidbody existingBody)
            {
                if (existingBody.gameObject == spawnedWeapon) weaponBody = existingBody;
                else existingBody.isKinematic = true;
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

        if (weaponBody == null) weaponBody = spawnedWeapon.AddComponent<Rigidbody>();
        weaponBody.mass = 2f;
        weaponBody.drag = 0f;
        weaponBody.angularDrag = 0.05f;
        weaponBody.useGravity = true;
        weaponBody.isKinematic = false;
        weaponBody.constraints = RigidbodyConstraints.None;

        armsRig = gameObject.AddComponent<OriginalTabzArmsRig>();
        armsRig.Initialize(viewCamera, weaponBody, weaponVisualTransform);
        reloadAim = armsRig.ReloadAim;
        rifleAim = armsRig.RifleAim;

        if (placeholderToHide != null) placeholderToHide.SetActive(false);
        Debug.Log("OriginalWeaponVisualBridge: " + GetDiagnosticStatus());
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

    private void Update()
    {
        if (weaponBody == null || viewCamera == null || weaponVisualTransform == null) return;

        if (isReloadMoving && Time.time < reloadMoveUntil)
        {
            // WeaponHandler.ReloadMoveRigCoroutine uses Time.deltaTime here.
            MoveRig(weaponBody, reloadAim, reloadPositionForce, reloadRotationForce, true, true);
        }
        else
        {
            isReloadMoving = false;
            MoveWeaponToOriginalAim();
        }

        if (recoilStartTime < 0f || isReloadMoving) return;
        float elapsed = Time.time - recoilStartTime;
        float normalizedTime = Mathf.Clamp01(elapsed / OriginalAk74ForceShakeTime);
        float curveValue = originalAk74RecoilCurve.Evaluate(normalizedTime);
        weaponBody.AddTorque(-viewCamera.transform.right *
            originalAk74ForceShake.x * curveValue, ForceMode.VelocityChange);

        if (elapsed >= OriginalAk74ForceShakeTime) recoilStartTime = -1f;
    }

    private void MoveWeaponToOriginalAim()
    {
        if (rifleAim == null) return;

        // WeaponHandler.MoveWeapon's extra up-axis alignment torque.
        float angle = Vector3.Angle(weaponBody.transform.up, rifleAim.up);
        Vector3 axis = Vector3.Cross(weaponBody.transform.up, rifleAim.up);
        weaponBody.AddTorque(axis * angle * weaponRotationForce * 0.0166f,
            ForceMode.VelocityChange);

        // Original player prefab: weaponPositionForce=15, AK-74 weaponRotationForce=200.
        MoveRig(weaponBody, rifleAim, weaponPositionForce, weaponRotationForce, false, false);
    }

    // Original WeaponHandler.MoveRig force/torque structure.
    private void MoveRig(Rigidbody rig, Transform target, float force, float torque,
        bool fixer, bool useFrameDelta)
    {
        float delta = useFrameDelta ? Time.deltaTime : 0.0166f;
        Vector3 forward = rig.transform.forward;
        if (fixer && rig.transform.childCount > 0)
            forward = rig.transform.GetChild(0).forward;
        float angle = Vector3.Angle(forward, target.forward);
        Vector3 axis = Vector3.Cross(forward, target.forward);
        rig.AddTorque(axis * angle * torque * delta, ForceMode.VelocityChange);
        rig.AddForce((target.position - rig.position) * force, ForceMode.VelocityChange);
    }

    public string GetDiagnosticStatus()
    {
        return "AK Rigidbody: " + (weaponBody != null ? "OK" : "ausente") +
            " | Aim: " + (rifleAim != null ? "OK" : "ausente") +
            " | ReloadAim: " + (reloadAim != null ? "OK" : "ausente") +
            " | Recarga física: " + (isReloadMoving ? "ativa" : "inativa") +
            " | " + (armsRig != null ? armsRig.GetDiagnosticStatus() : "rig de braços ausente");
    }

    private void OnDestroy()
    {
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
        if (armsRig != null) Destroy(armsRig);
    }
}
