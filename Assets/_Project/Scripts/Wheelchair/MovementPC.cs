using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Electric wheelchair movement controller - PC Version (Keyboard Only)
/// Responsible for: input, speed, acceleration, rotation and physics
/// Sound effects play immediately on collision impact, not delayed by frame order
/// </summary>
public class MovementPC : MonoBehaviour
{
    [Header("=== Interface Settings ===")]
    [Tooltip("Show the debug controls on screen?")]
    public bool showInterface = true;

    [Header("=== Speed Settings ===")]
    [Tooltip("Maximum speed in normal mode (km/h)")]
    public float maxSpeedNormal = 8f;

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

    [Tooltip("Sound to play when changing speed mode")]
    public AudioClip modeChangeSound;

    [Tooltip("Sound to play when changing steering type")]
    public AudioClip steeringChangeSound;

    [Tooltip("Sound to play when hitting hard")]
    public AudioClip hardCollisionSound;

    [Tooltip("Sound to play when starting to slide on wall")]
    public AudioClip slideStartSound;

    [Header("=== Sound Cooldowns ===")]
    [Tooltip("Minimum time between collision sounds (seconds)")]
    public float collisionSoundCooldown = 0.5f;

    [Tooltip("Minimum time between slide sounds (seconds)")]
    public float slideSoundCooldown = 0.8f;

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

    // Cache for sounds
    private string steeringTypeCache = "Frontal";
    private bool wasSlidingLastFrame = false;

    // Sound cooldown timers
    private float lastCollisionSoundTime = -10f;
    private float lastSlideSoundTime = -10f;

    // Speed before collision (captured each frame for collision sound check)
    private float speedBeforeCollision = 0f;

    public enum SpeedMode
    {
        Slow,
        Normal,
        Off
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

    private void InitializeLevelSettings()
    {
        currentMode = startingSpeedMode;

        if (wheelController != null)
        {
            wheelController.SetSteeringType(startingSteeringMode);
        }
    }

    private void PreloadSounds()
    {
        if (effectsAudio == null) return;

        effectsAudio.playOnAwake = false;
        effectsAudio.spatialBlend = 0f;
        effectsAudio.priority = 0;

        if (modeChangeSound != null) modeChangeSound.LoadAudioData();
        if (steeringChangeSound != null) steeringChangeSound.LoadAudioData();
        if (hardCollisionSound != null) hardCollisionSound.LoadAudioData();
        if (slideStartSound != null) slideStartSound.LoadAudioData();
    }

    private void SetupCharacterController()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        controller.height = 0.8f;
        controller.radius = 0.17f;
        controller.center = new Vector3(0, 0.4f, 0);
        controller.skinWidth = 0.0001f;
        controller.minMoveDistance = 0.0f;
        controller.stepOffset = 0.08f;

        transform.position += Vector3.up * 0.1f;
    }

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

        // Pause handling (also unlocks cursor and shows it when paused)
        if (Time.timeScale == 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        // Save speed BEFORE collision processing so we can check it for sounds
        speedBeforeCollision = Mathf.Abs(currentSpeed);

        UpdateSteeringState();
        collisionSystem.Update();
        ProcessSlideSound();
        UpdateTimers();

        ManageModes();

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

    private void UpdateSteeringState()
    {
        if (wheelController != null)
        {
            string newSteering = wheelController.GetSteeringType().ToString();
            if (newSteering != steeringTypeCache)
            {
                PlaySound(steeringChangeSound);
                steeringTypeCache = newSteering;
            }
        }
    }

    /// <summary>
    /// Only handles slide sounds. Collision sounds are handled in OnControllerColliderHit
    /// for instant response (no frame delay).
    /// </summary>
    private void ProcessSlideSound()
    {
        bool slidingNow = collisionSystem.IsWallSliding;

        // Play slide sound when sliding starts
        if (slidingNow && !wasSlidingLastFrame)
        {
            if (Time.time - lastSlideSoundTime > slideSoundCooldown)
            {
                PlaySound(slideStartSound);
                lastSlideSoundTime = Time.time;
            }
        }

        wasSlidingLastFrame = slidingNow;
    }

    private void UpdateTimers()
    {
        if (tryingToTurnTime > 0)
        {
            tryingToTurnTime -= Time.deltaTime;
        }
    }

    void ManageModes()
    {
        // Speed mode switching (Keys 1 and 2)
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

        // Emergency Brake (Space - hold)
        bool brakeIsHeld = Input.GetKey(KeyCode.Space);

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

    void ProcessRealisticInput()
    {
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        playerIsAccelerating = (Mathf.Abs(verticalInput) > 0.1f);

        float smoothing = 3f;
        smoothedVerticalInput = Mathf.Lerp(smoothedVerticalInput, verticalInput, smoothing * Time.deltaTime);
        smoothedHorizontalInput = Mathf.Lerp(smoothedHorizontalInput, horizontalInput, smoothing * Time.deltaTime);

        float maxSpeed = currentMode == SpeedMode.Slow ? maxSpeedSlow : maxSpeedNormal;

        ApplyCollisionBlocking(ref verticalInput, ref maxSpeed);
        ApplyAccelerationDeceleration(maxSpeed);
        ProcessRotation(smoothedHorizontalInput);
    }

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
                deceleration *= 2f;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
        }
    }

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

