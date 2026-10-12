using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds a small offline combat sandbox at runtime. This is a scaffolding
/// scene to validate touch controls, weapon firing, a hotbar and a killable
/// target before the recovered TABZ prefabs/UI are wired in.
/// It deliberately does not use Photon, Steamworks or FMOD.
/// </summary>
public class OfflinePrototypeBootstrap : MonoBehaviour
{
    public bool launchOnDesktop = true;
    public int startingAmmo = 30;
    public int zombieHealth = 100;

    private OfflinePrototypePlayer player;
    private OfflinePrototypeWeapon weapon;
    private OfflinePrototypeZombie zombie;
    private Text status;
    private Text ammoText;
    private Text inventoryText;
    private Canvas canvas;
    private GameObject inventoryPanel;
    private Text hotbarText;
    private int selectedSlot;

    private void Start()
    {
        if (!launchOnDesktop && !Application.isMobilePlatform) return;
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        EnsureEventSystem();
        BuildWorld();
        BuildHud();
    }

    private void Update()
    {
        if (MobileInputState.InventoryTogglePressed)
        {
            MobileInputState.InventoryTogglePressed = false;
            if (inventoryPanel != null) inventoryPanel.SetActive(!inventoryPanel.activeSelf);
        }
        if (MobileInputState.HotbarSlotPressed >= 0)
        {
            selectedSlot = MobileInputState.HotbarSlotPressed;
            MobileInputState.HotbarSlotPressed = -1;
        }
        if (status != null)
        {
            status.text = zombie == null || zombie.IsDead
                ? "ALVO ELIMINADO — DEV: TOQUE GERAR ZUMBI"
                : "ALVO: ZUMBI | VIDA " + zombie.Health;
        }
        if (ammoText != null && weapon != null)
            ammoText.text = "MUNIÇÃO  " + weapon.Ammo + " / " + weapon.ReserveAmmo;
        if (hotbarText != null)
            hotbarText.text = "HOTBAR  [" + (selectedSlot + 1) + "] RIFLE DE TESTE";
    }

    private void BuildWorld()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Prototype Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(80f, 1f, 80f);
        ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.27f, 0.22f);

        GameObject playerObject = new GameObject("Offline Prototype Player");
        playerObject.transform.position = new Vector3(0f, 1f, -5f);
        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        player = playerObject.AddComponent<OfflinePrototypePlayer>();
        player.controller = controller;

        GameObject cameraObject = new GameObject("Prototype Camera");
        cameraObject.transform.SetParent(playerObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 75f;
        // The recovered AK mesh reaches close to the camera because its prefab
        // bounds extend along local X and the child transform turns X into depth.
        // A shorter near plane prevents the camera from slicing through it.
        camera.nearClipPlane = 0.05f;
        cameraObject.AddComponent<AudioListener>();
        player.viewCamera = camera;

        GameObject lightObject = new GameObject("Prototype Sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        SpawnZombie();
    }

    private void SpawnZombie()
    {
        if (zombie != null) Destroy(zombie.gameObject);
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "Prototype TABZ Zombie (placeholder)";
        enemy.transform.position = new Vector3(0f, 1f, 8f);
        enemy.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
        enemy.GetComponent<Renderer>().material.color = new Color(0.28f, 0.48f, 0.25f);
        zombie = enemy.AddComponent<OfflinePrototypeZombie>();
        zombie.health = zombieHealth;
        zombie.player = player != null ? player.transform : null;
        zombie.onDeath = () => { if (status != null) status.text = "ALVO ELIMINADO — DEV: TOQUE GERAR ZUMBI"; };
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("TABZ Prototype HUD");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        status = MakeText("Target Status", canvas.transform, "ALVO: ZUMBI", 22,
            new Vector2(0.35f, 0.88f), new Vector2(0.65f, 0.98f), TextAnchor.MiddleCenter);
        ammoText = MakeText("Ammo", canvas.transform, "MUNIÇÃO  30 / 90", 22,
            new Vector2(0.78f, 0.84f), new Vector2(0.98f, 0.96f), TextAnchor.MiddleRight);
        hotbarText = MakeText("Hotbar Selection", canvas.transform, "HOTBAR  [1] RIFLE DE TESTE", 18,
            new Vector2(0.33f, 0.02f), new Vector2(0.67f, 0.10f), TextAnchor.MiddleCenter);
        inventoryPanel = BuildInventoryPanel(canvas.transform);
        inventoryPanel.SetActive(false);

        MakeButton("DEV: GERAR ZUMBI", canvas.transform, new Vector2(0.39f, 0.76f),
            new Vector2(0.61f, 0.85f), () => SpawnZombie());

        // Create the shared touch input HUD. It remains usable on desktop when
        // testing is enabled, so no Android build is required to inspect layout.
        gameObject.AddComponent<MobileControlsBootstrap>().createOnDesktopForTesting = launchOnDesktop;
        weapon = player.gameObject.AddComponent<OfflinePrototypeWeapon>();
        weapon.viewCamera = player.viewCamera;
        weapon.ammo = startingAmmo;
        weapon.reserveAmmo = startingAmmo * 3;
        weapon.targetProvider = () => zombie;
        OriginalWeaponVisualBridge originalWeapon = player.gameObject.AddComponent<OriginalWeaponVisualBridge>();
        originalWeapon.viewCamera = player.viewCamera;
        originalWeapon.resourcesPrefabPath = "items/Ak74";
    }

    private GameObject BuildInventoryPanel(Transform parent)
    {
        GameObject panel = new GameObject("Prototype Inventory Panel");
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.28f, 0.18f);
        rect.anchorMax = new Vector2(0.72f, 0.78f);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.04f, 0.94f);
        MakeText("Inventory Title", panel.transform, "INVENTÁRIO (PROTÓTIPO)", 24,
            new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.96f), TextAnchor.MiddleCenter);
        MakeButton("RIFLE DE TESTE — EQUIPADO", panel.transform, new Vector2(0.12f, 0.42f),
            new Vector2(0.88f, 0.70f), () => { selectedSlot = 0; });
        MakeButton("FECHAR INVENTÁRIO", panel.transform, new Vector2(0.25f, 0.10f),
            new Vector2(0.75f, 0.30f), () => panel.SetActive(false));
        return panel;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        GameObject events = new GameObject("Prototype EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<StandaloneInputModule>();
    }

    private Text MakeText(string objectName, Transform parent, string value, int size,
        Vector2 min, Vector2 max, TextAnchor anchor)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size; text.color = Color.white;
        text.alignment = anchor; text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private void MakeButton(string label, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image; button.onClick.AddListener(action);
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
        Text text = labelObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = label; text.fontSize = 16; text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter; text.resizeTextForBestFit = true;
        text.raycastTarget = false;
    }
}