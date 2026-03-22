using UnityEngine;
using UnityEngine.InputSystem; // Required for VR Input System
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main electric wheelchair movement controller
/// Responsible for: input, speed, acceleration, rotation and physics
/// Supports both VR Controllers (Meta Quest) and Keyboard fallback
/// </summary>
public class Movement : MonoBehaviour
{
    [Header("=== VR Input Actions ===")]
    [Tooltip("Reference to the VR Joystick (Vector2) - Map both Left and Right joysticks here")]
    public InputActionReference moveAction; 
    
    [Tooltip("Button to toggle between Slow/Normal Mode (Map to X, Y, or B buttons)")]
    public InputActionReference toggleSpeedAction;
    
    [Tooltip("Button to change Steering Type (e.g., Right Controller A button)")]
    public InputActionReference switchSteeringAction;
    
    [Tooltip("Button for Emergency Brake (e.g., Left or Right Triggers)")]
    public InputActionReference brakeAction;

    [Header("=== Interface Settings ===")]
    [Tooltip("Show the debug controls on screen?")]
    public bool showInterface = true;

    [Header("=== Speed Settings ===")]
    [Tooltip("Maximum speed in normal mode (km/h)")]
    public float maxSpeedNormal = 6f;

    [Tooltip("Maximum speed in slow/interior mode (km/h)")]
    public float maxSpeedSlow = 3f;

    [Tooltip("Reverse speed (km/h)")]
    public float reverseSpeed = 2f;

    [Header("=== Acceleration Settings ===")]
    [Tooltip("Time to reach maximum speed (seconds)")]
    public float accelerationTime = 2f;

    [Tooltip("Time to stop completely (seconds)")]
    public float brakingTime = 1.5f;

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

    [Header("=== Effect Sounds (One-Shot) ===")]
    [Tooltip("Audio launcher for short effects (clicks, collisions)")]
    public AudioSource effectsAudio;

    [Tooltip("Sound to play when changing speed mode (keys 1, 2 or VR button)")]
    public AudioClip modeChangeSound;

    [Tooltip("Sound to play when changing steering type (key T or VR button)")]
    public AudioClip steeringChangeSound;

    [Tooltip("Sound to play when hitting hard")]
    public AudioClip hardCollisionSound;

    [Tooltip("Sound to play when starting to slide on wall")]
    public AudioClip slideStartSound;

    [Tooltip("Minimum speed (in m/s) for collision sound to play (optional)")]
    public float minCollisionSpeed = 0.8f;

    [Header("=== Physics and Limits ===")]
    [Tooltip("Maximum slope it can climb (degrees)")]
    public float maxSlope = 10f;

    [Tooltip("Applied gravity")]
    public float gravity = -9.81f;

    [Header("=== Current State (Debug) ===")]
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float targetSpeed = 0f;
    [SerializeField] private bool emergencyBrake = false;
    [SerializeField] private string currentSteeringType = "Frontal";
    [SerializeField] private float rotationEfficiency = 100f;

    // Internal Components
    private CharacterController controller;
    private Vector3 movementVelocity;
    private WheelController wheelController;
    private CollisionSystem collisionSystem;

    // Smoothed input system variables
    private float smoothedVerticalInput = 0f;
    private float smoothedHorizontalInput = 0f;
    private float tryingToTurnTime = 0f;

    // Public variable for sound script to know if player is accelerating
    [HideInInspector]
    public bool playerIsAccelerating = false;

    // Cache for sounds (to prevent repeating audio clips)
    private bool slidingCache = false;
    private string steeringTypeCache = "Frontal";
    private bool inCollisionCache = false;

    // Sound cooldown system
    private float lastCollisionSoundTime = 0f;
    private float lastSlideSoundTime = 0f;
    private float soundCooldown = 1.0f; // 1 second cooldown

    public enum SpeedMode
    {
        Slow,
        Normal,
        Off
    }

