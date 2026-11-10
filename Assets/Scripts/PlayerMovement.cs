using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owner-driven movement with human pacing: the body eases into a walk or a run,
/// turns toward the step, and can front-flip on a second jump.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    const float Deadzone = 0.14f;
    const float RunStart = 0.58f;
    const float WalkSpeed = 2.15f;
    const float RunSpeed = 6.2f;
    const float GroundAccel = 14f;
    const float GroundDecel = 18f;
    const float AirAccel = 5f;
    const float Gravity = -26f;
    const float FirstJumpHeight = 1.35f;
    const float SecondJumpHeight = 1.55f;

    public float lookSensitivity = 2.2f;
    public Transform cameraPivot;
    public Transform visuals;
    public Camera playerCamera;

    readonly NetworkVariable<float> syncedFlip = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> syncedDash = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> syncedSlide = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> syncedAir = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> syncedBrake = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    CharacterController controller;
    VirtualJoystick joystick;
    JumpButton jumpButton;
    JumpButton slideButton;
    JumpButton dashButton;
    HoldButton guardButton;
    JumpButton turnLeftButton;
    JumpButton turnRightButton;
    GameObject openRoot;
    JumpButton openButton;
    Text openLabel;
    Image slideImage;
    Image dashImage;
    Image turnLeftImage;
    Image turnRightImage;
    Color slideReady = new Color(0.16f, 0.62f, 0.58f, 0.94f);
    Color dashReady = Color.white;
    Color turnReady = Color.white;
    GameObject controlsRoot;
    Transform armL;
    Transform armR;
    Quaternion armLBase = Quaternion.identity;
    Quaternion armRBase = Quaternion.identity;

    Vector3 planarVelocity;
    Vector3 visualsBaseLocal;
    Vector3 spawnPoint;
    PlayerCombat combat;
    float verticalVelocity;
    float cameraYaw;
    float pitch;
    float turnVelocity;
    float jumpLock;
    float coyoteTime;
    float bobTime;
    float lean;
    float flip01;
    float spawnYaw;
    float impactShake;
    int jumpsUsed;
    bool flipping;
    bool slamReady;
    bool cameraDetached;
    bool sliding;
    bool dashing;
    bool airDashUsed;
    bool spinning;
    bool climbing;
    bool landHeavy;
    bool wasRunning;
    bool wasGrounded;
    float slideLeft;
    float previousSlide = -99f;
    float slideCooldownUntil;
    float dashCooldownUntil;
    float previousTurn = -99f;
    float turnCooldownUntil;
    float dashTime;
    float dashRecover;
    float slideRecover;
    float brakeTimer;
    float landTimer;
    float jumpWindup;
    float knockTimer;
    float stunTimer;
    float spinTime;
    float spinStartYaw;
    float spinSide;
    float climbTime;
    float climbCooldown;
    Vector3 slideDir;
    Vector3 dashDir;
    Vector3 spinMove;
    Vector3 climbStart;
    Vector3 climbEnd;
    bool guardBlocked;
    bool emptyDown;
    bool emptyDragged;
    int emptyFinger = -2;
    Vector2 emptyLast;
    public bool EmptyTap { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        combat = GetComponent<PlayerCombat>();
        if (playerCamera != null)
        {
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        if (visuals != null)
        {
            visualsBaseLocal = visuals.localPosition;
            armL = visuals.Find("ArmL");
            armR = visuals.Find("ArmR");
            if (armL != null)
                armLBase = armL.localRotation;
            if (armR != null)
                armRBase = armR.localRotation;
        }

        if (GetComponent<CharacterMotion>() == null)
            gameObject.AddComponent<CharacterMotion>();
    }

    public override void OnNetworkSpawn()
    {
        spawnPoint = transform.position;
        spawnYaw = transform.eulerAngles.y;
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
        if (!IsOwner)
        {
            controller.enabled = false;
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            return;
        }

        foreach (var cam in FindObjectsOfType<Camera>())
        {
            if (playerCamera == null || cam.transform.root != playerCamera.transform.root)
                cam.gameObject.SetActive(false);
        }

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.tag = "MainCamera";
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = true;
        }

        if (cameraPivot != null)
        {
            cameraYaw = transform.eulerAngles.y;
            cameraPivot.SetParent(null, true);
            cameraDetached = true;
        }

        var controls = ScreenControls.Create();
        controlsRoot = controls.Root;
        joystick = controls.Joystick;
        jumpButton = controls.Jump;
        slideButton = controls.Slide;
        dashButton = controls.Dash;
        guardButton = controls.Guard;
        turnLeftButton = controls.TurnLeft;
        turnRightButton = controls.TurnRight;
        openRoot = controls.OpenRoot;
        openButton = controls.Open;
        openLabel = controls.OpenLabel;
        slideImage = controls.SlideImage;
        if (dashButton != null)
        {
            dashImage = dashButton.GetComponent<Image>();
            dashReady = dashImage.color;
        }

        if (turnLeftButton != null)
        {
            turnLeftImage = turnLeftButton.GetComponent<Image>();
            turnReady = turnLeftImage.color;
        }

        if (turnRightButton != null)
            turnRightImage = turnRightButton.GetComponent<Image>();
        if (combat != null)
            combat.BindAttackButton(controls.Attack);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void OnNetworkDespawn()
    {
        DestroyControls();
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnDestroy()
    {
        DestroyControls();
    }

    void DestroyControls()
    {
        if (controlsRoot != null)
        {
            Destroy(controlsRoot);
            controlsRoot = null;
        }

        if (cameraDetached && cameraPivot != null)
        {
            Destroy(cameraPivot.gameObject);
            cameraDetached = false;
        }
    }

    void Update()
    {
        if (IsSpawned && IsOwner)
            TickDoor();

        if (IsSpawned && IsOwner && controller != null && (controller.enabled || climbing))
            MoveOwner();
    }

    void LateUpdate()
    {
        PublishPose();
        if (!IsSpawned || !IsOwner || cameraPivot == null)
            return;

        Look();
        cameraPivot.position = transform.position + Vector3.up * 1.4f;
        cameraPivot.rotation = Quaternion.Euler(pitch, cameraYaw, 0f);
        if (impactShake > 0f)
        {
            impactShake = Mathf.MoveTowards(impactShake, 0f, Time.deltaTime * 1.4f);
            cameraPivot.position += Random.insideUnitSphere * impactShake * 0.22f;
        }
    }

    public void ReturnToSpawn()
    {
        if (controller != null)
            controller.enabled = false;
        transform.position = spawnPoint;
        transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
        cameraYaw = spawnYaw;
        pitch = 12f;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        flipping = false;
        slamReady = false;
        flip01 = 0f;
        jumpsUsed = 0;
        impactShake = 0f;
        sliding = false;
        dashing = false;
        spinning = false;
        airDashUsed = false;
        climbing = false;
        knockTimer = 0f;
        stunTimer = 0f;
        dashRecover = 0f;
        slideRecover = 0f;
        brakeTimer = 0f;
        landTimer = 0f;
        jumpWindup = 0f;
        if (IsOwner && IsSpawned && Mathf.Abs(syncedFlip.Value) > 0.01f)
            syncedFlip.Value = 0f;
        if (controller != null)
            controller.enabled = true;
    }

    public void FaceLookDirection()
    {
        transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
    }

    void TickDoor()
    {
        if (openRoot == null)
            return;

        HouseDoor nearest = null;
        float best = 2.4f;
        for (int i = 0; i < HouseDoor.All.Count; i++)
        {
            var door = HouseDoor.All[i];
            if (door == null || door.Broken)
                continue;
            float distance = Vector3.Distance(transform.position, door.PromptPoint);
            if (distance >= best)
                continue;
            best = distance;
            nearest = door;
        }

        openRoot.SetActive(nearest != null);
        if (nearest == null)
            return;

        if (openLabel != null)
            openLabel.text = nearest.IsOpen ? "Close" : "Open";
        bool pressed = (openButton != null && openButton.Consume()) || Input.GetKeyDown(KeyCode.O);
        if (pressed && combat != null)
            combat.RequestDoor(nearest.Id);
    }

    bool GuardHeld()
    {
        return (guardButton != null && guardButton.Held) || Input.GetKey(KeyCode.G);
    }

    void ReadSpecials(bool grounded, Vector3 wish)
    {
        bool busy = sliding || dashing || spinning;
        if (!busy && ((slideButton != null && slideButton.Pressed) || Input.GetKeyDown(KeyCode.C)))
        {
            if (slideButton != null)
                slideButton.Consume();
            TrySlide(wish);
        }

        if (!busy && ((dashButton != null && dashButton.Pressed) || Input.GetKeyDown(KeyCode.X)))
        {
            if (dashButton != null)
                dashButton.Consume();
            TryDash(grounded);
        }

        if (!busy && ((turnLeftButton != null && turnLeftButton.Pressed) || Input.GetKeyDown(KeyCode.Q)))
        {
            if (turnLeftButton != null)
                turnLeftButton.Consume();
            TrySpin(-1f, grounded);
        }

        if (!busy && ((turnRightButton != null && turnRightButton.Pressed) || Input.GetKeyDown(KeyCode.E)))
        {
            if (turnRightButton != null)
                turnRightButton.Consume();
            TrySpin(1f, grounded);
        }

        if (slideImage != null)
            slideImage.color = Time.time < slideCooldownUntil ? slideReady * 0.45f : slideReady;
        if (dashImage != null)
            dashImage.color = Time.time < dashCooldownUntil ? dashReady * 0.45f : dashReady;
        bool turnsCooling = Time.time < turnCooldownUntil;
        if (turnLeftImage != null)
            turnLeftImage.color = turnsCooling ? turnReady * 0.45f : turnReady;
        if (turnRightImage != null)
            turnRightImage.color = turnsCooling ? turnReady * 0.45f : turnReady;
    }

    void TryDash(bool grounded)
    {
        if (dashing || spinning || sliding || climbing)
            return;
        if (Time.time < dashCooldownUntil)
            return;
        if (!grounded && airDashUsed)
            return;
        if (!grounded)
            airDashUsed = true;
        if (grounded)
            dashCooldownUntil = Time.time + 5f;

        dashDir = (Quaternion.Euler(pitch, cameraYaw, 0f) * Vector3.forward).normalized;
        if (dashDir.sqrMagnitude < 0.001f)
            dashDir = transform.forward;
        dashing = true;
        dashTime = 0f;
        dashRecover = 0f;
        guardBlocked = true;
        if (combat != null)
            combat.SetDefending(false);
    }

    void TrySlide(Vector3 wish)
    {
        if (Time.time < slideCooldownUntil || dashing || spinning || sliding || !IsGrounded())
            return;

        bool second = previousSlide > -1f && Time.time - previousSlide <= 2f;
        previousSlide = Time.time;
        if (second)
        {
            slideCooldownUntil = Time.time + 2f;
            previousSlide = -99f;
        }

        Vector3 dir = wish.sqrMagnitude > 0.04f ? wish : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;
        slideDir = dir.normalized;
        sliding = true;
        slideLeft = 0.32f;
    }

    void TrySpin(float side, bool grounded)
    {
        if (!grounded || spinning || dashing || sliding || climbing)
            return;
        if (Time.time < turnCooldownUntil)
            return;

        bool second = previousTurn > -1f && Time.time - previousTurn <= 2f;
        previousTurn = Time.time;
        if (second)
        {
            turnCooldownUntil = Time.time + 2f;
            previousTurn = -99f;
        }

        spinning = true;
        spinSide = side;
        spinTime = 0f;
        spinStartYaw = transform.eulerAngles.y;
        float yaw = cameraYaw + 60f * side;
        spinMove = (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward).normalized;
    }

    void TryStartClimb()
    {
        if (climbCooldown > 0f || controller.isGrounded || verticalVelocity <= 0.05f)
            return;

        Vector3 chest = transform.position + Vector3.up * (controller.height * 0.55f);
        Vector3 head = transform.position + Vector3.up * (controller.height * 0.95f);
        var hits = Physics.OverlapCapsule(chest, head, 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        ClimbWall wall = null;
        float best = 99f;
        for (int i = 0; i < hits.Length; i++)
        {
            var candidate = hits[i].GetComponent<ClimbWall>();
            if (candidate == null || candidate.House == null || !hits[i].enabled)
                continue;
            Vector3 touch = hits[i].ClosestPoint(head);
            if (!candidate.AtEdge(touch))
                continue;
            if (!candidate.IsOutside(transform.position) || !candidate.IsNearTop(transform.position, controller.height))
                continue;

            float distance = Vector3.Distance(head, touch);
            if (distance >= best)
                continue;
            best = distance;
            wall = candidate;
        }

        if (wall == null)
            return;

        float sole = controller.center.y - controller.height * 0.5f;
        climbing = true;
        climbTime = 0f;
        climbStart = transform.position;
        climbEnd = wall.Landing(transform.position, -sole + controller.skinWidth + 0.04f);
        flipping = false;
        slamReady = false;
        flip01 = 0f;
        verticalVelocity = 0f;
        planarVelocity = Vector3.zero;
        if (controller.enabled)
            controller.enabled = false;
    }

    void AdvanceClimb()
    {
        climbTime += Time.deltaTime;
        float along = Mathf.Clamp01(climbTime / 0.5f);
        float rise = Mathf.Clamp01(along / 0.4f);
        float onto = Mathf.Clamp01((along - 0.4f) / 0.6f);
        var crest = new Vector3(climbStart.x, climbEnd.y, climbStart.z);
        if (controller.enabled)
            controller.enabled = false;
        transform.position = along < 0.4f
            ? Vector3.Lerp(climbStart, crest, rise)
            : Vector3.Lerp(crest, climbEnd, onto);
        if (along < 1f)
            return;

        climbing = false;
        climbCooldown = 0.35f;
        verticalVelocity = 0f;
        planarVelocity = Vector3.zero;
        jumpsUsed = 0;
        if (controller != null)
            controller.enabled = true;
    }

    void MoveOwner()
    {
        bool down = combat != null && combat.IsDown;
        if (down)
        {
            flipping = false;
            slamReady = false;
            climbing = false;
            flip01 = Mathf.MoveTowards(flip01, 0f, Time.deltaTime * 4f);
            if (controller != null && !controller.enabled)
                controller.enabled = true;
        }

        if (knockTimer > 0f)
        {
            knockTimer -= Time.deltaTime;
            planarVelocity = Vector3.zero;
            verticalVelocity = knockTimer > 0.32f ? 6.2f : 0.15f;
            flipping = false;
            if (controller != null && !controller.enabled)
                controller.enabled = true;
            if (controller != null && controller.enabled)
                controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }

        if (stunTimer > 0f)
            stunTimer -= Time.deltaTime;
        if (dashRecover > 0f)
            dashRecover -= Time.deltaTime;
        if (slideRecover > 0f)
            slideRecover -= Time.deltaTime;
        if (brakeTimer > 0f)
            brakeTimer -= Time.deltaTime;
        if (landTimer > 0f)
            landTimer -= Time.deltaTime;

        if (climbCooldown > 0f)
            climbCooldown -= Time.deltaTime;

        if (climbing)
        {
            AdvanceClimb();
            return;
        }

        bool grounded = IsGrounded();
        int jumpState = jumpsUsed;
        bool heavyAir = flipping;
        if (grounded)
        {
            if (!wasGrounded && (heavyAir || jumpState > 0))
            {
                landHeavy = heavyAir || jumpState >= 2;
                landTimer = landHeavy ? 0.2f : 0.16f;
            }

            wasGrounded = true;
            coyoteTime = 0.12f;
            if (!flipping)
                jumpsUsed = 0;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
            if (flipping && flip01 >= 0.98f)
                LandFlip();
        }
        else
        {
            wasGrounded = false;
            coyoteTime -= Time.deltaTime;
        }

        Vector2 stick = down || stunTimer > 0f ? Vector2.zero : ReadStick();
        Vector3 wish = CameraPlanarForward() * stick.y + CameraPlanarRight() * stick.x;
        float magnitude = Mathf.Clamp01(wish.magnitude);
        if (grounded && !dashing)
            airDashUsed = false;
        if (!down)
            ReadSpecials(grounded, wish);
        if (guardBlocked && !GuardHeld())
            guardBlocked = false;
        if (combat != null)
            combat.SetDefending(!down && !dashing && !guardBlocked && GuardHeld());

        bool busy = spinning || dashing || sliding;
        if (!busy && magnitude > Deadzone)
        {
            Vector3 face = wish / magnitude;
            float targetYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            float smooth = magnitude >= RunStart ? 0.08f : 0.16f;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity, smooth);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        float targetSpeed = SpeedFor(magnitude);
        if (combat != null && combat.IsDefending)
            targetSpeed *= 0.8f;
        Vector3 desired = magnitude > Deadzone ? (wish / magnitude) * targetSpeed : Vector3.zero;
        float rate = down ? 28f : (!grounded ? AirAccel : (desired.sqrMagnitude + 0.05f < planarVelocity.sqrMagnitude ? GroundDecel : GroundAccel));
        if (!busy)
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, rate * Time.deltaTime);

        bool running = grounded && !busy && magnitude >= RunStart && planarVelocity.magnitude > 4.2f;
        if (running)
            wasRunning = true;
        else if (wasRunning && grounded && !dashing)
        {
            wasRunning = false;
            brakeTimer = 0.36f;
        }

        bool jumpPressed = Input.GetButtonDown("Jump") || (jumpButton != null && jumpButton.Consume());
        if (jumpPressed && jumpWindup <= 0f && !down && !busy && stunTimer <= 0f && !GameSession.AutoMove)
            TryJump(grounded);
        if (jumpWindup > 0f)
        {
            if (down || dashing)
                jumpWindup = 0f;
            else
            {
                jumpWindup -= Time.deltaTime;
                if (jumpWindup <= 0f)
                {
                    verticalVelocity = JumpVelocity(FirstJumpHeight);
                    jumpsUsed = 1;
                    jumpLock = 0.12f;
                    coyoteTime = 0f;
                }
                else
                    verticalVelocity = -2f;
            }
        }

        if (flipping && !grounded)
        {
            float remaining = Mathf.Max(0.05f, EstimateTimeToGround());
            flip01 = Mathf.Clamp01(flip01 + (1f - flip01) * Time.deltaTime / remaining);
        }
        else if (flipping && grounded)
        {
            flip01 = Mathf.MoveTowards(flip01, 1f, Time.deltaTime / 0.1f);
            if (flip01 >= 0.995f)
                LandFlip();
        }

        if (Mathf.Abs(syncedFlip.Value - flip01) > 0.01f)
            syncedFlip.Value = flip01;

        if (!down && !dashing && !sliding && !spinning)
            TryStartClimb();
        if (climbing)
        {
            AdvanceClimb();
            jumpLock -= Time.deltaTime;
            return;
        }

        if (dashing)
        {
            dashTime += Time.deltaTime;
            float speed = DashSpeed(dashTime);
            Vector3 step = dashDir * speed;
            if (dashTime >= 1f)
            {
                dashing = false;
                dashRecover = 0.18f;
                verticalVelocity = 0f;
                planarVelocity = Vector3.zero;
            }
            else
            {
                planarVelocity = new Vector3(step.x, 0f, step.z);
                verticalVelocity = step.y;
                controller.Move(step * Time.deltaTime);
            }
        }
        else
        {
            if (sliding)
            {
                slideLeft -= Time.deltaTime;
                planarVelocity = slideDir * (4f / 0.32f);
                if (slideLeft <= 0f)
                {
                    sliding = false;
                    slideRecover = 0.2f;
                }
            }
            else if (spinning)
            {
                spinTime += Time.deltaTime;
                float along = Mathf.Clamp01(spinTime / 0.5f);
                transform.rotation = Quaternion.Euler(0f, spinStartYaw - spinSide * 360f * along, 0f);
                planarVelocity = spinMove * (controller.height * 2f / 0.5f);
                if (along >= 1f)
                    spinning = false;
            }

            verticalVelocity += Gravity * Time.deltaTime;
            var motion = planarVelocity;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        jumpLock -= Time.deltaTime;

        if (transform.position.y < -4f)
        {
            controller.enabled = false;
            transform.position = spawnPoint;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            flipping = false;
            slamReady = false;
            flip01 = 0f;
            controller.enabled = true;
        }
    }

    void LandFlip()
    {
        bool slam = slamReady;
        slamReady = false;
        flipping = false;
        flip01 = 0f;
        if (!slam || combat == null)
            return;

        impactShake = 0.32f;
        combat.BreakGround(transform.position);
    }

    void TryJump(bool grounded)
    {
        bool firstJump = grounded || coyoteTime > 0f;
        if (firstJump && jumpsUsed == 0)
        {
            jumpWindup = 0.14f;
            verticalVelocity = -2f;
            return;
        }

        if (jumpsUsed == 1)
        {
            verticalVelocity = JumpVelocity(SecondJumpHeight);
            jumpsUsed = 2;
            jumpLock = 0.12f;
            flipping = true;
            slamReady = true;
            flip01 = 0f;
        }
    }

    bool IsGrounded()
    {
        return controller.isGrounded && jumpLock <= 0f && verticalVelocity <= 0.5f;
    }

    Vector2 ReadStick()
    {
        if (GameSession.AutoMove)
            return Vector2.up;

        Vector2 stick = joystick != null ? joystick.Value : Vector2.zero;
        var keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (keys.sqrMagnitude > 1f)
            keys.Normalize();
        if (keys.sqrMagnitude > 0.01f)
        {
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            keys *= run ? 1f : 0.4f;
        }

        return keys.magnitude > stick.magnitude ? keys : stick;
    }

    static float SpeedFor(float magnitude)
    {
        if (magnitude < Deadzone)
            return 0f;
        if (magnitude < RunStart)
        {
            float walkAmount = Mathf.InverseLerp(Deadzone, RunStart, magnitude);
            return Mathf.Lerp(WalkSpeed * 0.72f, WalkSpeed, walkAmount);
        }

        float runAmount = Mathf.InverseLerp(RunStart, 1f, magnitude);
        return Mathf.Lerp(WalkSpeed, RunSpeed, runAmount);
    }

    static float JumpVelocity(float height)
    {
        return Mathf.Sqrt(height * -2f * Gravity);
    }

    float EstimateTimeToGround()
    {
        float distance = 0.2f;
        var origin = transform.position + Vector3.up * 0.08f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0.05f, hit.distance - 0.08f);

        float gravity = -Gravity;
        if (verticalVelocity > 0f)
        {
            float timeUp = verticalVelocity / gravity;
            float rise = verticalVelocity * timeUp - 0.5f * gravity * timeUp * timeUp;
            return timeUp + Mathf.Sqrt(2f * (distance + rise) / gravity);
        }

        return Mathf.Sqrt(2f * distance / gravity);
    }

    Vector3 CameraPlanarForward()
    {
        return Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
    }

    Vector3 CameraPlanarRight()
    {
        return Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;
    }

    void Look()
    {
        EmptyTap = false;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Input.touchCount > 0)
            LookTouch();
        else
            LookMouse();

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool looking = cursorLocked || Input.GetMouseButton(1);
        if (!looking)
            return;
        if (!cursorLocked && PointerOverUi(Input.mousePosition))
            return;

        cameraYaw += Input.GetAxis("Mouse X") * lookSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -80f, 55f);
    }

    void LookTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && emptyFinger < 0 && !PointerOverUi(touch.position))
            {
                emptyFinger = touch.fingerId;
                emptyDown = true;
                emptyDragged = false;
            }

            if (touch.fingerId != emptyFinger)
                continue;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (touch.deltaPosition.sqrMagnitude > 16f)
                    emptyDragged = true;
                if (emptyDragged)
                    ApplyLookPixels(touch.deltaPosition);
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (emptyDown && !emptyDragged)
                    EmptyTap = true;
                emptyDown = false;
                emptyFinger = -2;
            }
        }
    }

    void LookMouse()
    {
        if (Input.GetMouseButtonDown(0) && !PointerOverUi(Input.mousePosition))
        {
            emptyDown = true;
            emptyDragged = false;
            emptyLast = Input.mousePosition;
        }

        if (!emptyDown || !Input.GetMouseButton(0))
        {
            if (emptyDown && Input.GetMouseButtonUp(0))
            {
                if (!emptyDragged)
                    EmptyTap = true;
                emptyDown = false;
            }

            return;
        }

        Vector2 now = Input.mousePosition;
        Vector2 delta = now - emptyLast;
        if (delta.sqrMagnitude > 16f)
            emptyDragged = true;
        if (emptyDragged)
            ApplyLookPixels(delta);
        emptyLast = now;
    }

    void ApplyLookPixels(Vector2 delta)
    {
        float scale = 0.1f * lookSensitivity;
        cameraYaw += delta.x * scale;
        pitch = Mathf.Clamp(pitch - delta.y * scale, -80f, 55f);
    }

    static readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();

    static bool PointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var data = new PointerEventData(EventSystem.current);
        data.position = screenPosition;
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }

    public float PlanarSpeed => planarVelocity.magnitude;

    public bool LocksActions => stunTimer > 0f || knockTimer > 0f;

    public float DashPose => IsSpawned && IsOwner ? DashLocal() : syncedDash.Value;

    public float SlidePose => IsSpawned && IsOwner ? SlideLocal() : syncedSlide.Value;

    public float AirPose => IsSpawned && IsOwner ? AirLocal() : syncedAir.Value;

    public float BrakePose => IsSpawned && IsOwner ? brakeTimer : syncedBrake.Value;

    public void ApplyReaction(int kind)
    {
        planarVelocity = Vector3.zero;
        jumpWindup = 0f;
        flipping = false;
        slamReady = false;
        if (kind >= 2)
        {
            knockTimer = 0.5f;
            stunTimer = 0f;
            verticalVelocity = 6.2f;
            return;
        }

        stunTimer = 0.2f;
    }

    float DashLocal()
    {
        if (dashing)
            return Mathf.Clamp(dashTime, 0.001f, 1f);
        if (dashRecover > 0f)
            return -dashRecover;
        return 0f;
    }

    float SlideLocal()
    {
        if (sliding)
            return Mathf.Clamp(slideLeft / 0.32f, 0.02f, 1f);
        if (slideRecover > 0f)
            return -slideRecover / 0.2f;
        return 0f;
    }

    float AirLocal()
    {
        if (jumpWindup > 0f)
            return Mathf.Lerp(0.60f, 0.50f, jumpWindup / 0.14f);
        if (landTimer > 0f && landHeavy)
            return Mathf.Lerp(2.5f, 3f, landTimer / 0.2f);
        if (flipping)
            return Mathf.Lerp(1.50f, 2.40f, flip01);
        if (!wasGrounded && jumpsUsed == 1)
            return SingleJumpAir(verticalVelocity);
        if (!wasGrounded && jumpsUsed > 0)
            return 1f;
        if (landTimer > 0f)
            return Mathf.Lerp(0.97f, 0.90f, landTimer / 0.16f);
        return 0f;
    }

    static float SingleJumpAir(float verticalVelocity)
    {
        if (verticalVelocity > 6f)
            return 0.66f;
        if (verticalVelocity > 2f)
            return Mathf.Lerp(0.74f, 0.68f, (verticalVelocity - 2f) / 4f);
        if (verticalVelocity > -2f)
            return 0.78f;
        if (verticalVelocity > -5.5f)
            return Mathf.Lerp(0.88f, 0.80f, (verticalVelocity + 5.5f) / 3.5f);
        return 0.90f;
    }

    void PublishPose()
    {
        if (!IsSpawned || !IsOwner)
            return;

        float dashValue = DashLocal();
        float slideValue = SlideLocal();
        float airValue = AirLocal();
        if (Mathf.Abs(syncedDash.Value - dashValue) > 0.03f)
            syncedDash.Value = dashValue;
        if (Mathf.Abs(syncedSlide.Value - slideValue) > 0.03f)
            syncedSlide.Value = slideValue;
        if (Mathf.Abs(syncedAir.Value - airValue) > 0.03f)
            syncedAir.Value = airValue;
        if (Mathf.Abs(syncedBrake.Value - brakeTimer) > 0.03f)
            syncedBrake.Value = brakeTimer;
    }

    static float DashSpeed(float time)
    {
        if (time < 0.5f)
            return 0.8f;
        if (time < 0.9f)
            return 21.5f;
        return 10f;
    }
}
