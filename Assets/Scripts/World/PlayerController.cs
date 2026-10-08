using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CharacterController motor plus first/third-person camera.
/// Main Camera should be a scene root (not a child of Player).
/// Third person is Zelda-style: mouse always orbits (locked cursor),
/// WASD walks relative to the camera, body turns only when moving.
/// Toggle with V.
/// </summary>
public class PlayerController : MonoBehaviour
{
    private enum ViewMode
    {
        FirstPerson,
        ThirdPerson,
    }

    [SerializeField] private Transform lookCamera;
    [SerializeField] private ViewMode viewMode = ViewMode.ThirdPerson;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float yawSensitivity = 1.8f;
    [SerializeField] private float pitchSensitivity = 0.55f;
    [SerializeField] private float pitchMin = -89.5f;
    [SerializeField] private float pitchMax = 89.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float eyeHeight = 1.75f;
    [SerializeField] private float capsuleHeight = 1.7f;
    [SerializeField] private float capsuleRadius = 0.35f;
    [SerializeField] private float stepOffset = 0.1f;
    [SerializeField] private float slopeLimit = 40f;
    [SerializeField] private float thirdPersonDistance = 3.5f;
    [SerializeField] private float thirdPersonMinDistance = 1.2f;
    [SerializeField] private float thirdPersonMaxDistance = 8f;
    [SerializeField] private float thirdPersonLookHeight = 1.4f;
    [SerializeField] private float thirdPersonCollisionRadius = 0.18f;
    [SerializeField] private float thirdPersonFollowSmooth = 0.08f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float runSpeedMultiplier = 1.75f;
    [SerializeField] private float jumpSpeed = 7f;
    [SerializeField] private float interactDistance = 1f;
    [SerializeField] private float interactRadius = 0.4f;

    private readonly Collider[] interactHits = new Collider[32];
    private CharacterController controller;
    private Animator animator;
    private SkinnedMeshRenderer[] bodyRenderers;
    private float yaw;
    private float pitch;
    private float verticalVelocity = -2f;
    private float ignoreLookUntil;
    private Vector3 cameraFollowVelocity;
    private Vector3 cameraFocusVelocity;
    private Vector3 smoothedCameraPosition;
    private Vector3 smoothedFocus;
    private bool cameraSmoothingReady;

    private bool IsFirstPerson => viewMode == ViewMode.FirstPerson;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        if (lookCamera == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }

