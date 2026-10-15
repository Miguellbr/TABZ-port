using UnityEngine;

/// <summary>
/// Compatibility layer for the original TABZ scripts. Desktop keyboard/mouse
/// input remains intact; Android touch controls are translated into the exact
/// KeyCode and mouse-axis queries used by the original player systems.
/// </summary>
public static class TABZInput
{
    public static bool GetKey(KeyCode key)
    {
        bool touch = false;
        switch (key)
        {
            case KeyCode.W: touch = MobileInputState.Move.y > 0.15f; break;
            case KeyCode.S: touch = MobileInputState.Move.y < -0.15f; break;
            // Original TABZ maps A to Right() and D to Left().
            case KeyCode.A: touch = MobileInputState.Move.x < -0.15f; break;
            case KeyCode.D: touch = MobileInputState.Move.x > 0.15f; break;
            case KeyCode.LeftShift:
            case KeyCode.RightShift: touch = MobileInputState.SprintHeld; break;
            case KeyCode.Space: touch = MobileInputState.JumpHeld; break;
            case KeyCode.Mouse0: touch = MobileInputState.FireHeld; break;
            case KeyCode.Mouse1: touch = MobileInputState.AimHeld; break;
            case KeyCode.LeftControl:
            case KeyCode.RightControl: touch = MobileInputState.CrouchHeld; break;
        }
        return Input.GetKey(key) || touch;
    }

    public static bool GetKeyDown(KeyCode key)
    {
        bool touch = false;
        switch (key)
        {
            case KeyCode.Space: touch = MobileInputState.JumpPressed; break;
            case KeyCode.Mouse0: touch = MobileInputState.FirePressed; break;
            case KeyCode.R: touch = MobileInputState.ReloadPressed; break;
            case KeyCode.E: touch = MobileInputState.InteractPressed; break;
            case KeyCode.LeftControl:
            case KeyCode.RightControl: touch = MobileInputState.CrouchPressed; break;
            case KeyCode.Z: touch = MobileInputState.PronePressed; break;
            case KeyCode.G: touch = MobileInputState.SpecialPressed; break;
        }
        return Input.GetKeyDown(key) || touch;
    }

    public static Vector2 GetLookDelta()
    {
        Vector2 touch = MobileInputState.ConsumeLookDelta();
        return new Vector2(Input.GetAxis("Mouse X") + touch.x,
                           Input.GetAxis("Mouse Y") + touch.y);
    }
}
