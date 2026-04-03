using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Complete wheelchair wheel control system
/// Manages steering, spinning and differential wheel movement based on true physics.
/// Fully compatible with both PC Keyboard and VR Joysticks.
/// NOTE: Sound effects are handled by the Movement scripts, NOT here.
/// </summary>
public class WheelController : MonoBehaviour
{
    [Header("=== Steering Joints ===")]
    [Tooltip("Front wheel center joint - controls steering")]
    public Transform joint4_FrontSteering;

    [Tooltip("Rear wheel center joint - controls steering")]
    public Transform joint5_RearSteering;

    [Header("=== Wheel Rotation Joints ===")]
    [Tooltip("Front left wheel joint - spins the wheel")]
    public Transform joint6_FrontLeftWheel;

    [Tooltip("Front right wheel joint - spins the wheel")]
    public Transform joint7_FrontRightWheel;

    [Tooltip("Rear left wheel joint - spins the wheel")]
    public Transform joint8_RearLeftWheel;

    [Tooltip("Rear right wheel joint - spins the wheel")]
    public Transform joint9_RearRightWheel;

    [Header("=== Wheelchair Type ===")]
    [Tooltip("Wheelchair steering type")]
    public SteeringType steeringType = SteeringType.FrontSteering;

    [Tooltip("Key to toggle steering type (PC Fallback)")]
    public KeyCode toggleSteeringKey = KeyCode.T;

    [Header("=== Physical Configuration ===")]
    [Tooltip("Maximum wheelchair speed in km/h (Used as fallback if no movement script found)")]
    public float maxSpeedKmH = 6f;

    [Tooltip("Rear wheel diameter in meters. Crucial for realistic rotation speed!")]
    public float rearWheelDiameter = 0.6f;

    [Tooltip("Front wheel diameter in meters. Crucial for realistic rotation speed!")]
    public float frontWheelDiameter = 0.15f;

    [Header("=== Steering Configuration ===")]
    [Tooltip("Maximum steering angle")]
    [Range(0f, 45f)]
    public float maxSteeringAngle = 30f;

    [Tooltip("Steering speed")]
    [Range(1f, 10f)]
    public float steeringSpeed = 5f;

    [Header("=== Rotation Configuration ===")]
    [Tooltip("Make wheels rotate differentially in turns")]
    public bool differentialRotation = true;

    [Tooltip("Differential rotation intensity")]
    [Range(0f, 2f)]
    public float differentialIntensity = 0.5f;

    [Tooltip("Invert rotation direction if your 3D model axes are flipped")]
    public bool invertRotation = false;

    [Header("=== Debug Info ===")]
    [SerializeField] private float rotationFrontLeft = 0f;
    [SerializeField] private float rotationFrontRight = 0f;
    [SerializeField] private float rotationRearLeft = 0f;
    [SerializeField] private float rotationRearRight = 0f;
    [SerializeField] private float currentSteeringAngle = 0f;
    [SerializeField] private float currentSpeed = 0f; // Exact speed in m/s
    [SerializeField] private float steeringInput = 0f;

    public enum SteeringType
    {
        FrontSteering,
        RearSteering
    }

    // Component references
    private MovementPC movementPC;
    private MovementVR movementVR;
    private Rigidbody rb;

    // Initial joint rotations
    private Quaternion initialRotJoint4;
    private Quaternion initialRotJoint5;
    private Quaternion initialRotJoint6;
    private Quaternion initialRotJoint7;
    private Quaternion initialRotJoint8;
    private Quaternion initialRotJoint9;

    // For manual speed calculation (fallback)
    private Vector3 previousPosition;

    // Rotation axes
    private readonly Vector3 ROTATION_AXIS = Vector3.forward;
    private readonly Vector3 STEERING_AXIS = Vector3.up;

    // ===== HELPER METHODS =====

    private bool HasMovementScript()
    {
        return movementPC != null || movementVR != null;
    }

    private void SetRotationSpeed(float speed)
    {
        if (movementPC != null) movementPC.rotationSpeed = speed;
        if (movementVR != null) movementVR.rotationSpeed = speed;
    }

    private void SetRotationInPlace(bool value)
    {
        if (movementPC != null) movementPC.rotationInPlace = value;
        if (movementVR != null) movementVR.rotationInPlace = value;
    }

    // ===== INITIALIZATION =====

    void Awake()
    {
        InitializeComponents();
        FindJointsAutomatically();
        StoreInitialRotations();
        VerifyConfiguration();
        ConfigureMovementScript();
    }

    void Update()
    {
        // PC Keyboard fallback for toggling steering
        if (Input.GetKeyDown(toggleSteeringKey))
        {
            ToggleSteeringType();
        }

        GetInputs();
        ApplySteering();
        ApplyWheelRotation();
    }

