using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reusable touch button. Set action to Fire, Aim, Jump, Sprint, Crouch,
/// Reload, or Interact. Hold actions remain active while pressed.
/// </summary>
public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum ActionType { Fire, Aim, Jump, Sprint, Crouch, Reload, Interact }
    public ActionType action;

    public void OnPointerDown(PointerEventData eventData) { SetState(true); }
    public void OnPointerUp(PointerEventData eventData) { SetState(false); }

    private void SetState(bool pressed)
    {
        switch (action)
        {
            case ActionType.Fire: MobileInputState.FireHeld = pressed; break;
            case ActionType.Aim: MobileInputState.AimHeld = pressed; break;
            case ActionType.Jump: MobileInputState.JumpHeld = pressed; break;
            case ActionType.Sprint: MobileInputState.SprintHeld = pressed; break;
            case ActionType.Crouch: MobileInputState.CrouchHeld = pressed; break;
            case ActionType.Reload: if (pressed) MobileInputState.ReloadPressed = true; break;
            case ActionType.Interact: if (pressed) MobileInputState.InteractPressed = true; break;
        }
    }

    private void OnDisable() { SetState(false); }
}
