using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reusable touch button. Hold actions remain active while pressed; one-shot
/// actions are latched until the end-of-frame reset.
/// </summary>
public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum ActionType
    {
        Fire, Aim, Jump, Sprint, Crouch, Prone, Special, Reload, Interact, Inventory,
        Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6
    }

    public ActionType action;
    private int activePointer = int.MinValue;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != int.MinValue) return;
        activePointer = eventData.pointerId;
        SetState(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        activePointer = int.MinValue;
        SetState(false);
    }

    private void SetState(bool pressed)
    {
        switch (action)
        {
            case ActionType.Fire:
                MobileInputState.FireHeld = pressed;
                if (pressed) MobileInputState.FirePressed = true;
                break;
            case ActionType.Aim: MobileInputState.AimHeld = pressed; break;
            case ActionType.Jump:
                MobileInputState.JumpHeld = pressed;
                if (pressed) MobileInputState.JumpPressed = true;
                break;
            case ActionType.Sprint: MobileInputState.SprintHeld = pressed; break;
            case ActionType.Crouch:
                MobileInputState.CrouchHeld = pressed;
                if (pressed) MobileInputState.CrouchPressed = true;
                break;
            case ActionType.Prone: if (pressed) MobileInputState.PronePressed = true; break;
            case ActionType.Special: if (pressed) MobileInputState.SpecialPressed = true; break;
            case ActionType.Reload: if (pressed) MobileInputState.ReloadPressed = true; break;
            case ActionType.Interact: if (pressed) MobileInputState.InteractPressed = true; break;
            case ActionType.Inventory: if (pressed) MobileInputState.InventoryTogglePressed = true; break;
            case ActionType.Hotbar1: if (pressed) MobileInputState.HotbarSlotPressed = 0; break;
            case ActionType.Hotbar2: if (pressed) MobileInputState.HotbarSlotPressed = 1; break;
            case ActionType.Hotbar3: if (pressed) MobileInputState.HotbarSlotPressed = 2; break;
            case ActionType.Hotbar4: if (pressed) MobileInputState.HotbarSlotPressed = 3; break;
            case ActionType.Hotbar5: if (pressed) MobileInputState.HotbarSlotPressed = 4; break;
            case ActionType.Hotbar6: if (pressed) MobileInputState.HotbarSlotPressed = 5; break;
        }
    }

    private void OnDisable() { CancelPointer(); }
    private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) CancelPointer(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelPointer(); }

    private void CancelPointer()
    {
        if (activePointer == int.MinValue) return;
        activePointer = int.MinValue;
        SetState(false);
    }
}