            lookCamera = cam != null ? cam.transform : transform;
        }

        bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        ApplyCapsule();
        yaw = transform.eulerAngles.y;
        ApplyViewMode(lockCursor: true);
    }

    private void TryInteract()
    {
        var origin = transform.position + Vector3.up * (IsFirstPerson ? eyeHeight : thirdPersonLookHeight);
        var lookDir = InteractLookDirection();
        var reach = origin + lookDir * interactDistance;
        var count = Physics.OverlapCapsuleNonAlloc(
            origin,
            reach,
            interactRadius,
            interactHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        HingedProp best = null;
        var bestDist = float.PositiveInfinity;
        for (var i = 0; i < count; i++)
        {
            var col = interactHits[i];
            if (col == null || col.transform.IsChildOf(transform))
            {
                continue;
            }

            var hinged = col.GetComponentInParent<HingedProp>();
            if (hinged == null)
            {
                continue;
            }

            var point = col.ClosestPoint(origin);
            var dist = (point - origin).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hinged;
            }
        }

        if (best != null)
        {
            best.Toggle();
        }
    }

    private Vector3 InteractLookDirection()
    {
        if (IsFirstPerson && lookCamera != null)
        {
            return lookCamera.forward;
        }

        var facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (facing.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return facing.normalized;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            SetCursorLocked(true);
            ignoreLookUntil = Time.unscaledTime + 0.15f;
        }
    }

    private void ApplyCapsule()
    {
        if (controller == null)
        {
            return;
        }

        controller.height = capsuleHeight;
        controller.radius = capsuleRadius;
        controller.slopeLimit = slopeLimit;
        controller.stepOffset = Mathf.Min(stepOffset, capsuleRadius);
        controller.skinWidth = Mathf.Max(0.01f, capsuleRadius * 0.1f);
        controller.minMoveDistance = 0f;
        controller.enableOverlapRecovery = true;
        controller.center = new Vector3(0f, capsuleHeight * 0.5f, 0f);
    }

    private void ApplyViewMode(bool lockCursor)
    {
        SetCursorLocked(lockCursor);
        ignoreLookUntil = Time.unscaledTime + 0.15f;
        SetBodyVisible(!IsFirstPerson);
        cameraSmoothingReady = false;
        PlaceCamera();
    }

    private void SetBodyVisible(bool visible)
    {
        if (bodyRenderers == null)
        {
            return;
        }

        for (var i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
            {
                bodyRenderers[i].enabled = visible;
            }
        }
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDestroy()
    {
        SetCursorLocked(false);
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null)
        {
            return;
        }

        if (keyboard.vKey.wasPressedThisFrame)
        {
            viewMode = IsFirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
            ApplyViewMode(lockCursor: true);
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLocked(true);
            ignoreLookUntil = Time.unscaledTime + 0.15f;
        }

        var looking = Cursor.lockState == CursorLockMode.Locked;

        if (looking && Time.unscaledTime >= ignoreLookUntil)
        {
            var look = mouse.delta.ReadValue();
            yaw += look.x * yawSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * pitchSensitivity, pitchMin, pitchMax);
        }

        if (!IsFirstPerson)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                thirdPersonDistance = Mathf.Clamp(
                    thirdPersonDistance - scroll * 0.01f,
                    thirdPersonMinDistance,
                    thirdPersonMaxDistance);
            }
        }

        var input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 planarForward;
        Vector3 planarRight;
        if (IsFirstPerson)
        {
            planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }
        else
        {
            var orbit = Quaternion.Euler(0f, yaw, 0f);
            planarForward = orbit * Vector3.forward;
            planarRight = orbit * Vector3.right;
        }

        if (planarForward.sqrMagnitude > 0.0001f) planarForward.Normalize();
        if (planarRight.sqrMagnitude > 0.0001f) planarRight.Normalize();

        var running = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        var animSpeed = 0f;
        if (input.sqrMagnitude > 0.01f)
        {
            animSpeed = running ? 1f : 0.5f;
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);
        }

        var motion = (planarForward * input.y + planarRight * input.x)
            * moveSpeed * (running && input.sqrMagnitude > 0.01f ? runSpeedMultiplier : 1f);
        if (IsFirstPerson)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else if (motion.sqrMagnitude > 0.0001f)
        {
            var facing = Quaternion.LookRotation(planarForward * input.y + planarRight * input.x, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = jumpSpeed;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool("Grounded", controller.isGrounded);
        }
    }

    private void LateUpdate()
    {
        PlaceCamera();
    }

    private void PlaceCamera()
    {
        if (lookCamera == null || lookCamera == transform)
        {
            return;
        }

        if (IsFirstPerson)
        {
            lookCamera.position = transform.position + Vector3.up * eyeHeight;
            lookCamera.rotation = Quaternion.Euler(pitch, yaw, 0f);
            return;
        }

        var focus = transform.position + Vector3.up * thirdPersonLookHeight;
        var orbit = Quaternion.Euler(pitch, yaw, 0f);
        var desired = focus + orbit * (Vector3.back * thirdPersonDistance);
        var toCamera = desired - focus;
        var distance = toCamera.magnitude;
        if (distance > 0.001f)
        {
            var direction = toCamera / distance;
            var skip = capsuleRadius + thirdPersonCollisionRadius + 0.05f;
            var remaining = distance - skip;
            if (remaining > 0.001f
                && Physics.SphereCast(
                    focus + direction * skip,
                    thirdPersonCollisionRadius,
                    direction,
                    out var hit,
                    remaining,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                desired = hit.point + hit.normal * thirdPersonCollisionRadius;
            }
        }

        if (!cameraSmoothingReady)
        {
            smoothedFocus = focus;
            smoothedCameraPosition = desired;
            cameraFollowVelocity = Vector3.zero;
            cameraFocusVelocity = Vector3.zero;
            cameraSmoothingReady = true;
        }
        else
        {
            smoothedFocus = Vector3.SmoothDamp(
                smoothedFocus,
                focus,
                ref cameraFocusVelocity,
                thirdPersonFollowSmooth);
            smoothedCameraPosition = Vector3.SmoothDamp(
                smoothedCameraPosition,
                desired,
                ref cameraFollowVelocity,
                thirdPersonFollowSmooth);
        }

        lookCamera.position = smoothedCameraPosition;
        lookCamera.LookAt(smoothedFocus, Vector3.up);
    }
}
