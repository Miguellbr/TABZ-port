using UnityEngine;

/// <summary>Standalone local controller for the first offline prototype.</summary>
public class OfflinePrototypePlayer : MonoBehaviour
{
    public CharacterController controller;
    public Camera viewCamera;
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.55f;
    public float lookSensitivity = 0.12f;
    public float gravity = -20f;
    public float jumpHeight = 1.1f;
    public float desktopLookSensitivity = 2f;

    private float pitch;
    private float verticalVelocity;

    private void Update()
    {
        if (controller == null || viewCamera == null) return;

        Vector2 move = MobileInputState.Move;
        bool sprint = MobileInputState.SprintHeld;
        bool jump = MobileInputState.JumpHeld;
        Vector2 look = MobileInputState.ConsumeLookDelta();

        if (!Application.isMobilePlatform)
        {
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            move = Vector2.ClampMagnitude(move, 1f);
            sprint = Input.GetKey(KeyCode.LeftShift);
            jump |= Input.GetKeyDown(KeyCode.Space);
            look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * desktopLookSensitivity;
            if (Input.GetMouseButtonDown(0)) MobileInputState.FirePressed = true;
            if (Input.GetMouseButton(0)) MobileInputState.FireHeld = true;
            else MobileInputState.FireHeld = false;
            if (Input.GetMouseButtonDown(1)) MobileInputState.AimHeld = true;
            if (Input.GetMouseButtonUp(1)) MobileInputState.AimHeld = false;
            if (Input.GetKeyDown(KeyCode.R)) MobileInputState.ReloadPressed = true;
            if (Input.GetKeyDown(KeyCode.E)) MobileInputState.InteractPressed = true;
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab)) MobileInputState.InventoryTogglePressed = true;
            for (int i = 0; i < 6; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) MobileInputState.HotbarSlotPressed = i;
        }

        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);
        Vector3 planar = (transform.forward * move.y + transform.right * move.x) * speed;
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (jump && controller.isGrounded) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;
        planar.y = verticalVelocity;
        controller.Move(planar * Time.deltaTime);
    }
}
