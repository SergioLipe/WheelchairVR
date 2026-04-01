using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Complete wheelchair wheel control system
/// Manages steering, spinning and differential wheel movement
/// Supports two modes: Front Steering (standard) and Rear Steering (more maneuverable)
/// Fully compatible with both PC Keyboard and VR Joysticks.
/// NOTE: Sound effects are handled by the Movement scripts, NOT here
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
    [Tooltip("Maximum wheelchair speed in km/h")]
    public float maxSpeedKmH = 6f;

    [Tooltip("Rear wheel diameter in meters")]
    public float rearWheelDiameter = 0.6f;

    [Tooltip("Front wheel diameter in meters")]
    public float frontWheelDiameter = 0.15f;

    [Tooltip("Rotation speed multiplier")]
    public float speedMultiplier = 5f;

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

    [Tooltip("Invert rotation direction")]
    public bool invertRotation = false;

    [Header("=== Debug Info ===")]
    [SerializeField] private float rotationFrontLeft = 0f;
    [SerializeField] private float rotationFrontRight = 0f;
    [SerializeField] private float rotationRearLeft = 0f;
    [SerializeField] private float rotationRearRight = 0f;
    [SerializeField] private float currentSteeringAngle = 0f;
    [SerializeField] private float currentSpeed = 0f;
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

    // For manual speed calculation
    private Vector3 previousPosition;

    // Rotation axes
    private readonly Vector3 ROTATION_AXIS = Vector3.forward;
    private readonly Vector3 STEERING_AXIS = Vector3.up;

    // ===== HELPER METHODS =====

    private float GetMovementNormalizedSpeed()
    {
        if (movementPC != null) return movementPC.GetNormalizedSpeed();
        if (movementVR != null) return movementVR.GetNormalizedSpeed();
        return 0f;
    }

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

    void Start()
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

    /// <summary>
    /// Toggles between front and rear steering.
    /// Made PUBLIC so MovementVR and MovementPC can trigger it.
    /// </summary>
    public void ToggleSteeringType()
    {
        if (steeringType == SteeringType.FrontSteering)
        {
            steeringType = SteeringType.RearSteering;
        }
        else
        {
            steeringType = SteeringType.FrontSteering;
        }

        ResetSteering();
        ConfigureMovementScript();
    }

    void GetInputs()
    {
        // Fetch steering input dynamically from VR Joystick or PC Keyboard
        steeringInput = GetDynamicSteeringInput();

        if (HasMovementScript())
        {
            currentSpeed = GetMovementNormalizedSpeed();
        }
        else if (rb != null)
        {
            currentSpeed = rb.linearVelocity.magnitude / (maxSpeedKmH / 3.6f);
            currentSpeed = Mathf.Clamp(currentSpeed, -1f, 1f);
        }
        else
        {
            float distance = Vector3.Distance(transform.position, previousPosition);
            float calculatedSpeed = distance / Time.deltaTime;
            currentSpeed = calculatedSpeed / (maxSpeedKmH / 3.6f);
            currentSpeed = Mathf.Clamp(currentSpeed, -1f, 1f);
            previousPosition = transform.position;
        }
    }

    /// <summary>
    /// Detects if the player is using VR or PC and grabs the correct horizontal input.
    /// </summary>
    private float GetDynamicSteeringInput()
    {
        // If VR script is active, read the thumbstick X axis directly
        if (movementVR != null && movementVR.isActiveAndEnabled)
        {
            if (movementVR.joystickAction != null && movementVR.joystickAction.action != null)
            {
                return movementVR.joystickAction.action.ReadValue<Vector2>().x;
            }
        }
        
        // PC Fallback
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
        float rearCircumference = Mathf.PI * rearWheelDiameter;
        float rotationsPerMeterRear = 1f / rearCircumference;
        float speedMetersPerSecond = currentSpeed * (maxSpeedKmH / 3.6f);
        float rotationsPerSecondRear = speedMetersPerSecond * rotationsPerMeterRear;
        float degreesPerSecondRear = rotationsPerSecondRear * 360f * speedMultiplier;

        float frontCircumference = Mathf.PI * frontWheelDiameter;
        float rotationsPerMeterFront = 1f / frontCircumference;
        float rotationsPerSecondFront = speedMetersPerSecond * rotationsPerMeterFront;
        float degreesPerSecondFront = rotationsPerSecondFront * 360f * speedMultiplier;

        if (invertRotation)
        {
            degreesPerSecondRear = -degreesPerSecondRear;
            degreesPerSecondFront = -degreesPerSecondFront;
        }

        float deltaRotationLeft = 1f;
        float deltaRotationRight = 1f;

        if (differentialRotation && Mathf.Abs(steeringInput) > 0.01f)
        {
            float intensity = differentialIntensity;

            if (steeringType == SteeringType.RearSteering)
            {
                intensity *= 1.5f;
            }

            if (steeringInput > 0)
            {
                deltaRotationLeft = 1f + (Mathf.Abs(steeringInput) * intensity);
                deltaRotationRight = 1f - (Mathf.Abs(steeringInput) * intensity * 0.5f);
            }
            else
            {
                deltaRotationRight = 1f + (Mathf.Abs(steeringInput) * intensity);
                deltaRotationLeft = 1f - (Mathf.Abs(steeringInput) * intensity * 0.5f);
            }
        }

        rotationRearLeft += degreesPerSecondRear * deltaRotationLeft * Time.deltaTime;
        rotationRearRight += degreesPerSecondRear * deltaRotationRight * Time.deltaTime;
        rotationFrontLeft += degreesPerSecondFront * deltaRotationLeft * Time.deltaTime;
        rotationFrontRight += degreesPerSecondFront * deltaRotationRight * Time.deltaTime;

        if (joint8_RearLeftWheel != null)
        {
            Quaternion rotation = Quaternion.AngleAxis(rotationRearLeft, ROTATION_AXIS);
            joint8_RearLeftWheel.localRotation = initialRotJoint8 * rotation;
        }

        if (joint9_RearRightWheel != null)
        {
            Quaternion rotation = Quaternion.AngleAxis(rotationRearRight, ROTATION_AXIS);
            joint9_RearRightWheel.localRotation = initialRotJoint9 * rotation;
        }

        if (joint6_FrontLeftWheel != null)
        {
            Quaternion rotation = Quaternion.AngleAxis(rotationFrontLeft, ROTATION_AXIS);
            joint6_FrontLeftWheel.localRotation = initialRotJoint6 * rotation;
        }

        if (joint7_FrontRightWheel != null)
        {
            Quaternion rotation = Quaternion.AngleAxis(rotationFrontRight, ROTATION_AXIS);
            joint7_FrontRightWheel.localRotation = initialRotJoint7 * rotation;
        }
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
        if (joint4_FrontSteering == null)
            joint4_FrontSteering = transform.Find("joint4");
        if (joint5_RearSteering == null)
            joint5_RearSteering = transform.Find("joint5");
        if (joint6_FrontLeftWheel == null)
            joint6_FrontLeftWheel = transform.Find("joint6");
        if (joint7_FrontRightWheel == null)
            joint7_FrontRightWheel = transform.Find("joint7");
        if (joint8_RearLeftWheel == null)
            joint8_RearLeftWheel = transform.Find("joint8");
        if (joint9_RearRightWheel == null)
            joint9_RearRightWheel = transform.Find("joint9");
    }

    void StoreInitialRotations()
    {
        if (joint4_FrontSteering != null)
            initialRotJoint4 = joint4_FrontSteering.localRotation;
        if (joint5_RearSteering != null)
            initialRotJoint5 = joint5_RearSteering.localRotation;
        if (joint6_FrontLeftWheel != null)
            initialRotJoint6 = joint6_FrontLeftWheel.localRotation;
        if (joint7_FrontRightWheel != null)
            initialRotJoint7 = joint7_FrontRightWheel.localRotation;
        if (joint8_RearLeftWheel != null)
            initialRotJoint8 = joint8_RearLeftWheel.localRotation;
        if (joint9_RearRightWheel != null)
            initialRotJoint9 = joint9_RearRightWheel.localRotation;
    }

    void VerifyConfiguration()
    {
        if (joint4_FrontSteering == null) return;
        if (joint5_RearSteering == null) return;
        if (joint6_FrontLeftWheel == null) return;
        if (joint7_FrontRightWheel == null) return;
        if (joint8_RearLeftWheel == null) return;
        if (joint9_RearRightWheel == null) return;
    }

    // ===== PUBLIC METHODS =====

    public void StopWheels()
    {
        rotationFrontLeft = 0f;
        rotationFrontRight = 0f;
        rotationRearLeft = 0f;
        rotationRearRight = 0f;
        currentSteeringAngle = 0f;
        currentSpeed = 0f;
        steeringInput = 0f;

        ResetSteering();

        if (joint6_FrontLeftWheel != null)
            joint6_FrontLeftWheel.localRotation = initialRotJoint6;
        if (joint7_FrontRightWheel != null)
            joint7_FrontRightWheel.localRotation = initialRotJoint7;
        if (joint8_RearLeftWheel != null)
            joint8_RearLeftWheel.localRotation = initialRotJoint8;
        if (joint9_RearRightWheel != null)
            joint9_RearRightWheel.localRotation = initialRotJoint9;
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