    // --- Unity Input System Initialization ---
    private void OnEnable()
    {
        if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
        if (toggleSpeedAction != null && toggleSpeedAction.action != null) toggleSpeedAction.action.Enable();
        if (switchSteeringAction != null && switchSteeringAction.action != null) switchSteeringAction.action.Enable();
        if (brakeAction != null && brakeAction.action != null) brakeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
        if (toggleSpeedAction != null && toggleSpeedAction.action != null) toggleSpeedAction.action.Disable();
        if (switchSteeringAction != null && switchSteeringAction.action != null) switchSteeringAction.action.Disable();
        if (brakeAction != null && brakeAction.action != null) brakeAction.action.Disable();
    }

    void Start()
    {
        SetupCharacterController();
        SetupComponents();
        ConvertSpeeds();
        InitializeCache();
        InitializeLevelSettings();
    }

    /// <summary>
    /// Applies the starting speed and steering modes defined in the Inspector for this specific level.
    /// </summary>
    private void InitializeLevelSettings()
    {
        currentMode = startingSpeedMode;

        if (wheelController != null)
        {
            wheelController.SetSteeringType(startingSteeringMode);
        }
    }

    /// <summary>
    /// Configures CharacterController with optimized values for realistic physical contact
    /// </summary>
    private void SetupCharacterController()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        // Optimized values to ensure proper collision with doorways and obstacles
        controller.height = 0.8f;
        controller.radius = 0.17f;
        controller.center = new Vector3(0, 0.4f, 0);
        controller.skinWidth = 0.0001f;
        controller.minMoveDistance = 0.0f;
        controller.stepOffset = 0.08f;

