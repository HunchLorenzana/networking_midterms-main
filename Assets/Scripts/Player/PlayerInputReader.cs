using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    private enum AimPhase
    {
        Idle,
        Pending,
        Aiming
    }

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private float aimHeight = 1f;
    [SerializeField] private float quickClickSeconds = 0.15f;

    private readonly AimPhase[] phases = new AimPhase[PlayerWeapon.SLOT_COUNT];
    private readonly WeaponFireMode[] fireModes = new WeaponFireMode[PlayerWeapon.SLOT_COUNT];
    private readonly float[] pressTimes = new float[PlayerWeapon.SLOT_COUNT];
    private readonly bool[] ignoreUntilRelease = new bool[PlayerWeapon.SLOT_COUNT];

    private Camera aimCamera;
    private bool isBlocked;
    private bool isActive;

    public event Action<int> AimStarted;
    public event Action<int> ShotReleased;

    private bool IsLocked => isBlocked || !MatchManager.Instance.CanPlayersAct;

    public bool IsAiming => isActive && !IsLocked;

    // Screen-relative: "up" always moves up the screen, even when the camera is turned for the far side of the map
    public Vector2 MoveInput => IsLocked ? Vector2.zero : ToCameraSpace(moveAction.action.ReadValue<Vector2>());

    public Vector3 AimPoint
    {
        get
        {
            Vector3 fallback = transform.position + transform.forward;

            if (Mouse.current == null) return fallback;

            Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimHeight, 0f));

            return aimPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : fallback;
        }
    }

    public Vector3 AimDirection
    {
        get
        {
            Vector3 direction = AimPoint - transform.position;

            direction.y = 0f;

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        }
    }

    void Update()
    {
        if (!isActive) return;

        for (int slot = 0; slot < PlayerWeapon.SLOT_COUNT; slot++)
        {
            if (IsLocked)
            {
                BlockSlot(slot);
            }
            else
            {
                CancelOnOtherButton(slot);
            }
        }

        if (IsLocked) return;

        for (int slot = 0; slot < PlayerWeapon.SLOT_COUNT; slot++)
        {
            TickSlot(slot);
        }
    }

    void OnDisable()
    {
        Deactivate();
    }

    private Vector2 ToCameraSpace(Vector2 input)
    {
        if (aimCamera == null) return input;

        Vector3 world = Quaternion.Euler(0f, aimCamera.transform.eulerAngles.y, 0f) * new Vector3(input.x, 0f, input.y);

        return new Vector2(world.x, world.z);
    }

    public void Activate(Camera camera)
    {
        aimCamera = camera;
        isActive = true;
        moveAction.action.Enable();
        attackAction.action.Enable();
    }

    public void Deactivate()
    {
        if (!isActive) return;

        isActive = false;
        moveAction.action.Disable();
        attackAction.action.Disable();
        Array.Clear(phases, 0, phases.Length);
        Array.Clear(ignoreUntilRelease, 0, ignoreUntilRelease.Length);
    }

    public void SetBlocked(bool value)
    {
        isBlocked = value;
    }

    public void SetFireMode(int slot, WeaponFireMode mode)
    {
        fireModes[slot] = mode;
        phases[slot] = AimPhase.Idle;
    }

    public bool IsFireHeld(int slot)
    {
        return CanReadSlot(slot) && fireModes[slot] == WeaponFireMode.HoldToFire && IsButtonHeld(slot);
    }

    public bool IsAimingSlot(int slot)
    {
        if (!CanReadSlot(slot)) return false;

        return fireModes[slot] == WeaponFireMode.HoldToFire ? IsButtonHeld(slot) : phases[slot] == AimPhase.Aiming;
    }

    public bool IsAbilityPressed(int slot)
    {
        if (IsLocked || Keyboard.current == null) return false;

        return slot == 0 ? Keyboard.current.qKey.wasPressedThisFrame : Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool CanReadSlot(int slot)
    {
        return isActive && !IsLocked && !ignoreUntilRelease[slot];
    }

    private bool IsButtonHeld(int slot)
    {
        return slot == 0 ? attackAction.action.IsPressed() : Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    private bool IsButtonPressed(int slot)
    {
        return slot == 0 ? attackAction.action.WasPressedThisFrame() : Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    private void BlockSlot(int slot)
    {
        phases[slot] = AimPhase.Idle;

        if (IsButtonHeld(slot))
        {
            ignoreUntilRelease[slot] = true;
        }
    }

    private void CancelOnOtherButton(int slot)
    {
        int other = 1 - slot;

        if (fireModes[slot] != WeaponFireMode.AimAndRelease || phases[slot] == AimPhase.Idle || !IsButtonPressed(other)) return;

        phases[slot] = AimPhase.Idle;
        ignoreUntilRelease[slot] = IsButtonHeld(slot);
        ignoreUntilRelease[other] = true;
    }

    private void TickSlot(int slot)
    {
        if (ignoreUntilRelease[slot])
        {
            if (!IsButtonHeld(slot))
            {
                ignoreUntilRelease[slot] = false;
            }

            return;
        }

        if (fireModes[slot] == WeaponFireMode.HoldToFire) return;

        switch (phases[slot])
        {
            case AimPhase.Idle:
                if (!IsButtonPressed(slot)) return;

                phases[slot] = AimPhase.Pending;
                pressTimes[slot] = Time.time;

                break;
            case AimPhase.Pending:
                if (!IsButtonHeld(slot))
                {
                    phases[slot] = AimPhase.Idle;
                    ShotReleased?.Invoke(slot);
                }
                else if (Time.time - pressTimes[slot] >= quickClickSeconds)
                {
                    phases[slot] = AimPhase.Aiming;
                    AimStarted?.Invoke(slot);
                }

                break;
            case AimPhase.Aiming:
                if (IsButtonHeld(slot)) return;

                phases[slot] = AimPhase.Idle;
                ShotReleased?.Invoke(slot);

                break;
        }
    }
}
