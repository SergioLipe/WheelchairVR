using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Electric wheelchair movement controller - VR Version (Meta Quest 3)
/// Simulates realistic electric wheelchair joystick control.
/// Optimized for performance and Meta Store VRC compliance.
/// </summary>
public class MovementVR : MonoBehaviour
{
    [Header("=== VR Input Actions ===")]
    [Tooltip("Left controller thumbstick - acts as the wheelchair joystick")]
    public InputActionReference joystickAction;

    [Header("=== Hand Joystick (opcional) ===")]
    [Tooltip("Joystick virtual por hand tracking. Quando ativo, sobrepõe-se ao thumbstick.")]
    public HandVirtualJoystick handJoystick;

    [Tooltip("Button to toggle between Slow/Normal Mode (e.g. Left X button)")]
    public InputActionReference toggleSpeedAction;

    [Tooltip("Button to change Steering Type (e.g. Right A button)")]
    public InputActionReference switchSteeringAction;

    [Tooltip("Emergency Brake (e.g. Left Trigger or Grip)")]
    public InputActionReference brakeAction;

    [Header("=== Haptic Feedback ===")]
    [Tooltip("Haptic action for the left controller (binding: <XRController>{LeftHand}/haptic)")]
    public InputActionReference leftHapticAction;

    [Tooltip("Haptic action for the right controller (binding: <XRController>{RightHand}/haptic)")]
    public InputActionReference rightHapticAction;

    [Tooltip("Skip haptics on a side when its controller is resting")]
    public InputModeSwitcher inputModeSwitcher;

    [Range(0f, 1f)] public float collisionHapticIntensity = 0.6f;
    [Range(0f, 1f)] public float slideHapticIntensity = 0.25f;
    [Range(0f, 1f)] public float brakeHapticIntensity = 0.3f;

    [Header("=== Speed Settings ===")]
    [Tooltip("Maximum speed in normal mode (km/h)")]
    public float maxSpeedNormal = 8f;

    [Tooltip("Maximum speed in slow/interior mode (km/h)")]
    public float maxSpeedSlow = 3f;

    [Tooltip("Reverse speed (km/h)")]
    public float reverseSpeed = 2f;

    [Header("=== Joystick Feel ===")]
    [Range(0.05f, 0.3f)] public float joystickDeadzone = 0.12f;
    [Range(1f, 3f)] public float joystickCurve = 1.8f;
    [Range(1f, 10f)] public float joystickSmoothing = 4f;

    [Header("=== Acceleration Settings ===")]
    public float accelerationTime = 2.5f;
    public float brakingTime = 1.5f;
    public float emergencyBrakeTime = 0.4f;

    [Header("=== Rotation Settings ===")]
    public float rotationSpeed = 90f;
    public bool rotationInPlace = false;

    [Header("=== Level Start Settings ===")]
    [Tooltip("The speed mode this specific level should start with.")]
    public SpeedMode startingSpeedMode = SpeedMode.Slow;

    [Tooltip("The steering mode this specific level should start with.")]
    public WheelController.SteeringType startingSteeringMode = WheelController.SteeringType.FrontSteering;

    [Header("=== Driving Modes ===")]
    public SpeedMode currentMode = SpeedMode.Normal;

    [Header("=== Effect Sounds ===")]
    public AudioSource effectsAudio;
    public AudioClip modeChangeSound;
    public AudioClip steeringChangeSound;
    public AudioClip hardCollisionSound;
    public AudioClip slideStartSound;
    public float minCollisionSpeed = 0.8f;

    [Header("=== Sound Cooldowns ===")]
    public float collisionSoundCooldown = 0.5f;
    public float slideSoundCooldown = 0.8f;

    [Header("=== Physics and Limits ===")]
    public float maxSlope = 10f;
    public float gravity = -9.81f;

    [Header("=== VRC: Pause Behavior (Meta Store) ===")]
    [Tooltip("Pause game when user removes the headset (focus lost)")]
    public bool pauseOnFocusLost = true;

    [Tooltip("Stop the wheelchair immediately when focus is lost")]
    public bool stopOnFocusLost = true;

    [Header("=== Current State (Debug) ===")]
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float targetSpeed = 0f;
    [SerializeField] private bool emergencyBrake = false;
    [SerializeField] private float rotationEfficiency = 100f;
    [SerializeField] private Vector2 rawJoystickInput = Vector2.zero;
    [SerializeField] private Vector2 processedJoystickInput = Vector2.zero;

