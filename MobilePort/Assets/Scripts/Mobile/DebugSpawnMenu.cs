using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Development-only runtime spawn menu for the TABZ mobile reconstruction.
/// Add this component to a scene object in test builds and assign recovered
/// enemy/player/item prefabs in the Inspector. It intentionally does not guess
/// prefab references: the original serialized prefabs still need to be recovered.
/// Compatible with Unity 5.x UI APIs.
/// </summary>
public class DebugSpawnMenu : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        public string label;
        public GameObject prefab;
    }

    public List<SpawnEntry> spawnEntries = new List<SpawnEntry>();
    public KeyCode desktopToggleKey = KeyCode.F1;
    public bool startOpen = false;
    public float spawnDistance = 4f;
    public float spawnSpread = 1.5f;
    public int maxTrackedObjects = 100;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private Canvas canvas;
    private GameObject panel;
    private Text statusText;
    private bool isOpen;

    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        enabled = false;
        return;
#endif
        isOpen = startOpen;
        BuildUI();
        SetOpen(isOpen);
    }

    private void Update()
    {
        if (Input.GetKeyDown(desktopToggleKey))
            SetOpen(!isOpen);
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("TABZ Debug Spawn Menu");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventObject = new GameObject("TABZ Debug EventSystem");
            eventObject.AddComponent<EventSystem>();
            eventObject.AddComponent<StandaloneInputModule>();
        }

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        GameObject toggle = MakeButton(root, "DEV", new Vector2(0.88f, 0.88f),
            new Vector2(0.98f, 0.98f), new Color(0.15f, 0.15f, 0.15f, 0.9f));
        toggle.GetComponent<Button>().onClick.AddListener(delegate { SetOpen(!isOpen); });

        panel = new GameObject("Spawn Menu Panel");
        panel.transform.SetParent(root, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.58f, 0.08f);
        panelRect.anchorMax = new Vector2(0.97f, 0.86f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.06f, 0.94f);

        Text title = MakeText(panelRect, "TABZ TEST MENU", new Vector2(0.04f, 0.91f),
            new Vector2(0.96f, 0.99f), 24);
        title.alignment = TextAnchor.MiddleCenter;

        statusText = MakeText(panelRect, "Ready", new Vector2(0.04f, 0.01f),
            new Vector2(0.96f, 0.08f), 14);
        statusText.alignment = TextAnchor.MiddleLeft;

        MakeButton(panelRect, "REMOVE SPAWNED", new Vector2(0.04f, 0.82f),
            new Vector2(0.48f, 0.90f), new Color(0.5f, 0.12f, 0.1f, 0.95f))
            .GetComponent<Button>().onClick.AddListener(RemoveSpawnedObjects);
        MakeButton(panelRect, "SPAWN ALL", new Vector2(0.52f, 0.82f),
            new Vector2(0.96f, 0.90f), new Color(0.15f, 0.32f, 0.18f, 0.95f))
            .GetComponent<Button>().onClick.AddListener(SpawnAllAssigned);

        int count = Mathf.Max(1, spawnEntries.Count);
        for (int i = 0; i < spawnEntries.Count; i++)
        {
            SpawnEntry entry = spawnEntries[i];
            float top = 0.79f - (i * 0.085f);
            float bottom = top - 0.075f;
            string label = entry == null || String.IsNullOrEmpty(entry.label)
                ? "Spawn " + (i + 1) : entry.label;
            bool available = entry != null && entry.prefab != null;
            if (!available) label += " (prefab missing)";

            GameObject button = MakeButton(panelRect, label,
                new Vector2(0.04f, bottom), new Vector2(0.96f, top),
                available ? new Color(0.12f, 0.22f, 0.30f, 0.95f)
                          : new Color(0.22f, 0.22f, 0.22f, 0.8f));
            button.GetComponent<Button>().interactable = available;
            if (available)
            {
                SpawnEntry captured = entry;
                button.GetComponent<Button>().onClick.AddListener(delegate { Spawn(captured); });
            }
        }
    }

    private GameObject MakeButton(RectTransform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject(label + " Button");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = color;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        button.colors = colors;

        Text text = MakeText(rect, label, Vector2.zero, Vector2.one, 16);
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        return go;
    }

    private Text MakeText(RectTransform parent, string value,
        Vector2 anchorMin, Vector2 anchorMax, int size)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void Spawn(SpawnEntry entry)
    {
        if (entry == null || entry.prefab == null) return;
        if (spawnedObjects.Count >= maxTrackedObjects)
        {
            SetStatus("Spawn limit reached. Remove spawned objects first.");
            return;
        }

        Transform origin = Camera.main != null ? Camera.main.transform : transform;
        Vector3 position = origin.position + origin.forward * spawnDistance;
        position += origin.right * UnityEngine.Random.Range(-spawnSpread, spawnSpread);
        position.y = Mathf.Max(position.y, origin.position.y - 1f);
        GameObject instance = Instantiate(entry.prefab, position, Quaternion.LookRotation(
            new Vector3(origin.forward.x, 0f, origin.forward.z)));
        spawnedObjects.Add(instance);
        SetStatus("Spawned " + entry.label + " (" + spawnedObjects.Count + " tracked)");
    }

    private void SpawnAllAssigned()
    {
        for (int i = 0; i < spawnEntries.Count; i++)
            if (spawnEntries[i] != null && spawnEntries[i].prefab != null)
                Spawn(spawnEntries[i]);
    }

    private void RemoveSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        spawnedObjects.Clear();
        SetStatus("Removed test-spawned objects.");
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (panel != null) panel.SetActive(open);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
