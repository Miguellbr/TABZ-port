using UnityEngine;

/// <summary>
/// Shared touch input state for the TABZ Android reconstruction.
/// The original TABZ scripts must explicitly consume these states in their
/// mobile branches; this class does not override Unity's legacy Input API.
/// </summary>
public static class MobileInputState
{
    public static Vector2 Move;
    public static Vector2 LookDelta;

    public static bool FireHeld, FirePressed, AimHeld, JumpHeld;
    public static bool ReloadPressed, InteractPressed, SprintHeld, CrouchHeld;
    public static bool InventoryTogglePressed;
    // -1 means no slot was tapped this frame; 0..5 map to the original 6-slot hotbar.
    public static int HotbarSlotPressed = -1;

    public static void ResetFrame()
    {
        LookDelta = Vector2.zero;
        FirePressed = false;
        ReloadPressed = false;
        InteractPressed = false;
        InventoryTogglePressed = false;
        HotbarSlotPressed = -1;
    }

    public static Vector2 ConsumeLookDelta()
    {
        Vector2 delta = LookDelta;
        LookDelta = Vector2.zero;
        return delta;
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
        if (buttonName == "Fire1" || buttonName == "Fire") return FirePressed;
        if (buttonName == "Reload") return ReloadPressed;
        if (buttonName == "Interact") return InteractPressed;
        if (buttonName == "Inventory") return InventoryTogglePressed;
        return false;
    }
}