    // Internal Components
    private CharacterController controller;
    private Vector3 movementVelocity;
    private WheelController wheelController;
    private CollisionSystemVR collisionSystem;

    // [OPT] Cache de InputActions (evita lookup via .action todos os frames)
    private InputAction joystickActionCached;
    private InputAction toggleSpeedActionCached;
    private InputAction switchSteeringActionCached;
    private InputAction brakeActionCached;

    // [OPT] Cache de transform
    private Transform myTransform;

    // [OPT] Cache de rumble devices
    private XRControllerWithRumble leftRumbleDevice;
    private XRControllerWithRumble rightRumbleDevice;
    private float lastRumbleDeviceCheck = 0f;
    private const float RUMBLE_DEVICE_RECHECK_INTERVAL = 2f;

    // [OPT] Cache da steering type
    private WheelController.SteeringType currentSteeringTypeCached;
    private WheelController.SteeringType steeringTypeForSoundCache;

    // Smoothed input
    private float smoothedVerticalInput = 0f;
    private float smoothedHorizontalInput = 0f;
    private float tryingToTurnTime = 0f;

    // Realistic Physics State
    private float currentAccelerationVelocity = 0f;
    private bool brakeLockEngaged = true;
    private float previousSpeed = 0f;
    private SpeedMode modeBeforeBrake = SpeedMode.Slow;

    [HideInInspector] public bool playerIsAccelerating = false;
    [HideInInspector] public bool inputLocked = false;

    // Sound cache
    private bool slidingCache = false;
    private float lastCollisionSoundTime = 0f;
    private float lastSlideSoundTime = 0f;

    // Haptic state
    private bool wasColliding = false;

    // [VRC] Focus state
    private bool wasFocusedBefore = true;

    public enum SpeedMode { Slow, Normal, Off }

    // ===== INPUT SYSTEM ENABLE/DISABLE =====

    private void OnEnable()
    {
        EnableAction(joystickAction);
        EnableAction(toggleSpeedAction);
        EnableAction(switchSteeringAction);
        EnableAction(brakeAction);
        EnableAction(leftHapticAction);
        EnableAction(rightHapticAction);
    }

    private void OnDisable()
    {
        DisableAction(joystickAction);
        DisableAction(toggleSpeedAction);
        DisableAction(switchSteeringAction);
        DisableAction(brakeAction);
        DisableAction(leftHapticAction);
        DisableAction(rightHapticAction);
    }

    private void EnableAction(InputActionReference actionRef)
    {
        if (actionRef != null && actionRef.action != null)
            actionRef.action.Enable();
    }

    private void DisableAction(InputActionReference actionRef)
    {
        if (actionRef != null && actionRef.action != null)
            actionRef.action.Disable();
    }

    // ===== INITIALIZATION =====

    void Awake()
    {
        myTransform = transform;

        // [OPT] Cache de InputActions
        if (joystickAction != null) joystickActionCached = joystickAction.action;
        if (toggleSpeedAction != null) toggleSpeedActionCached = toggleSpeedAction.action;
        if (switchSteeringAction != null) switchSteeringActionCached = switchSteeringAction.action;
        if (brakeAction != null) brakeActionCached = brakeAction.action;
    }

    void Start()
    {
        SetupCharacterController();
        SetupComponents();
        ConvertSpeeds();
        InitializeCache();
        InitializeLevelSettings();
        PreloadSounds();
        CacheRumbleDevices();
    }

    private void PreloadSounds()
    {
        if (effectsAudio == null) return;
        effectsAudio.spatialBlend = 0f;
        if (modeChangeSound != null) modeChangeSound.LoadAudioData();
        if (steeringChangeSound != null) steeringChangeSound.LoadAudioData();
        if (hardCollisionSound != null) hardCollisionSound.LoadAudioData();
        if (slideStartSound != null) slideStartSound.LoadAudioData();
    }

    private void InitializeLevelSettings()
    {
        currentMode = startingSpeedMode;
        if (wheelController != null)
        {
            WheelController.SteeringType chosenSteering = SteeringPreference.HasUserChosen
                ? SteeringPreference.CurrentSteering
                : startingSteeringMode;

            wheelController.SetSteeringType(chosenSteering);
            Debug.Log($"[MovementVR] Steering set to: {chosenSteering}");
        }
    }

