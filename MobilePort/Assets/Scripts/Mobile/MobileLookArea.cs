using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Transparent UI panel used to drag-look with the right thumb.
/// Configure the panel to occupy the right half of the gameplay screen.
/// </summary>
public class MobileLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public float sensitivity = 0.12f;
    public bool invertY = false;
    private int activePointer = int.MinValue;
    private Vector2 previousPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != int.MinValue) return;
        activePointer = eventData.pointerId;
        previousPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        Vector2 delta = eventData.position - previousPosition;
        previousPosition = eventData.position;
        float y = invertY ? delta.y : -delta.y;
        MobileInputState.LookDelta += new Vector2(delta.x, y) * sensitivity;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointer) activePointer = int.MinValue;
    }
}
