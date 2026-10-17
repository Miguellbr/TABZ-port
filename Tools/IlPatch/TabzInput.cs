using UnityEngine;

// Drop-in replacement for the parts of UnityEngine.Input that the original TABZ v5.1
// assemblies use. Compiled against the ORIGINAL UnityEngine.dll / mscorlib.dll so that the
// signatures match exactly what the original IL calls.
//
// The IL patcher (TabzIlPatch) redirects every call to UnityEngine.Input.<Method> to
// TabzInput.<Method> with the same signature.
//
// Behaviour:
//   Enabled == false -> every call is forwarded to the real UnityEngine.Input (desktop behaviour).
//   Enabled == true  -> every call is answered by the hooks below (touch controls).
//                       A missing hook answers false / 0, so no desktop input leaks through.

public delegate bool TabzKeyQuery(KeyCode key);
public delegate bool TabzNameQuery(string name);
public delegate float TabzAxisQuery(string name);
public delegate bool TabzMouseButtonQuery(int button);
public delegate Vector3 TabzPointerQuery();

public static class TabzInput
{
    public static bool Enabled;

    public static TabzKeyQuery KeyHeld;
    public static TabzKeyQuery KeyDown;
    public static TabzKeyQuery KeyUp;

    public static TabzNameQuery ButtonHeld;
    public static TabzNameQuery ButtonDown;
    public static TabzNameQuery ButtonUp;

    public static TabzAxisQuery Axis;
    public static TabzAxisQuery AxisRaw;

    public static TabzMouseButtonQuery MouseHeld;
    public static TabzMouseButtonQuery MouseDown;
    public static TabzMouseButtonQuery MouseUp;

    public static TabzPointerQuery Pointer;

    // ---- keys -------------------------------------------------------------
    public static bool GetKey(KeyCode key)
    {
        if (Enabled) return KeyHeld != null && KeyHeld(key);
        return Input.GetKey(key);
    }

    public static bool GetKeyDown(KeyCode key)
    {
        if (Enabled) return KeyDown != null && KeyDown(key);
        return Input.GetKeyDown(key);
    }

    public static bool GetKeyUp(KeyCode key)
    {
        if (Enabled) return KeyUp != null && KeyUp(key);
        return Input.GetKeyUp(key);
    }

    public static bool GetKey(string name)
    {
        if (Enabled) return ButtonHeld != null && ButtonHeld(name);
        return Input.GetKey(name);
    }

    public static bool GetKeyDown(string name)
    {
        if (Enabled) return ButtonDown != null && ButtonDown(name);
        return Input.GetKeyDown(name);
    }

    public static bool GetKeyUp(string name)
    {
        if (Enabled) return ButtonUp != null && ButtonUp(name);
        return Input.GetKeyUp(name);
    }

    // ---- named buttons (Fire1, Jump, ...) ----------------------------------
    public static bool GetButton(string name)
    {
        if (Enabled) return ButtonHeld != null && ButtonHeld(name);
        return Input.GetButton(name);
    }

    public static bool GetButtonDown(string name)
    {
        if (Enabled) return ButtonDown != null && ButtonDown(name);
        return Input.GetButtonDown(name);
    }

    public static bool GetButtonUp(string name)
    {
        if (Enabled) return ButtonUp != null && ButtonUp(name);
        return Input.GetButtonUp(name);
    }

    // ---- axes (Horizontal, Vertical, Mouse X, Mouse Y, Mouse ScrollWheel) --
    public static float GetAxis(string name)
    {
        if (Enabled) return Axis != null ? Axis(name) : 0f;
        return Input.GetAxis(name);
    }

    public static float GetAxisRaw(string name)
    {
        if (Enabled)
        {
            if (AxisRaw != null) return AxisRaw(name);
            return Axis != null ? Axis(name) : 0f;
        }
        return Input.GetAxisRaw(name);
    }

    // ---- mouse -------------------------------------------------------------
    public static bool GetMouseButton(int button)
    {
        if (Enabled) return MouseHeld != null && MouseHeld(button);
        return Input.GetMouseButton(button);
    }

    public static bool GetMouseButtonDown(int button)
    {
        if (Enabled) return MouseDown != null && MouseDown(button);
        return Input.GetMouseButtonDown(button);
    }

    public static bool GetMouseButtonUp(int button)
    {
        if (Enabled) return MouseUp != null && MouseUp(button);
        return Input.GetMouseButtonUp(button);
    }

    // Property getter compiles to get_mousePosition, same name as UnityEngine.Input.
    public static Vector3 mousePosition
    {
        get
        {
            if (Enabled && Pointer != null) return Pointer();
            return Input.mousePosition;
        }
    }
}
