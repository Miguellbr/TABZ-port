using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Floating or fixed on-screen movement joystick.
/// Place on a UI object with a background Image and assign the handle RectTransform.
/// </summary>
public class MobileVirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;
    [Range(0.2f, 1f)] public float handleRange = 0.7f;
    public bool floatingJoystick = true;

    private Vector2 origin;
    private Canvas canvas;
    private int activePointer = int.MinValue;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (background == null) background = transform as RectTransform;
        if (background != null) origin = background.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != int.MinValue) return;
        activePointer = eventData.pointerId;

        if (floatingJoystick && background != null && canvas != null)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background.parent as RectTransform, eventData.position,
                eventData.pressEventCamera, out local))
                background.anchoredPosition = local;
        }
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        if (background == null || handle == null) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out local)) return;

        float radius = Mathf.Min(background.rect.width, background.rect.height) * 0.5f;
        if (radius <= 0f) return;
        Vector2 value = Vector2.ClampMagnitude(local / radius, 1f);
        handle.anchoredPosition = value * radius * handleRange;
        MobileInputState.Move = value;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        Release();
    }

    private void Release()
    {
        activePointer = int.MinValue;
        MobileInputState.Move = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (floatingJoystick && background != null) background.anchoredPosition = origin;
    }

    private void OnDisable() { Release(); }
}