using UnityEngine;

/// <summary>
/// Per-frame touch state shared by the Android HUD and the original TABZ input adapter.
/// One-shot values are cleared at the end of the frame by MobileControlsBootstrap.
/// </summary>
public static class MobileInputState
{
    public static Vector2 Move;
    public static Vector2 LookDelta;

    public static bool FireHeld, FirePressed, AimHeld;
    public static bool JumpHeld, JumpPressed;
    public static bool ReloadPressed, InteractPressed, SprintHeld;
    public static bool CrouchHeld, CrouchPressed;
    public static bool InventoryTogglePressed;
    public static int HotbarSlotPressed = -1;

    public static Vector2 ConsumeLookDelta()
    {
        Vector2 delta = LookDelta;
        LookDelta = Vector2.zero;
        return delta;
    }

    public static void ResetFrame()
    {
        LookDelta = Vector2.zero;
        FirePressed = false;
        JumpPressed = false;
        ReloadPressed = false;
        InteractPressed = false;
        CrouchPressed = false;
        InventoryTogglePressed = false;
        HotbarSlotPressed = -1;
    }
}
