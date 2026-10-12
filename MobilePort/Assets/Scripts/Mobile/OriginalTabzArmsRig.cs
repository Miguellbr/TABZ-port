using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rebuilds the original TABZ v5.1 first-person arm rig from recovered prefab
/// data. The original character and camera both had a 180-degree yaw in their
/// parent chain; this prototype camera is normalized to forward +Z, so the
/// matching yaw is omitted here while all local bone/joint data is retained.
/// </summary>
public sealed class OriginalTabzArmsRig : MonoBehaviour
{
    private const float RigScale = 0.3617785f;
    private const float ArmTriangleWeightThreshold = 0.4f;

    public Transform ReloadAim { get; private set; }
    public Transform RifleAim { get; private set; }

    private Transform rigRoot;
    private Transform rightIdle;
    private Transform leftIdle;
    private Rigidbody rightHandBody;
    private Rigidbody leftHandBody;
    private ConfigurableJoint rightWeaponJoint;
    private ConfigurableJoint leftWeaponJoint;
    private Mesh armMesh;
    private string visualStatus = "não inicializado";

    public void Initialize(Camera camera, Rigidbody weaponBody, Transform weaponRoot)
    {
        if (camera == null || weaponBody == null || weaponRoot == null)
        {
            visualStatus = "faltam referências da câmera/arma";
            Debug.LogError("OriginalTabzArmsRig: " + visualStatus);
            return;
        }

        CreateCameraTargets(camera.transform);
        CreatePhysicsRig();
        CreateArmVisual();
        CreateWeaponHandTags(weaponRoot);
        AttachHandsToWeapon(weaponBody, weaponRoot);
        visualStatus = "braços originais filtrados do mesh; juntas físicas conectadas";
        Debug.Log("OriginalTabzArmsRig: " + GetDiagnosticStatus());
    }

    private void CreateCameraTargets(Transform cameraTransform)
    {
        GameObject rifle = new GameObject("Original TABZ Rifle Aim");
        RifleAim = rifle.transform;
        RifleAim.SetParent(cameraTransform, false);
        RifleAim.localPosition = new Vector3(0.14f, -0.28f, 0.5f);
        RifleAim.localRotation = new Quaternion(0f, 0f, 0.026176924f, 0.99965733f);
        RifleAim.localScale = Vector3.one * RigScale;

        GameObject reload = new GameObject("Original TABZ ReloadAimTag");
        ReloadAim = reload.transform;
        ReloadAim.SetParent(cameraTransform, false);
        ReloadAim.localPosition = new Vector3(0.214f, -0.72f, 0.471f);
        ReloadAim.localRotation = new Quaternion(0.2595866f, 0.048887484f, 0.013975345f, 0.9643804f);
        ReloadAim.localScale = Vector3.one * RigScale;
    }