    private void InitializeComponents()
    {
        movementPC = GetComponent<MovementPC>();
        movementVR = GetComponent<MovementVR>();
        rb = GetComponent<Rigidbody>();
        previousPosition = transform.position;
    }

    private void ConfigureMovementScript()
    {
        if (steeringType == SteeringType.RearSteering)
        {
            SetRotationSpeed(60f);
            SetRotationInPlace(true);
        }
        else
        {
            SetRotationSpeed(45f);
            SetRotationInPlace(false);
        }
    }

    public void ToggleSteeringType()
    {
        if (steeringType == SteeringType.FrontSteering)
            steeringType = SteeringType.RearSteering;
        else
            steeringType = SteeringType.FrontSteering;

        ResetSteering();
        ConfigureMovementScript();
    }

    void GetInputs()
    {
        steeringInput = GetDynamicSteeringInput();

        // Use EXACT physical speed (m/s) instead of fake normalized multipliers
        if (HasMovementScript())
        {
            if (movementVR != null) 
            {
                currentSpeed = movementVR.GetCurrentSpeed();
            }
            else if (movementPC != null) 
            {
                // Safe fallback for PC script if it doesn't have GetCurrentSpeed() yet
                currentSpeed = movementPC.GetNormalizedSpeed() * (maxSpeedKmH / 3.6f); 
            }
        }
        else if (rb != null)
        {
            currentSpeed = rb.linearVelocity.magnitude;
            // Check if moving backwards
            if (Vector3.Dot(rb.linearVelocity, transform.forward) < 0) currentSpeed = -currentSpeed;
        }
        else
        {
            Vector3 movementVector = transform.position - previousPosition;
            currentSpeed = movementVector.magnitude / Time.deltaTime;
            
            if (Vector3.Dot(movementVector, transform.forward) < 0) currentSpeed = -currentSpeed;
            previousPosition = transform.position;
        }
    }

    private float GetDynamicSteeringInput()
    {
        if (movementVR != null && movementVR.isActiveAndEnabled)
        {
            if (movementVR.joystickAction != null && movementVR.joystickAction.action != null)
            {
                return movementVR.joystickAction.action.ReadValue<Vector2>().x;
            }
        }
        return Input.GetAxis("Horizontal");
    }

