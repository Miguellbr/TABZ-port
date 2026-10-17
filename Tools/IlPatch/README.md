# TabzIlPatch

Redirects every call to `UnityEngine.Input.*` inside the original TABZ v5.1 assemblies
to `TabzInput.*` (from `TabzInputShim.dll`), directly in the IL with Mono.Cecil.

No decompiling and no recompiling of game code, so decompiler bugs cannot be introduced.

## What the workflow produces

Workflow: `.github/workflows/il-patch-input.yml` (runs on every push to this branch, or manually).

Artifact `tabz-il-patched-input`:

- `Assembly-CSharp.dll` (patched)
- `Assembly-CSharp-firstpass.dll` (patched, only written if it has Input calls)
- `TabzInputShim.dll` (compiled against the original `UnityEngine.dll` / `mscorlib.dll`)
- `report-*.txt` (how many calls were redirected, per method and per type, plus any
  `Input` calls that have no shim method yet)

## How the game uses it

`TabzInput.Enabled = false` (default): all calls go to the real `UnityEngine.Input`.

`TabzInput.Enabled = true`: all calls are answered by delegates that the Android project sets,
so no desktop input leaks through. Example wiring (to live in the Unity project, not here):

```csharp
TabzInput.Enabled = true;
TabzInput.KeyHeld = k =>
    k == KeyCode.W ? MobileInputState.Move.y >  0.3f :
    k == KeyCode.S ? MobileInputState.Move.y < -0.3f :
    k == KeyCode.D ? MobileInputState.Move.x >  0.3f :
    k == KeyCode.A ? MobileInputState.Move.x < -0.3f :
    (k == KeyCode.LeftShift || k == KeyCode.RightShift) ? MobileInputState.SprintHeld :
    k == KeyCode.Space ? MobileInputState.JumpHeld : false;
TabzInput.KeyDown = k =>
    k == KeyCode.R ? MobileInputState.ReloadPressed :
    k == KeyCode.E ? MobileInputState.InteractPressed :
    (k == KeyCode.I || k == KeyCode.Tab) ? MobileInputState.InventoryTogglePressed : false;
TabzInput.Axis = n =>
    n == "Mouse X" ? MobileInputState.LookDelta.x :
    n == "Mouse Y" ? MobileInputState.LookDelta.y : 0f;
TabzInput.MouseHeld = b => b == 0 && MobileInputState.FireHeld;
TabzInput.MouseDown = b => b == 0 && MobileInputState.FirePressed;
```

The key mapping above follows `MobilePort/ANDROID-INPUT-MAP.md`. Check the redirect report to see
which keys and axes the original code really reads before trusting it.

## Known limits

- Only calls are redirected. Fields, properties and `Input.touches` / `GetTouch` are not covered
  unless a matching public static method exists in `TabzInput.cs`.
- The shim is a separate DLL; it has to be imported next to the original DLLs in the Unity project.
  It has not been imported or run in Unity yet.
- The mobile hooks (the example above) are not part of this change.