    private void CreatePhysicsRig()
    {
        GameObject root = new GameObject("Original TABZ Arms Rig");
        rigRoot = root.transform;
        rigRoot.SetParent(transform, false);
        rigRoot.localPosition = Vector3.zero;
        rigRoot.localRotation = Quaternion.identity;
        rigRoot.localScale = Vector3.one * RigScale;

        Transform armature = CreateChild(rigRoot, "Armature", Vector3.zero,
            new Quaternion(0.7071068f, 0f, 0f, -0.70710677f), Vector3.one);

        Transform hip = CreateChild(armature, "hip",
            new Vector3(0f, 0.12968852f, 2.947054f),
            new Quaternion(0.54099184f, 0.5409918f, -0.45533267f, 0.4553327f), Vector3.one);
        Rigidbody hipBody = AddBody(hip, 30f, 5f, 5f, true);

        Transform torso = CreateChild(armature, "torso",
            new Vector3(0f, -0.0021936893f, 3.7082458f),
            new Quaternion(0.4974715f, 0.49747145f, -0.50251585f, 0.5025158f), Vector3.one);
        Rigidbody torsoBody = AddBody(torso, 20f, 5f, 5f, true);
        ConfigureCharacterJoint(torso.gameObject.AddComponent<CharacterJoint>(), hipBody,
            Vector3.up, Vector3.up, new Vector3(-0.7725424f, 0.0000030696392f, 0.000018239021f),
            10000f, -20f, 20f, 0f, 0f);

        Transform holder = CreateChild(torso, "Holder",
            new Vector3(-0.95053154f, 0f, -0.000027179718f), Quaternion.identity, Vector3.one);
        Transform head = CreateChild(holder, "head", Vector3.zero, Quaternion.identity, Vector3.one);

        Transform armLeft = CreateChild(armature, "Arm_Left",
            new Vector3(-0.30991465f, 0.047909737f, 4.4283676f),
            new Quaternion(0.6997459f, -0.016339839f, 0.009785423f, 0.714138f), Vector3.one);
        Rigidbody armLeftBody = AddBody(armLeft, 2f, 0f, 0.05f, true);
        ConfigureCharacterJoint(armLeft.gameObject.AddComponent<CharacterJoint>(), torsoBody,
            Vector3.forward, Vector3.up, new Vector3(-0.7205907f, -0.30991465f, -0.04283583f),
            1000f, -90f, 60f, 140f, 140f);

        Transform handLeft = CreateChild(armature, "Hand_Left",
            new Vector3(-1.459158f, 0.058022022f, 4.385791f),
            new Quaternion(0.000042080876f, -0.018698407f, 0.00000089406956f, 0.9998252f), Vector3.one);
        leftHandBody = AddBody(handLeft, 2f, 0f, 0.05f, true);
        ConfigureCharacterJoint(handLeft.gameObject.AddComponent<CharacterJoint>(), armLeftBody,
            Vector3.forward, Vector3.up, new Vector3(-1.1500763f, 0.000011026859f, 0.000113509595f),
            1000f, -90f, 0f, 0f, 0f);
        CreateHandMarkers(handLeft, true);

        Transform armRight = CreateChild(armature, "Arm_Right",
            new Vector3(0.30485964f, 0.04791546f, 4.455677f),
            new Quaternion(-0.025241703f, 0.6994643f, -0.713981f, -0.01853894f), Vector3.one);
        Rigidbody armRightBody = AddBody(armRight, 2f, 0f, 0.05f, true);
        ConfigureCharacterJoint(armRight.gameObject.AddComponent<CharacterJoint>(), torsoBody,
            Vector3.forward, Vector3.down, new Vector3(-0.74789876f, 0.30485964f, -0.04256618f),
            1000f, -90f, 60f, 140f, 140f);

        Transform handRight = CreateChild(armature, "Hand_Right",
            new Vector3(1.3878003f, 0.057373047f, 4.3884354f),
            new Quaternion(-0.018698426f, 0.000042316144f, -0.99982524f, -0.0000008045897f), Vector3.one);
        rightHandBody = AddBody(handRight, 2f, 0f, 0.05f, true);
        ConfigureCharacterJoint(handRight.gameObject.AddComponent<CharacterJoint>(), armRightBody,
            Vector3.back, Vector3.up, new Vector3(-1.0850682f, 0.0000035837293f, 0.00013267994f),
            1000f, -90f, 0f, 0f, 0f);
        CreateHandMarkers(handRight, false);

        rightIdle = CreateChild(torso, "RightIdle",
            new Vector3(0.63f, 0.69f, 0.21f),
            new Quaternion(-0.27388707f, -0.2738925f, -0.6519072f, -0.65190935f), Vector3.one);
        leftIdle = CreateChild(torso, "LeftIdle",
            new Vector3(0.63f, -0.69f, 0.21f),
            new Quaternion(-0.27388707f, -0.2738925f, -0.6519072f, -0.65190935f), Vector3.one);

        // The original hip was joined to the player's main Rigidbody. The
        // offline controller uses CharacterController, so a kinematic anchor
        // replaces only that outermost connection; the original ragdoll joints
        // from torso through both hands remain CharacterJoints.
        GameObject anchorObject = new GameObject("Original TABZ Player Rig Anchor");
        anchorObject.transform.SetParent(transform, false);
        Rigidbody anchorBody = anchorObject.AddComponent<Rigidbody>();
        anchorBody.isKinematic = true;
        anchorBody.useGravity = false;
        ConfigurableJoint rootJoint = hip.gameObject.AddComponent<ConfigurableJoint>();
        rootJoint.connectedBody = anchorBody;
        rootJoint.autoConfigureConnectedAnchor = true;
        LockJoint(rootJoint);

        CreateBone(armature, "Leg_Left_Upper",
            new Vector3(-0.22573039f, 0.04791093f, 2.7232938f),
            new Quaternion(0.499247f, -0.5061902f, 0.49738976f, 0.49711907f));
        CreateBone(armature, "Leg_Left_Lower",
            new Vector3(-0.21645255f, 0.061745286f, 1.4539939f),
            new Quaternion(0.4997668f, 0.49991876f, 0.49985588f, -0.50045836f));
        CreateBone(armature, "Leg_Right_Upper",
            new Vector3(0.2257339f, 0.04791498f, 2.7233038f),
            new Quaternion(0.50331086f, -0.5004727f, 0.49668872f, 0.49950534f));
        CreateBone(armature, "Leg_Right_Lower",
            new Vector3(0.21847834f, 0.057531476f, 1.4539407f),
            new Quaternion(0.5013649f, 0.49862948f, 0.50134826f, -0.49865013f));

        // Keep references alive and make the intended physics chain explicit.
        _ = head;
        _ = torsoBody;
    }