    void ApplyVerticalMovement()
    {
        Vector3 verticalMovement = new Vector3(0, movementVelocity.y, 0);
        controller.Move(verticalMovement * Time.deltaTime);
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
        currentSpeed = 0;
        targetSpeed = 0;

        collisionSystem.ClearSlide();

        if (wheelController != null)
        {
            wheelController.StopWheels();
        }
    }


    /// <summary>
    /// Collision sounds are played HERE
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
            // Check cooldown specifically for collision sound
            if (clip == hardCollisionSound)
            {
                if (Time.time - lastCollisionSoundTime < collisionSoundCooldown) return;
                lastCollisionSoundTime = Time.time;
            }

            effectsAudio.PlayOneShot(clip);
        }
    }

    // ===== GRAPHICAL INTERFACE (PT-PT) =====

    void OnGUI()
    {
        if (!showInterface || Time.timeScale == 0) return;

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

        GUI.Label(new Rect(30, 22, 200, 25), "CADEIRA DE RODAS", headerStyle);

        GUI.color = new Color(0.5f, 0.95f, 1f, 0.6f);
        GUI.DrawTexture(new Rect(30, 48, 195, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string modeText = currentMode == SpeedMode.Slow ? "Interior" :
                         (currentMode == SpeedMode.Off ? "Desligado" : "Exterior");

        Color modeColor = currentMode == SpeedMode.Slow ? new Color(1f, 0.9f, 0.5f, 1f) :
                         (currentMode == SpeedMode.Off ? new Color(1f, 0.6f, 0.6f, 1f) : new Color(0.6f, 1f, 0.7f, 1f));

        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 58, 90, 22), "Modo:", labelStyle);
        valueStyle.normal.textColor = modeColor;
        GUI.Label(new Rect(120, 58, 120, 22), modeText, valueStyle);

        float maxDisplaySpeed = currentMode == SpeedMode.Slow ? 3f : 8f;
        string speedText = $"{(currentSpeed * 3.6f):F1}/{maxDisplaySpeed:F0} km/h";
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 78, 90, 22), "Veloc:", labelStyle);
        valueStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(120, 78, 120, 22), speedText, valueStyle);

        string steeringText = currentSteeringType.Contains("Rear") ? "Traseira" : "Frontal";
        Color steeringColor = currentSteeringType.Contains("Rear") ? new Color(1f, 0.75f, 1f, 1f) : new Color(0.65f, 0.95f, 1f, 1f);
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 96, 90, 22), "Direção:", labelStyle);
        valueStyle.normal.textColor = steeringColor;
        GUI.Label(new Rect(120, 96, 120, 22), steeringText, valueStyle);

        // ===== EMERGENCY BRAKE =====
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

            GUI.Label(new Rect(Screen.width / 2 - 150, 22, 300, 26), "TRAVÃO DE EMERGÊNCIA", warningStyle);
        }

        // ===== RIGHT - CONTROLS =====
        float rightX = Screen.width - 240 - 15;

        GUI.Box(new Rect(rightX, 15, 240, 120), "", boxStyle);

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

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "1 / 2", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Modo Lento/Normal", descStyle);
        y += lineH;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "T", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Mudar Direção", descStyle);
        y += lineH;

        keyStyle.normal.textColor = new Color(1f, 0.7f, 0.7f, 1f);
        GUI.Label(new Rect(rightX + 20, y, 100, 18), "ESPAÇO", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Travão", descStyle);
    }

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