        // Elevate slightly at start to avoid clipping through the floor
        transform.position += Vector3.up * 0.1f;
    }

    /// <summary>
    /// Initializes references to necessary external scripts on the same GameObject
    /// </summary>
    private void SetupComponents()
    {
        wheelController = GetComponent<WheelController>();

        collisionSystem = GetComponent<CollisionSystem>();
        if (collisionSystem == null)
        {
            collisionSystem = gameObject.AddComponent<CollisionSystem>();
        }
        collisionSystem.Initialize(controller, transform);
    }

    /// <summary>
    /// Converts km/h defined in inspector to m/s used by Unity's physics
    /// </summary>
    private void ConvertSpeeds()
    {
        maxSpeedNormal = maxSpeedNormal / 3.6f;
        maxSpeedSlow = maxSpeedSlow / 3.6f;
        reverseSpeed = reverseSpeed / 3.6f;
    }

    /// <summary>
    /// Initializes cache values for steering state changes
    /// </summary>
    private void InitializeCache()
    {
        if (wheelController != null)
        {
            steeringTypeCache = wheelController.GetSteeringType().ToString();
        }
    }

    void Update()
    {
        UpdateSteeringState();
        collisionSystem.Update();
        ProcessSoundEffects();
        UpdateTimers();

        ManageModes();

        // Process movement only if not in emergency stop mode
        if (currentMode != SpeedMode.Off)
        {
            ProcessRealisticInput();
            ApplyRealisticMovement();
        }
        else
        {
            EmergencyStop();
            ApplyVerticalMovement();
        }

        ApplyGravity();
    }

    /// <summary>
    /// Updates current steering string for UI and debug purposes
    /// </summary>
    private void UpdateSteeringState()
    {
        if (wheelController != null)
        {
            currentSteeringType = wheelController.GetSteeringType().ToString();
        }
    }

    /// <summary>
    /// Processes and plays sound effects based on collisions and state changes
    /// </summary>
    private void ProcessSoundEffects()
    {
        float currentTime = Time.time;

        // Play sound when steering type changes
        if (currentSteeringType != steeringTypeCache)
        {
            PlaySound(steeringChangeSound);
            steeringTypeCache = currentSteeringType;
        }

        bool slidingNow = collisionSystem.IsWallSliding;
        bool inCollisionNow = (collisionSystem.IsInCollision ||
                               collisionSystem.IsFrontBlocked ||
                               collisionSystem.IsBackBlocked);

        // Manage slide vs hard collision sounds
        if (slidingNow)
        {
            if (!slidingCache && currentTime - lastSlideSoundTime > soundCooldown)
            {
                PlaySound(slideStartSound);
                lastSlideSoundTime = currentTime;
            }
            inCollisionCache = inCollisionNow; 
        }
        else if (inCollisionNow)
        {
            if (!inCollisionCache && currentTime - lastCollisionSoundTime > soundCooldown)
            {
                PlaySound(hardCollisionSound);
                lastCollisionSoundTime = currentTime;
            }
        }

        slidingCache = slidingNow;
        if (!slidingNow) 
        {
            inCollisionCache = inCollisionNow;
        }
    }

    private void UpdateTimers()
    {
        if (tryingToTurnTime > 0)
        {
            tryingToTurnTime -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Handles shifting between speed modes and engaging emergency brakes
    /// </summary>
    void ManageModes()
    {
        // 1. Toggle Speed Mode (VR Input)
        bool toggleSpeed = false;
        if (toggleSpeedAction != null && toggleSpeedAction.action != null)
        {
            toggleSpeed = toggleSpeedAction.action.WasPressedThisFrame();
        }

        // Apply toggle logic
        if (toggleSpeed)
        {
            currentMode = (currentMode == SpeedMode.Slow) ? SpeedMode.Normal : SpeedMode.Slow;
            PlaySound(modeChangeSound);
        }

        // 2. PC Fallback (Keys 1 and 2 explicitly)
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode != SpeedMode.Slow)
        {
            currentMode = SpeedMode.Slow;
            PlaySound(modeChangeSound);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && currentMode != SpeedMode.Normal)
        {
            currentMode = SpeedMode.Normal;
            PlaySound(modeChangeSound);
        }

        // 3. Switch Steering
        bool steeringSwitchPressed = Input.GetKeyDown(KeyCode.T);
        if (switchSteeringAction != null && switchSteeringAction.action != null)
        {
            steeringSwitchPressed |= switchSteeringAction.action.WasPressedThisFrame();
        }

        // Add method call to your WheelController here if it handles the toggle directly
        // if (steeringSwitchPressed && wheelController != null) { wheelController.ToggleSteering(); }

        // 4. Emergency Brake (Hold to brake)
        bool brakeIsHeld = Input.GetKey(KeyCode.Space);
        if (brakeAction != null && brakeAction.action != null)
        {
            brakeIsHeld |= brakeAction.action.IsPressed();
        }
        
        if (brakeIsHeld && currentMode != SpeedMode.Off)
        {
            currentMode = SpeedMode.Off;
            emergencyBrake = true;
        }
        else if (!brakeIsHeld && emergencyBrake)
        {
            currentMode = SpeedMode.Normal; 
            emergencyBrake = false;
        }
    }

    /// <summary>
    /// Reads inputs from VR or Keyboard and calculates target speeds
    /// </summary>
    void ProcessRealisticInput()
    {
        float verticalInput = 0f;
        float horizontalInput = 0f;

        // Try reading from VR Joystick first
        if (moveAction != null && moveAction.action != null)
        {
            Vector2 stickValue = moveAction.action.ReadValue<Vector2>();
            horizontalInput = stickValue.x;
            verticalInput = stickValue.y;
        }
        
        // Fallback to Keyboard if VR input is completely zero
        if (Mathf.Abs(horizontalInput) < 0.01f && Mathf.Abs(verticalInput) < 0.01f)
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");
        }

        // Set acceleration flag for external sound scripts
        playerIsAccelerating = (Mathf.Abs(verticalInput) > 0.1f);

        // Apply input smoothing to simulate realistic joystick resistance
        float smoothing = 3f;
        smoothedVerticalInput = Mathf.Lerp(smoothedVerticalInput, verticalInput, smoothing * Time.deltaTime);
        smoothedHorizontalInput = Mathf.Lerp(smoothedHorizontalInput, horizontalInput, smoothing * Time.deltaTime);

        // Define target max speed based on current selected mode
        float maxSpeed = currentMode == SpeedMode.Slow ? maxSpeedSlow : maxSpeedNormal;

        ApplyCollisionBlocking(ref verticalInput, ref maxSpeed);
        ApplyAccelerationDeceleration(maxSpeed);
        ProcessRotation(smoothedHorizontalInput);
    }

    /// <summary>
    /// Restricts input if the wheelchair is physically blocked by an obstacle
    /// </summary>
    private void ApplyCollisionBlocking(ref float verticalInput, ref float maxSpeed)
    {
        if (collisionSystem.IsFrontBlocked && smoothedVerticalInput > 0)
        {
            smoothedVerticalInput = 0;
            targetSpeed = 0;

            // Apply slight pushback if player forces against a wall
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

    /// <summary>
    /// Gradually speeds up or slows down the wheelchair for realistic physics
    /// </summary>
    private void ApplyAccelerationDeceleration(float maxSpeed)
    {
        bool notBlocked = !collisionSystem.IsFrontBlocked && !collisionSystem.IsBackBlocked;
        bool accelerating = Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed);

        if (notBlocked && accelerating)
        {
            float acceleration = maxSpeed / accelerationTime;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            float deceleration = maxSpeed / brakingTime;

            if (collisionSystem.IsFrontBlocked || collisionSystem.IsBackBlocked)
            {
                currentSpeed = 0;
            }
            else if (collisionSystem.IsInCollision)
            {
                // Decelerate faster if rubbing against a wall
                deceleration *= 2f;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Rotates the wheelchair considering the type of steering configuration
    /// </summary>
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

            // Invert rotation when reversing with rear wheels
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

            // Invert steering visually when moving backwards
            if (currentSpeed < 0)
            {
                multiplier *= -1f; 
            }
        }
    }

    /// <summary>
    /// Moves the Character Controller in the world space based on speed and rotation
    /// </summary>
    void ApplyRealisticMovement()
    {
        Vector3 movementDirection = Vector3.zero;

        // Apply slide vector if scraping against a wall
        if (collisionSystem.IsWallSliding && collisionSystem.SlideDirection != Vector3.zero)
        {
            movementDirection = collisionSystem.SlideDirection * Mathf.Abs(currentSpeed) * 0.5f;
        }
        else
        {
            movementDirection = transform.forward * currentSpeed;
        }

        // Retain gravity forces
        movementDirection.y = movementVelocity.y;

        controller.Move(movementDirection * Time.deltaTime);
    }

    /// <summary>
    /// Applies only Y-axis forces, typically used when the chair is turned off
    /// </summary>
    void ApplyVerticalMovement()
    {
        Vector3 verticalMovement = new Vector3(0, movementVelocity.y, 0);
        controller.Move(verticalMovement * Time.deltaTime);
    }

    /// <summary>
    /// Handles falling and staying grounded
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            movementVelocity.y = -0.5f; // Keep grounded tightly
        }
        else
        {
            movementVelocity.y += gravity * Time.deltaTime;
            movementVelocity.y = Mathf.Max(movementVelocity.y, -20f); // Terminal velocity cap
        }
    }

    /// <summary>
    /// Immediately halts all forward/backward movement
    /// </summary>
    void EmergencyStop()
    {
        currentSpeed = 0;
        targetSpeed = 0;

        collisionSystem.ClearSlide();

        if (wheelController != null)
        {
            wheelController.StopWheels();
        }
    }

    /// <summary>
    /// Character Controller collision event listener
    /// </summary>
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
            effectsAudio.PlayOneShot(clip);
        }
    }

    // ===== GRAPHICAL INTERFACE (PT-PT) =====

    void OnGUI()
    {
        // Cancel drawing if hidden or game is paused
        if (!showInterface || Time.timeScale == 0) return;

        // Modern styling with gradient-like semi-transparent background
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.18f, 0.22f, 0.75f));
        boxStyle.border = new RectOffset(8, 8, 8, 8);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = Color.white;

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(0.5f, 0.95f, 1f, 1f);

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 14;
        valueStyle.fontStyle = FontStyle.Bold;

        // ===== LEFT - INFO =====
        GUI.Box(new Rect(15, 15, 240, 110), "", boxStyle);

        // Header
        GUI.Label(new Rect(30, 22, 200, 25), "CADEIRA DE RODAS", headerStyle);

        // Elegant separator line with glow effect
        GUI.color = new Color(0.5f, 0.95f, 1f, 0.6f);
        GUI.DrawTexture(new Rect(30, 48, 195, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Display Mode
        string modeText = currentMode == SpeedMode.Slow ? "Interior" :
                         (currentMode == SpeedMode.Off ? "Desligado" : "Exterior");
        
        Color modeColor = currentMode == SpeedMode.Slow ? new Color(1f, 0.9f, 0.5f, 1f) :
                         (currentMode == SpeedMode.Off ? new Color(1f, 0.6f, 0.6f, 1f) : new Color(0.6f, 1f, 0.7f, 1f));

        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 58, 90, 22), "Modo:", labelStyle);
        valueStyle.normal.textColor = modeColor;
        GUI.Label(new Rect(120, 58, 120, 22), modeText, valueStyle);

        // Display Speed
        float maxDisplaySpeed = currentMode == SpeedMode.Slow ? 3f : 6f;
        string speedText = $"{(currentSpeed * 3.6f):F1}/{maxDisplaySpeed:F0} km/h";
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 78, 90, 22), "Veloc:", labelStyle);
        valueStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(120, 78, 120, 22), speedText, valueStyle);

        // Display Steering Setup
        string steeringText = currentSteeringType.Contains("Rear") ? "Traseira" : "Frontal";
        Color steeringColor = currentSteeringType.Contains("Rear") ? new Color(1f, 0.75f, 1f, 1f) : new Color(0.65f, 0.95f, 1f, 1f);
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 96, 90, 22), "Direção:", labelStyle);
        valueStyle.normal.textColor = steeringColor;
        GUI.Label(new Rect(120, 96, 120, 22), steeringText, valueStyle);

        // ===== EMERGENCY BRAKE (CENTER TOP) =====
        if (emergencyBrake)
        {
            GUIStyle emergencyBoxStyle = new GUIStyle(GUI.skin.box);
            emergencyBoxStyle.normal.background = MakeTex(2, 2, new Color(0.9f, 0.2f, 0.2f, 0.85f));

            GUI.Box(new Rect(Screen.width / 2 - 150, 15, 300, 40), "", emergencyBoxStyle);

            GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.fontSize = 16;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.alignment = TextAnchor.MiddleCenter;
            warningStyle.normal.textColor = Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 150, 22, 300, 26), "⚠ TRAVÃO DE EMERGÊNCIA ⚠", warningStyle);
        }

        // ===== RIGHT - CONTROLS =====
        float rightX = Screen.width - 240 - 15;

        GUI.Box(new Rect(rightX, 15, 240, 138), "", boxStyle);

        headerStyle.normal.textColor = new Color(0.6f, 1f, 0.7f, 1f);
        GUI.Label(new Rect(rightX + 15, 22, 200, 25), "CONTROLOS", headerStyle);

        GUI.color = new Color(0.6f, 1f, 0.7f, 0.6f);
        GUI.DrawTexture(new Rect(rightX + 15, 48, 210, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle keyStyle = new GUIStyle(GUI.skin.label);
        keyStyle.fontSize = 13;
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.normal.textColor = new Color(1f, 0.95f, 0.7f, 1f);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = 13;
        descStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        int y = 58;
        int lineH = 18;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "WASD/Setas", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Mover", descStyle);
        y += lineH;

        // Updated interface text to reflect the toggle functionality
        GUI.Label(new Rect(rightX + 20, y, 100, 18), "Botão VR / 1/2", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Alternar Modo", descStyle);
        y += lineH;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "T", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Mudar Direção", descStyle);
        y += lineH;

        keyStyle.normal.textColor = new Color(1f, 0.7f, 0.7f, 1f);
        GUI.Label(new Rect(rightX + 20, y, 100, 18), "ESPAÇO", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Travão", descStyle);
    }

    /// <summary>
    /// Helper to create colored textures for GUI backgrounds
    /// </summary>
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}