    private void CreateArmVisual()
    {
        Mesh sourceMesh = Resources.Load<Mesh>("OriginalRig/RealHumanBean_0");
        Material skinMaterial = Resources.Load<Material>("OriginalRig/redd");
        if (sourceMesh == null || skinMaterial == null)
        {
            visualStatus = "mesh/material original não carregado";
            Debug.LogError("OriginalTabzArmsRig: " + visualStatus);
            return;
        }

        BoneWeight[] weights = sourceMesh.boneWeights;
        int[] sourceTriangles = sourceMesh.triangles;
        List<int> armTriangles = new List<int>(sourceTriangles.Length);
        for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
        {
            float score = (ArmWeight(weights[sourceTriangles[i]]) +
                           ArmWeight(weights[sourceTriangles[i + 1]]) +
                           ArmWeight(weights[sourceTriangles[i + 2]])) / 3f;
            if (score >= ArmTriangleWeightThreshold)
            {
                armTriangles.Add(sourceTriangles[i]);
                armTriangles.Add(sourceTriangles[i + 1]);
                armTriangles.Add(sourceTriangles[i + 2]);
            }
        }

        if (armTriangles.Count == 0)
        {
            visualStatus = "nenhum triângulo de braço encontrado no mesh";
            Debug.LogError("OriginalTabzArmsRig: " + visualStatus);
            return;
        }

        armMesh = Instantiate(sourceMesh);
        armMesh.name = "TABZ Original Arms Only";
        armMesh.triangles = armTriangles.ToArray();

        GameObject model = new GameObject("Original TABZ Arms Mesh");
        model.transform.SetParent(rigRoot, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = new Quaternion(0f, 0f, -0.7071063f, 0.70710725f);
        model.transform.localScale = Vector3.one * 0.53732395f;

        SkinnedMeshRenderer renderer = model.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = armMesh;
        renderer.sharedMaterial = skinMaterial;
        renderer.bones = new[]
        {
            rigRoot.Find("Armature/hip"),
            rigRoot.Find("Armature/torso"),
            rigRoot.Find("Armature/torso/Holder/head"),
            rigRoot.Find("Armature/Arm_Left"),
            rigRoot.Find("Armature/Hand_Left"),
            rigRoot.Find("Armature/Arm_Right"),
            rigRoot.Find("Armature/Hand_Right"),
            rigRoot.Find("Armature/Leg_Left_Upper"),
            rigRoot.Find("Armature/Leg_Right_Upper"),
            rigRoot.Find("Armature/Leg_Left_Lower"),
            rigRoot.Find("Armature/Leg_Right_Lower")
        };
        renderer.rootBone = rigRoot.Find("Armature/hip");
        renderer.updateWhenOffscreen = true;
        renderer.localBounds = sourceMesh.bounds;
        visualStatus = "mesh original: " + (armTriangles.Count / 3) +
                       " triângulos de braço / " + (sourceTriangles.Length / 3) + " totais";
    }

    private static float ArmWeight(BoneWeight weight)
    {
        float result = 0f;
        if (weight.boneIndex0 >= 3 && weight.boneIndex0 <= 6) result += weight.weight0;
        if (weight.boneIndex1 >= 3 && weight.boneIndex1 <= 6) result += weight.weight1;
        if (weight.boneIndex2 >= 3 && weight.boneIndex2 <= 6) result += weight.weight2;
        if (weight.boneIndex3 >= 3 && weight.boneIndex3 <= 6) result += weight.weight3;
        return result;
    }

    private void CreateHandMarkers(Transform hand, bool left)
    {
        GameObject fixer = new GameObject("Fixer");
        fixer.transform.SetParent(hand, false);
        fixer.transform.localPosition = Vector3.zero;
        fixer.transform.localRotation = left
            ? new Quaternion(-0.00000038743013f, -0.7071067f, -0.000000059604638f, 0.7071069f)
            : new Quaternion(-0.0000005662441f, -0.7071067f, 0.00000074505806f, 0.7071068f);

        GameObject position = new GameObject("WeaponPosition");
        position.transform.SetParent(hand, false);
        position.transform.localPosition = left
            ? new Vector3(-1.0652418f, 0.009998297f, 0.016676987f)
            : new Vector3(-1.065f, -0.01f, -0.138f);
        position.transform.localRotation = left
            ? new Quaternion(0.7070754f, 0.00000021909364f, 0.00000029057264f, 0.70713824f)
            : new Quaternion(-0.70707595f, 0.0000005112961f, -0.0000006756745f, 0.7071377f);
        position.transform.localScale = left
            ? new Vector3(2.741347f, 2.7431571f, 2.737919f)
            : new Vector3(2.7431612f, 2.7397316f, 2.7379148f);
    }

    private void CreateWeaponHandTags(Transform weaponRoot)
    {
        CreateTag(weaponRoot, "WeaponRightHandTag",
            new Vector3(0f, -0.092f, -0.159f), 0.2936616f);
        CreateTag(weaponRoot, "WeaponLeftHandTag",
            new Vector3(-0.034f, -0.163f, 0.037f), 0.29365999f);
    }

    private static Transform CreateTag(Transform parent, string name, Vector3 position, float scale)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        GameObject tag = new GameObject(name);
        tag.transform.SetParent(parent, false);
        tag.transform.localPosition = position;
        tag.transform.localRotation = Quaternion.identity;
        tag.transform.localScale = Vector3.one * scale;
        return tag.transform;
    }

