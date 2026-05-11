using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Electric wheelchair movement controller - VR Version (Meta Quest 3)
/// Simulates realistic electric wheelchair joystick control
/// Left Thumbstick = Wheelchair Joystick (forward/back/turn)
/// Haptic feedback on collisions, braking and speed limits
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

    [Tooltip("Haptic intensity for collisions (0-1)")]
    [Range(0f, 1f)]
    public float collisionHapticIntensity = 0.6f;

    [Tooltip("Haptic intensity for wall sliding (0-1)")]
    [Range(0f, 1f)]
    public float slideHapticIntensity = 0.25f;

    [Tooltip("Haptic intensity for braking (0-1)")]
    [Range(0f, 1f)]
    public float brakeHapticIntensity = 0.3f;

    [Header("=== Speed Settings ===")]
    [Tooltip("Maximum speed in normal mode (km/h)")]
    public float maxSpeedNormal = 8f;

    [Tooltip("Maximum speed in slow/interior mode (km/h)")]
    public float maxSpeedSlow = 3f;

    [Tooltip("Reverse speed (km/h)")]
    public float reverseSpeed = 2f;

    [Header("=== Joystick Feel ===")]
    [Tooltip("Dead zone for the thumbstick - ignores tiny inputs (real joysticks have this)")]
    [Range(0.05f, 0.3f)]
    public float joystickDeadzone = 0.12f;

    [Tooltip("How aggressively the joystick input curves (1 = linear, 2+ = more precision at low speeds)")]
    [Range(1f, 3f)]
    public float joystickCurve = 1.8f;

    [Tooltip("Smoothing applied to joystick input (simulates physical resistance)")]
    [Range(1f, 10f)]
    public float joystickSmoothing = 4f;

    [Header("=== Acceleration Settings ===")]
    [Tooltip("Time to reach maximum speed (seconds)")]
    public float accelerationTime = 2.5f;

    [Tooltip("Time to stop completely when releasing joystick (seconds)")]
    public float brakingTime = 1.5f;

    [Tooltip("Time to stop with emergency brake (seconds)")]
    public float emergencyBrakeTime = 0.4f;

    [Header("=== Rotation Settings ===")]
    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 90f;

    [Tooltip("Can rotate without moving forward/backward? (Only works with front steering)")]
    public bool rotationInPlace = false;

    [Header("=== Level Start Settings ===")]
    [Tooltip("The speed mode this specific level should start with.")]
    public SpeedMode startingSpeedMode = SpeedMode.Slow;

    [Tooltip("The steering mode this specific level should start with.")]
    public WheelController.SteeringType startingSteeringMode = WheelController.SteeringType.FrontSteering;

    [Header("=== Driving Modes ===")]
    [Tooltip("Current speed mode")]
    public SpeedMode currentMode = SpeedMode.Normal;

    [Header("=== Effect Sounds ===")]
    public AudioSource effectsAudio;
    public AudioClip modeChangeSound;
    public AudioClip steeringChangeSound;
    public AudioClip hardCollisionSound;
    public AudioClip slideStartSound;
    public float minCollisionSpeed = 0.8f;

    [Header("=== Sound Cooldowns ===")]
    [Tooltip("Minimum time between collision sounds (seconds)")]
    public float collisionSoundCooldown = 0.5f;

    [Tooltip("Minimum time between slide sounds (seconds)")]
    public float slideSoundCooldown = 0.8f;

    [Header("=== Physics and Limits ===")]
    public float maxSlope = 10f;
    public float gravity = -9.81f;

    [Header("=== Current State (Debug) ===")]
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float targetSpeed = 0f;
    [SerializeField] private bool emergencyBrake = false;
    [SerializeField] private string currentSteeringType = "Frontal";
    [SerializeField] private float rotationEfficiency = 100f;
    [SerializeField] private Vector2 rawJoystickInput = Vector2.zero;
    [SerializeField] private Vector2 processedJoystickInput = Vector2.zero;

    // Internal Components
    private CharacterController controller;
    private Vector3 movementVelocity;
    private WheelController wheelController;
    private CollisionSystemVR collisionSystem;

    // Smoothed input
    private float smoothedVerticalInput = 0f;
    private float smoothedHorizontalInput = 0f;
    private float tryingToTurnTime = 0f;

    // Realistic Physics State
    private float currentAccelerationVelocity = 0f;
    private bool brakeLockEngaged = true;

    private float previousSpeed = 0f;

    private SpeedMode modeBeforeBrake = SpeedMode.Slow;

    // Public for sound script
    [HideInInspector]
    public bool playerIsAccelerating = false;

    // Lock for countdown
    [HideInInspector] public bool inputLocked = false;

    // Sound cache
    private bool slidingCache = false;
    private string steeringTypeCache = "Frontal";
    private float lastCollisionSoundTime = 0f;
    private float lastSlideSoundTime = 0f;

    // Haptic state
    private bool wasColliding = false;

    public enum SpeedMode
    {
        Slow,
        Normal,
        Off
    }

    // --- Input System Enable/Disable ---
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

    void Start()
    {
        SetupCharacterController();
        SetupComponents();
        ConvertSpeeds();
        InitializeCache();
        InitializeLevelSettings();
        PreloadSounds();
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
        // If the user chose a steering type from the main menu, use that.
        // Otherwise fall back to the level's own default.
        WheelController.SteeringType chosenSteering = SteeringPreference.HasUserChosen
            ? SteeringPreference.CurrentSteering
            : startingSteeringMode;

        wheelController.SetSteeringType(chosenSteering);
        Debug.Log($"[MovementVR] Steering set to: {chosenSteering} (user chose: {SteeringPreference.HasUserChosen})");
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
        collisionSystem.Initialize(controller, transform);
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
            steeringTypeCache = wheelController.GetSteeringType().ToString();
        }
    }

    void Update()
    {
        if (inputLocked) return;

        UpdateSteeringState();
        collisionSystem.Update();
        ProcessSoundEffects();
        ProcessHapticFeedback();
        UpdateTimers();

        ManageModes();

        // Control logic separated from physics application
        if (currentMode != SpeedMode.Off)
        {
            ProcessJoystickInput();
        }
        else
        {
            EmergencyStop();
        }

        // Apply movement ALWAYS so SmoothDamp inertia works during braking
        ApplyRealisticMovement();
        ApplyGravity();
    }

    private void UpdateSteeringState()
    {
        if (wheelController != null)
        {
            currentSteeringType = wheelController.GetSteeringType().ToString();
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
        else if (joystickAction != null && joystickAction.action != null)
        {
            rawInput = joystickAction.action.ReadValue<Vector2>();
        }
        rawJoystickInput = rawInput;

        // Apply deadzone
        float magnitude = rawInput.magnitude;
        if (magnitude < joystickDeadzone)
        {
            rawInput = Vector2.zero;
        }
        else
        {
            float remapped = (magnitude - joystickDeadzone) / (1f - joystickDeadzone);
            rawInput = rawInput.normalized * remapped;
        }

        // Apply response curve
        float curvedMagnitude = Mathf.Pow(rawInput.magnitude, joystickCurve);
        Vector2 curvedInput = rawInput.normalized * curvedMagnitude;

        // Apply input smoothing
        smoothedVerticalInput = Mathf.Lerp(smoothedVerticalInput, curvedInput.y, joystickSmoothing * Time.deltaTime);
        smoothedHorizontalInput = Mathf.Lerp(smoothedHorizontalInput, curvedInput.x, joystickSmoothing * Time.deltaTime);

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
        if (toggleSpeedAction != null && toggleSpeedAction.action != null)
        {
            if (toggleSpeedAction.action.WasPressedThisFrame())
            {
                currentMode = (currentMode == SpeedMode.Slow) ? SpeedMode.Normal : SpeedMode.Slow;
                PlaySound(modeChangeSound);
                SendHapticPulse(leftHapticAction, 0.15f, 0.08f);
            }
        }

        if (switchSteeringAction != null && switchSteeringAction.action != null)
        {
            if (switchSteeringAction.action.WasPressedThisFrame())
            {
                // wheelController.ToggleSteering(); 
                PlaySound(steeringChangeSound);
                SendHapticPulse(rightHapticAction, 0.15f, 0.08f);
            }
        }

        bool brakeIsHeld = false;
        if (brakeAction != null && brakeAction.action != null)
        {
            brakeIsHeld = brakeAction.action.IsPressed();
        }

        if (brakeIsHeld && currentMode != SpeedMode.Off)
        {
            modeBeforeBrake = currentMode; // <-- Guarda o modo em que estavas!
            currentMode = SpeedMode.Off;
            emergencyBrake = true;
            SendHapticPulse(leftHapticAction, brakeHapticIntensity, 0.15f);
            SendHapticPulse(rightHapticAction, brakeHapticIntensity, 0.15f);
        }
        else if (!brakeIsHeld && emergencyBrake)
        {
            currentMode = modeBeforeBrake; // <-- Restaura o modo exato que guardou!
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

        // REALISTIC INERTIA: SmoothDamp creates an S-curve for heavy wheelchair acceleration
        if (!blockedInTargetDirection && accelerating)
        {
            // Only release the brake lock if the user is clearly trying to move (not micro-noise)
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

            // MECHANICAL BRAKE CLICK: Locks the wheels completely when almost stopped.
            // Only fires once per real stop: requires that we were actually moving (previousSpeed had real magnitude)
            // before reaching the near-zero state.
            if (Mathf.Abs(targetSpeed) < 0.05f && Mathf.Abs(currentSpeed) < 0.15f && !brakeLockEngaged && Mathf.Abs(previousSpeed) > 0.2f)
            {
                currentSpeed = 0f;
                targetSpeed = 0f;
                currentAccelerationVelocity = 0f;
                brakeLockEngaged = true;

                SendHapticPulse(leftHapticAction, 0.4f, 0.05f);
                SendHapticPulse(rightHapticAction, 0.4f, 0.05f);
            }
            else if (Mathf.Abs(targetSpeed) < 0.05f && Mathf.Abs(currentSpeed) < 0.05f && !brakeLockEngaged)
            {
                // Silently engage the lock without haptic feedback (avoids pulse spam at rest)
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
            isRearSteering = wheelController.GetSteeringType() == WheelController.SteeringType.RearSteering;
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
        transform.Rotate(0, rotation, 0);
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
        Vector3 movementDirection = Vector3.zero;

        if (collisionSystem.IsWallSliding && collisionSystem.SlideDirection != Vector3.zero)
        {
            movementDirection = collisionSystem.SlideDirection * Mathf.Abs(currentSpeed) * 0.5f;
        }
        else
        {
            movementDirection = transform.forward * currentSpeed;
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
        // Smoothly but aggressively ramp down speed instead of stopping instantly
        currentSpeed = Mathf.SmoothDamp(currentSpeed, 0f, ref currentAccelerationVelocity, emergencyBrakeTime * 0.5f);

        // Heavy mechanical lock when almost completely stopped
        if (Mathf.Abs(currentSpeed) < 0.15f && !brakeLockEngaged)
        {
            currentSpeed = 0f;
            targetSpeed = 0f;
            currentAccelerationVelocity = 0f;
            brakeLockEngaged = true;

            // Stronger haptic feedback to simulate the emergency brake pads engaging
            SendHapticPulse(leftHapticAction, 0.7f, 0.1f);
            SendHapticPulse(rightHapticAction, 0.7f, 0.1f);
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
            SendHapticPulse(leftHapticAction, intensity, 0.2f);
            SendHapticPulse(rightHapticAction, intensity, 0.2f);
        }

        if (collisionSystem.IsWallSliding)
        {
            SendHapticPulse(leftHapticAction, slideHapticIntensity, Time.deltaTime);
            SendHapticPulse(rightHapticAction, slideHapticIntensity, Time.deltaTime);
        }

        wasColliding = isColliding;
    }

    private void SendHapticPulse(InputActionReference hapticRef, float intensity, float duration)
    {
        if (hapticRef == null || hapticRef.action == null) return;

        // Skip if this side's controller is resting
        if (inputModeSwitcher != null)
        {
            if (hapticRef == leftHapticAction && !inputModeSwitcher.LeftSideActive) return;
            if (hapticRef == rightHapticAction && !inputModeSwitcher.RightSideActive) return;
        }

        // Find the XR controller device that owns this action's binding and send a haptic impulse
        foreach (var control in hapticRef.action.controls)
        {
            if (control.device is UnityEngine.InputSystem.XR.XRControllerWithRumble rumbleDevice)
            {
                rumbleDevice.SendImpulse(intensity, duration);
                return;
            }
        }
    }

    // ===== SOUND EFFECTS =====

    private void ProcessSoundEffects()
    {
        float currentTime = Time.time;

        if (currentSteeringType != steeringTypeCache)
        {
            PlaySound(steeringChangeSound);
            steeringTypeCache = currentSteeringType;
        }

        bool slidingNow = collisionSystem.IsWallSliding;

        if (slidingNow)
        {
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

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        collisionSystem.ProcessCollision(hit, currentSpeed, ref currentSpeed);
    }

    // ===== PUBLIC METHODS =====

    public float GetNormalizedSpeed()
    {
        return currentSpeed / maxSpeedNormal;
    }

    public bool IsMoving()
    {
        return Mathf.Abs(currentSpeed) > 0.1f;
    }

    public void ReduceSpeed(float multiplier)
    {
        currentSpeed *= multiplier;
    }

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

    public float GetCurrentSpeed() { return currentSpeed; }
    public bool IsEmergencyBraking() { return emergencyBrake; }
    public string GetCurrentSteeringType() { return currentSteeringType; }

    public void LockInput() { inputLocked = true; }
    public void UnlockInput() { inputLocked = false; }

    
}