    void ApplySteering()
    {
        if (Mathf.Abs(steeringInput) > 0.01f)
        {
            float targetAngle = steeringInput * maxSteeringAngle;
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, targetAngle, steeringSpeed * Time.deltaTime);
        }
        else
        {
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, 0f, steeringSpeed * Time.deltaTime);
        }

        Quaternion steeringRotation = Quaternion.AngleAxis(currentSteeringAngle, STEERING_AXIS);

        if (steeringType == SteeringType.FrontSteering)
        {
            if (joint4_FrontSteering != null)
                joint4_FrontSteering.localRotation = initialRotJoint4 * steeringRotation;

            if (joint5_RearSteering != null)
                joint5_RearSteering.localRotation = initialRotJoint5;
        }
        else
        {
            if (joint5_RearSteering != null)
                joint5_RearSteering.localRotation = initialRotJoint5 * steeringRotation;

            if (joint4_FrontSteering != null)
                joint4_FrontSteering.localRotation = initialRotJoint4;
        }
    }

    void ApplyWheelRotation()
    {
        // --- REALISTIC PHYSICS MATH ---
        // 1. Calculate circumference (Distance traveled in 1 full rotation)
        float rearCircumference = Mathf.PI * rearWheelDiameter;
        float frontCircumference = Mathf.PI * frontWheelDiameter;

        // 2. Calculate rotations per second based on exact m/s speed
        // If circumference is 0 to avoid DivideByZero error, we set it to 0.01f
        float rotationsPerSecondRear = currentSpeed / Mathf.Max(rearCircumference, 0.01f);
        float rotationsPerSecondFront = currentSpeed / Mathf.Max(frontCircumference, 0.01f);

        // 3. Convert to degrees per second
        float degreesPerSecondRear = rotationsPerSecondRear * 360f;
        float degreesPerSecondFront = rotationsPerSecondFront * 360f;

        if (invertRotation)
        {
            degreesPerSecondRear = -degreesPerSecondRear;
            degreesPerSecondFront = -degreesPerSecondFront;
        }

        float deltaRotationLeft = 1f;
        float deltaRotationRight = 1f;

        // Apply differential speed for turns
        if (differentialRotation && Mathf.Abs(steeringInput) > 0.01f)
        {
            float intensity = differentialIntensity;

            if (steeringType == SteeringType.RearSteering)
            {
                intensity *= 1.5f;
            }

            if (steeringInput > 0) // Turning Right
            {
                deltaRotationLeft = 1f + (Mathf.Abs(steeringInput) * intensity);
                deltaRotationRight = 1f - (Mathf.Abs(steeringInput) * intensity * 0.5f);
            }
            else // Turning Left
            {
                deltaRotationRight = 1f + (Mathf.Abs(steeringInput) * intensity);
                deltaRotationLeft = 1f - (Mathf.Abs(steeringInput) * intensity * 0.5f);
            }
        }

        // Add to current rotation (keeps memory of where the wheel stopped)
        rotationRearLeft += degreesPerSecondRear * deltaRotationLeft * Time.deltaTime;
        rotationRearRight += degreesPerSecondRear * deltaRotationRight * Time.deltaTime;
        rotationFrontLeft += degreesPerSecondFront * deltaRotationLeft * Time.deltaTime;
        rotationFrontRight += degreesPerSecondFront * deltaRotationRight * Time.deltaTime;

        // Apply visual rotation to joints
        if (joint8_RearLeftWheel != null)
            joint8_RearLeftWheel.localRotation = initialRotJoint8 * Quaternion.AngleAxis(rotationRearLeft, ROTATION_AXIS);

        if (joint9_RearRightWheel != null)
            joint9_RearRightWheel.localRotation = initialRotJoint9 * Quaternion.AngleAxis(rotationRearRight, ROTATION_AXIS);

        if (joint6_FrontLeftWheel != null)
            joint6_FrontLeftWheel.localRotation = initialRotJoint6 * Quaternion.AngleAxis(rotationFrontLeft, ROTATION_AXIS);

        if (joint7_FrontRightWheel != null)
            joint7_FrontRightWheel.localRotation = initialRotJoint7 * Quaternion.AngleAxis(rotationFrontRight, ROTATION_AXIS);
    }

    void ResetSteering()
    {
        currentSteeringAngle = 0f;

        if (joint4_FrontSteering != null)
            joint4_FrontSteering.localRotation = initialRotJoint4;

        if (joint5_RearSteering != null)
            joint5_RearSteering.localRotation = initialRotJoint5;
    }

    void FindJointsAutomatically()
    {
        if (joint4_FrontSteering == null) joint4_FrontSteering = transform.Find("joint4");
        if (joint5_RearSteering == null) joint5_RearSteering = transform.Find("joint5");
        if (joint6_FrontLeftWheel == null) joint6_FrontLeftWheel = transform.Find("joint6");
        if (joint7_FrontRightWheel == null) joint7_FrontRightWheel = transform.Find("joint7");
        if (joint8_RearLeftWheel == null) joint8_RearLeftWheel = transform.Find("joint8");
        if (joint9_RearRightWheel == null) joint9_RearRightWheel = transform.Find("joint9");
    }

    void StoreInitialRotations()
    {
        if (joint4_FrontSteering != null) initialRotJoint4 = joint4_FrontSteering.localRotation;
        if (joint5_RearSteering != null) initialRotJoint5 = joint5_RearSteering.localRotation;
        if (joint6_FrontLeftWheel != null) initialRotJoint6 = joint6_FrontLeftWheel.localRotation;
        if (joint7_FrontRightWheel != null) initialRotJoint7 = joint7_FrontRightWheel.localRotation;
        if (joint8_RearLeftWheel != null) initialRotJoint8 = joint8_RearLeftWheel.localRotation;
        if (joint9_RearRightWheel != null) initialRotJoint9 = joint9_RearRightWheel.localRotation;
    }

    void VerifyConfiguration()
    {
        if (joint4_FrontSteering == null || joint5_RearSteering == null || 
            joint6_FrontLeftWheel == null || joint7_FrontRightWheel == null || 
            joint8_RearLeftWheel == null || joint9_RearRightWheel == null)
        {
            Debug.LogWarning("WheelController: One or more joints are missing! Visual rotation might not work.");
        }
    }

    // ===== PUBLIC METHODS =====

    public void StopWheels()
    {
        // FIXED: Only zero out speed and steering logic, do NOT reset wheel rotation angles!
        currentSteeringAngle = 0f;
        currentSpeed = 0f;
        steeringInput = 0f;

        ResetSteering();
        
        // As rodas rotativas (joints 6, 7, 8, 9) mantêm a sua última rotação intacta!
    }

    public SteeringType GetSteeringType()
    {
        return steeringType;
    }

    public void SetSteeringType(SteeringType newType)
    {
        steeringType = newType;
        ResetSteering();
        ConfigureMovementScript();
    }
}