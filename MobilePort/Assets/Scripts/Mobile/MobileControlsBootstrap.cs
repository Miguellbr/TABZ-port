using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Creates the landscape touch HUD without requiring recovered editor scenes.
/// Attach to a scene object in the test scene. Gameplay scripts still need
/// explicit mobile branches to consume MobileInputState.
/// </summary>
[DefaultExecutionOrder(32000)]
public class MobileControlsBootstrap : MonoBehaviour
{
    public bool createOnDesktopForTesting = false;
    public float lookSensitivity = 0.12f;

    private void LateUpdate()
    {
        // Clear one-frame inputs only after the original camera consumed look delta.
        MobileInputState.ResetFrame();
    }

    private void Awake()
    {
        if (!Application.isMobilePlatform && !createOnDesktopForTesting) return;
        if (GameObject.Find("TABZ Mobile HUD") != null) return;

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventObject = new GameObject("Mobile EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("TABZ Mobile HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasObject.AddComponent<MobileInventoryBridge>();
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();

        GameObject lookObject = MakeRect("Look Area", root, new Vector2(0.42f, 0.05f),
            new Vector2(1f, 0.95f), new Vector2(0.5f, 0.5f));
        Image lookImage = lookObject.AddComponent<Image>();
        lookImage.color = new Color(1f, 1f, 1f, 0.01f);
        lookObject.AddComponent<MobileLookArea>().sensitivity = lookSensitivity;

        GameObject joystickObject = MakeRect("Move Joystick", root,
            new Vector2(0.04f, 0.08f), new Vector2(0.25f, 0.42f), new Vector2(0.5f, 0.5f));
        Image baseImage = joystickObject.AddComponent<Image>();
        baseImage.color = new Color(0.08f, 0.08f, 0.08f, 0.38f);
        MobileVirtualJoystick joystick = joystickObject.AddComponent<MobileVirtualJoystick>();
        joystick.background = joystickObject.GetComponent<RectTransform>();
        joystick.floatingJoystick = false;
        GameObject handleObject = MakeRect("Joystick Handle", joystickObject.GetComponent<RectTransform>(),
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Vector2(0.5f, 0.5f));
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.9f, 0.9f, 0.9f, 0.65f);
        joystick.handle = handleObject.GetComponent<RectTransform>();

        MakeButton(root, "FIRE", MobileActionButton.ActionType.Fire,
            new Vector2(0.83f, 0.09f), new Vector2(0.96f, 0.31f), new Color(0.75f, 0.12f, 0.1f, 0.68f));
        MakeButton(root, "AIM", MobileActionButton.ActionType.Aim,
            new Vector2(0.70f, 0.10f), new Vector2(0.81f, 0.26f), new Color(0.1f, 0.25f, 0.65f, 0.68f));
        MakeButton(root, "JUMP", MobileActionButton.ActionType.Jump,
            new Vector2(0.31f, 0.09f), new Vector2(0.41f, 0.24f), new Color(0.18f, 0.45f, 0.2f, 0.68f));
        MakeButton(root, "SPRINT", MobileActionButton.ActionType.Sprint,
            new Vector2(0.20f, 0.08f), new Vector2(0.29f, 0.20f), new Color(0.18f, 0.35f, 0.2f, 0.62f));
        MakeButton(root, "RELOAD", MobileActionButton.ActionType.Reload,
            new Vector2(0.70f, 0.29f), new Vector2(0.80f, 0.42f), new Color(0.2f, 0.2f, 0.2f, 0.7f));
        MakeButton(root, "USE", MobileActionButton.ActionType.Interact,
            new Vector2(0.83f, 0.34f), new Vector2(0.94f, 0.47f), new Color(0.2f, 0.2f, 0.2f, 0.7f));

        // The original game has a six-slot hotbar; these buttons emit slot taps
        // that the UIInventory mobile patch will consume.
        string[] slotLabels = { "1", "2", "3", "4", "5", "6" };
        MobileActionButton.ActionType[] slotActions = {
            MobileActionButton.ActionType.Hotbar1, MobileActionButton.ActionType.Hotbar2,
            MobileActionButton.ActionType.Hotbar3, MobileActionButton.ActionType.Hotbar4,
            MobileActionButton.ActionType.Hotbar5, MobileActionButton.ActionType.Hotbar6
        };
        for (int i = 0; i < slotLabels.Length; i++)
        {
            float left = 0.36f + i * 0.052f;
            MakeButton(root, slotLabels[i], slotActions[i],
                new Vector2(left, 0.86f), new Vector2(left + 0.048f, 0.96f),
                new Color(0.08f, 0.08f, 0.08f, 0.62f));
        }
        MakeButton(root, "INV", MobileActionButton.ActionType.Inventory,
            new Vector2(0.03f, 0.84f), new Vector2(0.13f, 0.96f), new Color(0.12f, 0.12f, 0.12f, 0.72f));
    }

    private GameObject MakeRect(string objectName, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = pivot;
        return go;
    }

    private void MakeButton(RectTransform parent, string label,
        MobileActionButton.ActionType action, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = MakeRect(label + " Button", parent, min, max, new Vector2(0.5f, 0.5f));
        Image image = go.AddComponent<Image>();
        image.color = color;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        go.AddComponent<MobileActionButton>().action = action;

        GameObject textObject = MakeRect("Label", go.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.resizeTextForBestFit = true;
        text.raycastTarget = false;
    }
}