    private void SetupCharacterController()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
        controller.minMoveDistance = 0.0f;
        controller.stepOffset = 0.08f;
    }

    private void SetupComponents()
    {
        wheelController = GetComponent<WheelController>();
        collisionSystem = GetComponent<CollisionSystemVR>();
        if (collisionSystem == null)
        {
            collisionSystem = gameObject.AddComponent<CollisionSystemVR>();
        }
        collisionSystem.Initialize(controller, myTransform);
    }

    private void ConvertSpeeds()
    {
        maxSpeedNormal = maxSpeedNormal / 3.6f;
        maxSpeedSlow = maxSpeedSlow / 3.6f;
        reverseSpeed = reverseSpeed / 3.6f;
    }

    private void InitializeCache()
    {
        if (wheelController != null)
        {
            currentSteeringTypeCached = wheelController.GetSteeringType();
            steeringTypeForSoundCache = currentSteeringTypeCached;
        }
    }

    private void CacheRumbleDevices()
    {
        leftRumbleDevice = FindRumbleDevice(leftHapticAction);
        rightRumbleDevice = FindRumbleDevice(rightHapticAction);
        lastRumbleDeviceCheck = Time.time;
    }

    private XRControllerWithRumble FindRumbleDevice(InputActionReference hapticRef)
    {
        if (hapticRef == null || hapticRef.action == null) return null;
        foreach (var control in hapticRef.action.controls)
        {
            if (control.device is XRControllerWithRumble rumbleDevice)
                return rumbleDevice;
        }
        return null;
    }

    // ===== MAIN UPDATE LOOP =====

    void Update()
    {
        if (inputLocked) return;

        // [OPT] Refresh dos rumble devices a cada 2s (caso o controller se desligue/ligue)
        if (Time.time - lastRumbleDeviceCheck > RUMBLE_DEVICE_RECHECK_INTERVAL)
        {
            if (leftRumbleDevice == null) leftRumbleDevice = FindRumbleDevice(leftHapticAction);
            if (rightRumbleDevice == null) rightRumbleDevice = FindRumbleDevice(rightHapticAction);
            lastRumbleDeviceCheck = Time.time;
        }

        UpdateSteeringState();
        collisionSystem.Update();
        ProcessSoundEffects();
        ProcessHapticFeedback();
        UpdateTimers();

        ManageModes();

        if (currentMode != SpeedMode.Off)
        {
            ProcessJoystickInput();
        }
        else
        {
            EmergencyStop();
        }

        ApplyRealisticMovement();
        ApplyGravity();
    }

    private void UpdateSteeringState()
    {
        if (wheelController != null)
        {
            currentSteeringTypeCached = wheelController.GetSteeringType();
        }
    }

    // ===== JOYSTICK INPUT PROCESSING =====

    void ProcessJoystickInput()
    {
        Vector2 rawInput = Vector2.zero;

        // Prioridade: hand tracking se estiver a agarrar; senão, comando
        if (handJoystick != null && handJoystick.IsActive)
        {
            rawInput = handJoystick.Output;
        }
        else if (joystickActionCached != null)
        {
            rawInput = joystickActionCached.ReadValue<Vector2>();
        }
        rawJoystickInput = rawInput;

        // [OPT] sqrMagnitude evita sqrt
        float sqrMag = rawInput.sqrMagnitude;
        float sqrDeadzone = joystickDeadzone * joystickDeadzone;

        if (sqrMag < sqrDeadzone)
        {
            rawInput = Vector2.zero;
        }
        else
        {
            float magnitude = Mathf.Sqrt(sqrMag);
            float remapped = (magnitude - joystickDeadzone) / (1f - joystickDeadzone);
            rawInput = (rawInput / magnitude) * remapped;
        }

        // Apply response curve
        float curvedMagnitude = Mathf.Pow(rawInput.magnitude, joystickCurve);
        Vector2 curvedInput = rawInput.normalized * curvedMagnitude;

        // [OPT] cache deltaTime
        float dt = Time.deltaTime;
        float smoothLerp = joystickSmoothing * dt;

        smoothedVerticalInput = Mathf.Lerp(smoothedVerticalInput, curvedInput.y, smoothLerp);
        smoothedHorizontalInput = Mathf.Lerp(smoothedHorizontalInput, curvedInput.x, smoothLerp);

        processedJoystickInput = new Vector2(smoothedHorizontalInput, smoothedVerticalInput);
        playerIsAccelerating = (Mathf.Abs(smoothedVerticalInput) > 0.05f);

        float maxSpeed = currentMode == SpeedMode.Slow ? maxSpeedSlow : maxSpeedNormal;

        // DIFFERENTIAL STEERING PHYSICS: Turning aggressively reduces max forward speed naturally
        float turnPenalty = 1f - (Mathf.Abs(smoothedHorizontalInput) * 0.4f);
        maxSpeed *= turnPenalty;

        float verticalForCollision = smoothedVerticalInput;

        ApplyCollisionBlocking(ref verticalForCollision, ref maxSpeed);
        ApplyAccelerationDeceleration(maxSpeed);
        ProcessRotation(smoothedHorizontalInput);
    }

    // ===== MODE MANAGEMENT =====

    void ManageModes()
    {
        if (toggleSpeedActionCached != null && toggleSpeedActionCached.WasPressedThisFrame())
        {
            currentMode = (currentMode == SpeedMode.Slow) ? SpeedMode.Normal : SpeedMode.Slow;
            PlaySound(modeChangeSound);
            SendHapticPulse(true, 0.15f, 0.08f);
        }

        if (switchSteeringActionCached != null && switchSteeringActionCached.WasPressedThisFrame())
        {
            PlaySound(steeringChangeSound);
            SendHapticPulse(false, 0.15f, 0.08f);
        }

        bool brakeIsHeld = false;
        if (brakeActionCached != null)
        {
            brakeIsHeld = brakeActionCached.IsPressed();
        }

        if (brakeIsHeld && currentMode != SpeedMode.Off)
        {
            modeBeforeBrake = currentMode;
            currentMode = SpeedMode.Off;
            emergencyBrake = true;
            SendHapticPulse(true, brakeHapticIntensity, 0.15f);
            SendHapticPulse(false, brakeHapticIntensity, 0.15f);
        }
        else if (!brakeIsHeld && emergencyBrake)
        {
            currentMode = modeBeforeBrake;
            emergencyBrake = false;
        }
    }

    // ===== COLLISION & PHYSICS =====

    private void ApplyCollisionBlocking(ref float verticalInput, ref float maxSpeed)
    {
        if (collisionSystem.IsFrontBlocked && smoothedVerticalInput > 0)
        {
            smoothedVerticalInput = 0;
            targetSpeed = 0;

            if (verticalInput > 0.5f)
            {
                currentSpeed = Mathf.Max(currentSpeed - 0.5f * Time.deltaTime, -0.05f);
            }
        }
        else if (collisionSystem.IsBackBlocked && smoothedVerticalInput < 0)
        {
            smoothedVerticalInput = 0;
            targetSpeed = 0;
        }
        else
        {
            if (smoothedVerticalInput < 0)
            {
                maxSpeed = reverseSpeed;
            }
            targetSpeed = smoothedVerticalInput * maxSpeed;
        }
    }

    private void ApplyAccelerationDeceleration(float maxSpeed)
    {
        bool isMovingForward = targetSpeed > 0;
        bool isMovingBackward = targetSpeed < 0;
        bool blockedForward = collisionSystem.IsFrontBlocked && isMovingForward;
        bool blockedBackward = collisionSystem.IsBackBlocked && isMovingBackward;
        bool blockedInTargetDirection = blockedForward || blockedBackward;
        bool accelerating = Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed);

        if (!blockedInTargetDirection && accelerating)
        {
            if (Mathf.Abs(targetSpeed) > 0.1f)
            {
                brakeLockEngaged = false;
            }
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref currentAccelerationVelocity, accelerationTime * 0.5f);
        }
        else
        {
            if (collisionSystem.IsFrontBlocked && currentSpeed > 0)
                currentSpeed = 0;
            else if (collisionSystem.IsBackBlocked && currentSpeed < 0)
                currentSpeed = 0;
            else if (collisionSystem.IsInCollision)
                currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref currentAccelerationVelocity, brakingTime * 0.3f);
            else
                currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref currentAccelerationVelocity, brakingTime);

            if (Mathf.Abs(targetSpeed) < 0.05f && Mathf.Abs(currentSpeed) < 0.15f && !brakeLockEngaged && Mathf.Abs(previousSpeed) > 0.2f)
            {
                currentSpeed = 0f;
                targetSpeed = 0f;
                currentAccelerationVelocity = 0f;
                brakeLockEngaged = true;

                SendHapticPulse(true, 0.4f, 0.05f);
                SendHapticPulse(false, 0.4f, 0.05f);
            }
            else if (Mathf.Abs(targetSpeed) < 0.05f && Mathf.Abs(currentSpeed) < 0.05f && !brakeLockEngaged)
            {
                currentSpeed = 0f;
                targetSpeed = 0f;
                currentAccelerationVelocity = 0f;
                brakeLockEngaged = true;
            }
        }

        previousSpeed = currentSpeed;
    }

    // ===== ROTATION =====

    void ProcessRotation(float horizontalInput)
    {
        float rotationMultiplier = 1f;
        bool isRearSteering = false;
        rotationEfficiency = 100f;

        if (wheelController != null)
        {
            isRearSteering = currentSteeringTypeCached == WheelController.SteeringType.RearSteering;
            if (isRearSteering)
            {
                rotationMultiplier = 2.5f;
            }
        }

        bool isStationary = Mathf.Abs(currentSpeed) < 0.1f;

        if (isRearSteering)
        {
            ProcessRearRotation(isStationary, horizontalInput, ref rotationMultiplier);
        }
        else
        {
            ProcessFrontRotation(isStationary, ref rotationMultiplier);
        }

        float rotation = horizontalInput * rotationSpeed * rotationMultiplier * Time.deltaTime;
        myTransform.Rotate(0, rotation, 0);
    }

    private void ProcessRearRotation(bool isStationary, float horizontalInput, ref float multiplier)
    {
        if (isStationary)
        {
            rotationEfficiency = 0f;
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                tryingToTurnTime = 1f;
            }
            multiplier = 0f;
        }
        else
        {
            float normalizedSpeed = Mathf.Abs(currentSpeed) / maxSpeedNormal;
            float baseEfficiency = Mathf.Lerp(0.2f, 1f, normalizedSpeed);
            multiplier *= baseEfficiency;

            if (currentSpeed < 0)
            {
                multiplier *= -0.8f;
                rotationEfficiency = baseEfficiency * 80f;
            }
            else
            {
                rotationEfficiency = baseEfficiency * 100f;
            }
        }
    }

    private void ProcessFrontRotation(bool isStationary, ref float multiplier)
    {
        if (isStationary && !rotationInPlace)
        {
            rotationEfficiency = 0f;
            multiplier = 0f;
        }
        else if (isStationary && rotationInPlace)
        {
            multiplier *= 1.5f;
            rotationEfficiency = 100f;
        }
        else
        {
            float normalizedSpeed = Mathf.Abs(currentSpeed) / maxSpeedNormal;
            multiplier *= (1f + normalizedSpeed * 0.8f);
            rotationEfficiency = 100f;

            if (currentSpeed < 0)
            {
                multiplier *= -1f;
            }
        }
    }

    // ===== MOVEMENT =====

    void ApplyRealisticMovement()
    {
        Vector3 movementDirection;

        if (collisionSystem.IsWallSliding && collisionSystem.SlideDirection != Vector3.zero)
        {
            movementDirection = collisionSystem.SlideDirection * Mathf.Abs(currentSpeed) * 0.5f;
        }
        else
        {
            movementDirection = myTransform.forward * currentSpeed;
        }

        movementDirection.y = movementVelocity.y;
        controller.Move(movementDirection * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            movementVelocity.y = -0.5f;
        }
        else
        {
            movementVelocity.y += gravity * Time.deltaTime;
            movementVelocity.y = Mathf.Max(movementVelocity.y, -20f);
        }
    }

    void EmergencyStop()
    {
        currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref currentAccelerationVelocity, emergencyBrakeTime * 0.5f);

        if (Mathf.Abs(currentSpeed) < 0.15f && !brakeLockEngaged)
        {
            currentSpeed = 0f;
            targetSpeed = 0f;
            currentAccelerationVelocity = 0f;
            brakeLockEngaged = true;

            SendHapticPulse(true, 0.7f, 0.1f);
            SendHapticPulse(false, 0.7f, 0.1f);
        }

        collisionSystem.ClearSlide();

        if (wheelController != null)
        {
            wheelController.StopWheels();
        }
    }

    // ===== HAPTIC FEEDBACK =====

    void ProcessHapticFeedback()
    {
        bool isColliding = collisionSystem.IsInCollision ||
                           collisionSystem.IsFrontBlocked ||
                           collisionSystem.IsBackBlocked;

        if (isColliding && !wasColliding)
        {
            float impactStrength = Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxSpeedNormal);
            float intensity = collisionHapticIntensity * impactStrength;
            SendHapticPulse(true, intensity, 0.2f);
            SendHapticPulse(false, intensity, 0.2f);
        }

        if (collisionSystem.IsWallSliding)
        {
            SendHapticPulse(true, slideHapticIntensity, Time.deltaTime);
            SendHapticPulse(false, slideHapticIntensity, Time.deltaTime);
        }

        wasColliding = isColliding;
    }

    private void SendHapticPulse(bool isLeft, float intensity, float duration)
    {
        if (inputModeSwitcher != null)
        {
            if (isLeft && !inputModeSwitcher.LeftSideActive) return;
            if (!isLeft && !inputModeSwitcher.RightSideActive) return;
        }

        XRControllerWithRumble device = isLeft ? leftRumbleDevice : rightRumbleDevice;
        if (device != null)
        {
            device.SendImpulse(intensity, duration);
        }
    }

    // ===== SOUND EFFECTS =====

    private void ProcessSoundEffects()
    {
        if (currentSteeringTypeCached != steeringTypeForSoundCache)
        {
            PlaySound(steeringChangeSound);
            steeringTypeForSoundCache = currentSteeringTypeCached;
        }

        bool slidingNow = collisionSystem.IsWallSliding;

        if (slidingNow)
        {
            float currentTime = Time.time;
            if (!slidingCache && currentTime - lastSlideSoundTime > slideSoundCooldown)
            {
                PlaySound(slideStartSound);
                lastSlideSoundTime = currentTime;
            }
        }

        slidingCache = slidingNow;
    }

    private void UpdateTimers()
    {
        if (tryingToTurnTime > 0)
        {
            tryingToTurnTime -= Time.deltaTime;
        }
    }

    // ===== COLLISION CALLBACK =====

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        collisionSystem.ProcessCollision(hit, currentSpeed, ref currentSpeed);
    }

    // ===== VRC: FOCUS / PAUSE HANDLING (Meta Store requirement) =====

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!pauseOnFocusLost) return;

        if (!hasFocus && wasFocusedBefore)
        {
            // Headset removed or user opened Meta menu
            inputLocked = true;

            if (stopOnFocusLost)
            {
                ForceStop();
            }

            Debug.Log("[MovementVR] Focus lost — input locked & wheelchair stopped");
        }
        else if (hasFocus && !wasFocusedBefore)
        {
            // User put headset back on
            inputLocked = false;
            Debug.Log("[MovementVR] Focus restored — input unlocked");
        }

        wasFocusedBefore = hasFocus;
    }

    // ===== VRC: CLEAN APPLICATION QUIT =====

    private void OnApplicationQuit()
    {
        // Ensure haptics are stopped
        if (leftRumbleDevice != null) leftRumbleDevice.SendImpulse(0f, 0f);
        if (rightRumbleDevice != null) rightRumbleDevice.SendImpulse(0f, 0f);

        Debug.Log("[MovementVR] Application quitting cleanly");
    }

    // ===== PUBLIC METHODS =====

    public float GetNormalizedSpeed() => currentSpeed / maxSpeedNormal;
    public bool IsMoving() => Mathf.Abs(currentSpeed) > 0.1f;
    public void ReduceSpeed(float multiplier) => currentSpeed *= multiplier;

    public void PlaySound(AudioClip clip)
    {
        if (effectsAudio != null && clip != null)
        {
            if (clip == hardCollisionSound)
            {
                if (Time.time - lastCollisionSoundTime < collisionSoundCooldown) return;
                lastCollisionSoundTime = Time.time;
            }
            effectsAudio.PlayOneShot(clip);
        }
    }

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsEmergencyBraking() => emergencyBrake;
    public string GetCurrentSteeringType() => currentSteeringTypeCached.ToString();

    public void LockInput() => inputLocked = true;
    public void UnlockInput() => inputLocked = false;

    /// <summary>
    /// Forces the wheelchair to a complete stop without animation.
    /// Use this when pausing, recentering, or in emergencies.
    /// </summary>
    public void ForceStop()
    {
        currentSpeed = 0f;
        targetSpeed = 0f;
        currentAccelerationVelocity = 0f;
        smoothedVerticalInput = 0f;
        smoothedHorizontalInput = 0f;
        movementVelocity = Vector3.zero;
        brakeLockEngaged = true;
        emergencyBrake = false;

        if (collisionSystem != null)
        {
            collisionSystem.ClearSlide();
        }

        if (wheelController != null)
        {
            wheelController.StopWheels();
        }
    }
}