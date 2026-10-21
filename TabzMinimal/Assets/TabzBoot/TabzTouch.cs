using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TabzMinimal
{
    /// On-screen touch controls. They do not move anything themselves: they answer the questions the ORIGINAL scripts
    /// ask (Input.GetKey / GetAxis / GetMouseButton), through TabzInput, the shim that the IL patcher put in the DLL.
    [DefaultExecutionOrder(-10000)]
    public sealed class TabzTouch : MonoBehaviour
    {
        public static float UiScale
        {
            get { return Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 160f) : 2f; }
        }

        private const float LookSensitivity = 0.1f;

        private struct Btn
        {
            public string Label;
            public KeyCode Key;
            public int Mouse;
            public bool Toggle;
            public Rect Rect;
        }

        private readonly List<Btn> buttons = new List<Btn>();
        private readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> prevHeld = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> down = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> up = new HashSet<KeyCode>();
        private readonly bool[] mouseHeld = new bool[3];
        private readonly bool[] mousePrev = new bool[3];
        private readonly bool[] mouseDown = new bool[3];
        private readonly bool[] mouseUp = new bool[3];
        private readonly Dictionary<int, int> fingerButton = new Dictionary<int, int>();
        private int moveFinger = -1;
        private int lookFinger = -1;
        private Vector2 moveOrigin;
        private Vector2 moveNow;
        private Vector2 moveVec;
        private Vector2 look;
        private Vector3 pointer;
        private bool runToggled;
        private float logicalW;
        private float logicalH;

        private void Awake()
        {
            Hook();
        }

        // ---------------------------------------------------------------- hooks into the shim
        private void Hook()
        {
            Type t = TabzReflect.FindType("TabzInput");
            if (t == null)
            {
                TabzLog.Add("[TABZ] TabzInput shim NOT FOUND: touch controls will not reach the original scripts");
                return;
            }
            Bind(t, "KeyHeld", "KeyHeldQ");
            Bind(t, "KeyDown", "KeyDownQ");
            Bind(t, "KeyUp", "KeyUpQ");
            Bind(t, "ButtonHeld", "NameQ");
            Bind(t, "ButtonDown", "NameQ");
            Bind(t, "ButtonUp", "NameQ");
            Bind(t, "Axis", "AxisQ");
            Bind(t, "AxisRaw", "AxisQ");
            Bind(t, "MouseHeld", "MouseHeldQ");
            Bind(t, "MouseDown", "MouseDownQ");
            Bind(t, "MouseUp", "MouseUpQ");
            Bind(t, "Pointer", "PointerQ");
            FieldInfo enabled = t.GetField("Enabled");
            if (enabled != null) enabled.SetValue(null, true);
            TabzLog.Add("[TABZ] TabzInput.Enabled = true: the original scripts now read the touch controls");
        }

        private void Bind(Type t, string field, string method)
        {
            FieldInfo f = t.GetField(field);
            if (f == null) { TabzLog.Add("[TABZ] TabzInput has no field " + field); return; }
            try { f.SetValue(null, Delegate.CreateDelegate(f.FieldType, this, method)); }
            catch (Exception e) { TabzLog.Add("[TABZ] could not bind " + field + ": " + e.Message); }
        }

        public bool KeyHeldQ(KeyCode k) { return held.Contains(k); }
        public bool KeyDownQ(KeyCode k) { return down.Contains(k); }
        public bool KeyUpQ(KeyCode k) { return up.Contains(k); }
        public bool NameQ(string name) { return false; }
        public bool MouseHeldQ(int b) { return b >= 0 && b < 3 && mouseHeld[b]; }
        public bool MouseDownQ(int b) { return b >= 0 && b < 3 && mouseDown[b]; }
        public bool MouseUpQ(int b) { return b >= 0 && b < 3 && mouseUp[b]; }

        public Vector3 PointerQ()
        {
            return pointer == Vector3.zero ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f) : pointer;
        }

        public float AxisQ(string name)
        {
            switch (name)
            {
                case "Mouse X": return look.x * LookSensitivity;
                case "Mouse Y": return look.y * LookSensitivity;
                case "Horizontal": return moveVec.x;
                case "Vertical": return moveVec.y;
                default: return 0f;
            }
        }

        // ---------------------------------------------------------------- layout
        private void Add(string label, KeyCode key, int mouse, bool toggle, float x, float y, float w, float h)
        {
            Btn b = new Btn();
            b.Label = label;
            b.Key = key;
            b.Mouse = mouse;
            b.Toggle = toggle;
            b.Rect = new Rect(x, y, w, h);
            buttons.Add(b);
        }

        private void Layout()
        {
            buttons.Clear();
            float W = logicalW;
            float H = logicalH;
            Add("FIRE", KeyCode.None, 0, false, W - 110f, H - 110f, 100f, 100f);
            Add("JUMP", KeyCode.Space, -1, false, W - 110f, H - 190f, 100f, 70f);
            Add("ADS", KeyCode.None, 1, false, W - 215f, H - 110f, 95f, 70f);
            Add("R", KeyCode.R, -1, false, W - 215f, H - 190f, 95f, 70f);
            Add("E", KeyCode.E, -1, false, W - 320f, H - 110f, 95f, 70f);
            Add("INV", KeyCode.I, -1, false, W - 320f, H - 190f, 95f, 70f);
            Add("RUN", KeyCode.LeftShift, -1, true, 10f, H - 330f, 80f, 50f);
            for (int i = 0; i < 6; i++)
                Add((i + 1).ToString(), KeyCode.Alpha1 + i, -1, false, W - 6f * 44f - 4f + i * 44f, 58f, 40f, 34f);
        }

        private int HitButton(float x, float guiY)
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i].Rect.Contains(new Vector2(x, guiY))) return i;
            return -1;
        }

        // ---------------------------------------------------------------- per frame
        private void Update()
        {
            float s = UiScale;
            logicalW = Screen.width / s;
            logicalH = Screen.height / s;
            Layout();

            prevHeld.Clear();
            foreach (KeyCode k in held) prevHeld.Add(k);
            Array.Copy(mouseHeld, mousePrev, 3);
            held.Clear();
            for (int i = 0; i < 3; i++) mouseHeld[i] = false;
            look = Vector2.zero;

            Touch[] touches = Input.touches;
            for (int i = 0; i < touches.Length; i++)
            {
                Touch t = touches[i];
                Vector2 p = new Vector2(t.position.x / s, t.position.y / s);
                float guiY = logicalH - p.y;
                pointer = t.position;

                if (t.phase == TouchPhase.Began)
                {
                    int hit = HitButton(p.x, guiY);
                    if (hit >= 0)
                    {
                        fingerButton[t.fingerId] = hit;
                        if (buttons[hit].Toggle) runToggled = !runToggled;
                    }
                    else if (guiY < 60f)
                    {
                        // debug bar area: ignored by the game controls
                    }
                    else if (p.x < logicalW * 0.42f && guiY > logicalH * 0.35f && moveFinger < 0)
                    {
                        moveFinger = t.fingerId;
                        moveOrigin = p;
                        moveNow = p;
                    }
                    else if (lookFinger < 0)
                    {
                        lookFinger = t.fingerId;
                    }
                }

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    fingerButton.Remove(t.fingerId);
                    if (t.fingerId == moveFinger) { moveFinger = -1; moveVec = Vector2.zero; }
                    if (t.fingerId == lookFinger) lookFinger = -1;
                    continue;
                }

                int bi;
                if (fingerButton.TryGetValue(t.fingerId, out bi))
                {
                    Btn b = buttons[bi];
                    if (!b.Toggle)
                    {
                        if (b.Key != KeyCode.None) held.Add(b.Key);
                        if (b.Mouse >= 0) mouseHeld[b.Mouse] = true;
                    }
                }
                else if (t.fingerId == moveFinger)
                {
                    moveNow = p;
                    Vector2 d = (moveNow - moveOrigin) / 60f;
                    moveVec = d.sqrMagnitude > 1f ? d.normalized : d;
                }
                else if (t.fingerId == lookFinger)
                {
                    look += t.deltaPosition / s;
                }
            }

            if (moveVec.y > 0.35f) held.Add(KeyCode.W);
            if (moveVec.y < -0.35f) held.Add(KeyCode.S);
            if (moveVec.x > 0.35f) held.Add(KeyCode.D);
            if (moveVec.x < -0.35f) held.Add(KeyCode.A);
            if (runToggled) held.Add(KeyCode.LeftShift);

            foreach (KeyCode k in held) if (!prevHeld.Contains(k)) down.Add(k);
            foreach (KeyCode k in prevHeld) if (!held.Contains(k)) up.Add(k);
            for (int i = 0; i < 3; i++)
            {
                if (mouseHeld[i] && !mousePrev[i]) mouseDown[i] = true;
                if (!mouseHeld[i] && mousePrev[i]) mouseUp[i] = true;
            }
        }

        public void EndFrame()
        {
            down.Clear();
            up.Clear();
            for (int i = 0; i < 3; i++) { mouseDown[i] = false; mouseUp[i] = false; }
        }

        private void OnGUI()
        {
            float s = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            for (int i = 0; i < buttons.Count; i++)
            {
                Btn b = buttons[i];
                bool on = (b.Key != KeyCode.None && held.Contains(b.Key)) || (b.Mouse >= 0 && mouseHeld[b.Mouse]) || (b.Toggle && runToggled);
                GUI.color = on ? new Color(1f, 0.8f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
                GUI.Box(b.Rect, b.Label);
            }
            if (moveFinger >= 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.Box(new Rect(moveOrigin.x - 40f, logicalH - moveOrigin.y - 40f, 80f, 80f), string.Empty);
                GUI.color = new Color(1f, 0.8f, 0.2f, 0.7f);
                GUI.Box(new Rect(moveNow.x - 18f, logicalH - moveNow.y - 18f, 36f, 36f), string.Empty);
            }
            GUI.color = Color.white;
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class TabzTouchClear : MonoBehaviour
    {
        public TabzTouch Target;

        private void LateUpdate()
        {
            if (Target != null) Target.EndFrame();
        }
    }
}