    private void AttachHandsToWeapon(Rigidbody weaponBody, Transform weaponRoot)
    {
        Transform rightTag = weaponRoot.Find("WeaponRightHandTag");
        Transform leftTag = weaponRoot.Find("WeaponLeftHandTag");
        AlignHandToTag(rightHandBody.transform, rightTag);
        AlignHandToTag(leftHandBody.transform, leftTag);
        rightWeaponJoint = CreateWeaponJoint(rightHandBody, weaponBody);
        leftWeaponJoint = CreateWeaponJoint(leftHandBody, weaponBody);
        weaponBody.constraints = RigidbodyConstraints.None;
    }

    private static void AlignHandToTag(Transform hand, Transform tag)
    {
        Transform position = hand.GetChild(1);
        hand.position = tag.position + (hand.position - position.position);
    }

    private static ConfigurableJoint CreateWeaponJoint(Rigidbody hand, Rigidbody weapon)
    {
        ConfigurableJoint joint = hand.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = weapon;
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        return joint;
    }

    private void Update()
    {
        if (rightHandBody != null && rightWeaponJoint == null && rightIdle != null)
            MoveRig(rightHandBody, rightIdle, 0f, 100f, true);
        if (leftHandBody != null && leftWeaponJoint == null && leftIdle != null)
            MoveRig(leftHandBody, leftIdle, 0f, 100f, true);
    }

