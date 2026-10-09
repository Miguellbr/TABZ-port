using UnityEngine;

/// <summary>
/// Touch input state for the TABZ Android reconstruction.
/// Compatible with older Unity C# profiles (including Unity 5.x).
/// </summary>
public static class MobileInputState
{
    public static Vector2 Move;
    public static Vector2 LookDelta;
    public static bool FireHeld, AimHeld, JumpHeld, ReloadPressed, InteractPressed, SprintHeld, CrouchHeld;

    public static void ResetFrame()
    {
        LookDelta = Vector2.zero;
        ReloadPressed = false;
        InteractPressed = false;
    }

    public static float GetAxis(string axisName)
    {
        if (axisName == "Horizontal") return Move.x;
        if (axisName == "Vertical") return Move.y;
        if (axisName == "Mouse X") return LookDelta.x;
        if (axisName == "Mouse Y") return LookDelta.y;
        return 0f;
    }

    public static bool GetButton(string buttonName)
    {
        if (buttonName == "Fire1" || buttonName == "Fire") return FireHeld;
        if (buttonName == "Fire2" || buttonName == "Aim") return AimHeld;
        if (buttonName == "Jump") return JumpHeld;
        if (buttonName == "Sprint") return SprintHeld;
        if (buttonName == "Crouch") return CrouchHeld;
        return false;
    }

    public static bool GetButtonDown(string buttonName)
    {
        if (buttonName == "Reload") return ReloadPressed;
        if (buttonName == "Interact") return InteractPressed;
        return false;
    }
}