    private static void MoveRig(Rigidbody body, Transform target, float force, float torque, bool fixer)
    {
        Vector3 forward = fixer && body.transform.childCount > 0
            ? body.transform.GetChild(0).forward : body.transform.forward;
        float angle = Vector3.Angle(forward, target.forward);
        Vector3 axis = Vector3.Cross(forward, target.forward);
        body.AddTorque(axis * angle * torque * 0.0166f, ForceMode.VelocityChange);
        body.AddForce((target.position - body.position) * force, ForceMode.VelocityChange);
    }

    private static Transform CreateBone(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        return CreateChild(parent, name, position, rotation, Vector3.one);
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 position,
        Quaternion rotation, Vector3 scale)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        child.transform.localRotation = rotation;
        child.transform.localScale = scale;
        return child.transform;
    }

    private static Rigidbody AddBody(Transform target, float mass, float drag, float angularDrag, bool gravity)
    {
        Rigidbody body = target.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.drag = drag;
        body.angularDrag = angularDrag;
        body.useGravity = gravity;
        body.isKinematic = false;
        return body;
    }

    private static void ConfigureCharacterJoint(CharacterJoint joint, Rigidbody connectedBody,
        Vector3 axis, Vector3 swingAxis, Vector3 connectedAnchor,
        float spring, float lowTwist, float highTwist, float swing1, float swing2)
    {
        joint.connectedBody = connectedBody;
        joint.anchor = Vector3.zero;
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = connectedAnchor;
        joint.axis = axis;
        joint.swingAxis = swingAxis;
        joint.twistLimitSpring = new SoftJointLimitSpring { spring = spring, damper = 0f };
        joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
        joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
        joint.swingLimitSpring = new SoftJointLimitSpring { spring = spring, damper = 0f };
        joint.swing1Limit = new SoftJointLimit { limit = swing1 };
        joint.swing2Limit = new SoftJointLimit { limit = swing2 };
        joint.enableProjection = true;
        joint.projectionDistance = 0.1f;
        joint.projectionAngle = 180f;
        joint.enableCollision = false;
        joint.enablePreprocessing = true;
    }

    private static void LockJoint(ConfigurableJoint joint)
    {
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
    }

    public string GetDiagnosticStatus()
    {
        return "Braços: " + (rightHandBody != null && leftHandBody != null ? "2 mãos físicas" : "incompletos") +
               " | Juntas da arma: " + ((rightWeaponJoint != null && leftWeaponJoint != null) ? "2 conectadas" : "não conectadas") +
               " | Visual: " + visualStatus +
               " | ReloadAim: " + (ReloadAim != null ? "OK" : "ausente");
    }

    private void OnDestroy()
    {
        if (rigRoot != null) Destroy(rigRoot.gameObject);
        if (ReloadAim != null) Destroy(ReloadAim.gameObject);
        if (RifleAim != null) Destroy(RifleAim.gameObject);
        if (armMesh != null) Destroy(armMesh);
    }
}
