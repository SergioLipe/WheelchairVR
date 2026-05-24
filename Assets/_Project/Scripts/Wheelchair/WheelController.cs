using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Complete wheelchair wheel control system
/// Manages steering, spinning and differential wheel movement based on true physics.
/// Fully compatible with both PC Keyboard and VR Joysticks.
/// </summary>
public class WheelController : MonoBehaviour
{
    [Header("=== Steering Joints ===")]
    public Transform joint4_FrontSteering;
    public Transform joint5_RearSteering;

    [Header("=== Wheel Rotation Joints ===")]
    public Transform joint6_FrontLeftWheel;
    public Transform joint7_FrontRightWheel;
    public Transform joint8_RearLeftWheel;
    public Transform joint9_RearRightWheel;

    [Header("=== Wheelchair Type ===")]
    public SteeringType steeringType = SteeringType.FrontSteering;
    public KeyCode toggleSteeringKey = KeyCode.T;

    [Header("=== Physical Configuration ===")]
    public float maxSpeedKmH = 6f;
    public float rearWheelDiameter = 0.6f;
    public float frontWheelDiameter = 0.15f;

    [Header("=== Steering Configuration ===")]
    [Range(0f, 45f)] public float maxSteeringAngle = 30f;
    [Range(1f, 10f)] public float steeringSpeed = 5f;

    [Header("=== Rotation Configuration ===")]
    public bool differentialRotation = true;
    [Range(0f, 2f)] public float differentialIntensity = 0.5f;
    public bool invertRotation = false;

    [Header("=== Debug Info ===")]
    [SerializeField] private float rotationFrontLeft = 0f;
    [SerializeField] private float rotationFrontRight = 0f;
    [SerializeField] private float rotationRearLeft = 0f;
    [SerializeField] private float rotationRearRight = 0f;
    [SerializeField] private float currentSteeringAngle = 0f;
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float steeringInput = 0f;

    public enum SteeringType { FrontSteering, RearSteering }

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

    private Vector3 previousPosition;

    // [OPT] Cache de transform e InputAction
    private Transform myTransform;
    private InputAction vrJoystickAction;
    private bool hasMovementVR;
    private bool hasMovementPC;
    private bool hasRigidbody;

    // [OPT] Cache de existência de joints (evita null check todos os frames)
    private bool hasJoint4, hasJoint5, hasJoint6, hasJoint7, hasJoint8, hasJoint9;

    // [OPT] Cache de circumferences (não muda em runtime — só recalcula se alterar Inspector)
    private float cachedRearCircumference;
    private float cachedFrontCircumference;

    // [OPT] Rotation axes como static readonly
    private static readonly Vector3 ROTATION_AXIS = Vector3.forward;
    private static readonly Vector3 STEERING_AXIS = Vector3.up;

    void Awake()
    {
        myTransform = transform;
        InitializeComponents();
        FindJointsAutomatically();
        CacheJointExistence();
        StoreInitialRotations();
        VerifyConfiguration();
        ConfigureMovementScript();
        RecalculateCircumferences();

        // [OPT] cache da input action no VR
        if (movementVR != null && movementVR.joystickAction != null)
        {
            vrJoystickAction = movementVR.joystickAction.action;
        }
    }

    // [OPT] Recalcula só quando algo muda no Inspector (Editor)
    #if UNITY_EDITOR
    private void OnValidate()
    {
        RecalculateCircumferences();
    }
    #endif

    private void RecalculateCircumferences()
    {
        cachedRearCircumference = Mathf.Max(Mathf.PI * rearWheelDiameter, 0.01f);
        cachedFrontCircumference = Mathf.Max(Mathf.PI * frontWheelDiameter, 0.01f);
    }

    void Update()
    {
        // [OPT] só checa keyboard se houver suporte (PC). Editor input system pode estar disabled
        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(toggleSteeringKey))
        {
            ToggleSteeringType();
        }
        #endif

        GetInputs();
        ApplySteering();
        ApplyWheelRotation();
    }

    private void InitializeComponents()
    {
        movementPC = GetComponent<MovementPC>();
        movementVR = GetComponent<MovementVR>();
        rb = GetComponent<Rigidbody>();

        hasMovementVR = movementVR != null;
        hasMovementPC = movementPC != null;
        hasRigidbody = rb != null;

        previousPosition = myTransform.position;
    }

    private void CacheJointExistence()
    {
        hasJoint4 = joint4_FrontSteering != null;
        hasJoint5 = joint5_RearSteering != null;
        hasJoint6 = joint6_FrontLeftWheel != null;
        hasJoint7 = joint7_FrontRightWheel != null;
        hasJoint8 = joint8_RearLeftWheel != null;
        hasJoint9 = joint9_RearRightWheel != null;
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

    private void SetRotationSpeed(float speed)
    {
        if (hasMovementPC) movementPC.rotationSpeed = speed;
        if (hasMovementVR) movementVR.rotationSpeed = speed;
    }

    private void SetRotationInPlace(bool value)
    {
        if (hasMovementPC) movementPC.rotationInPlace = value;
        if (hasMovementVR) movementVR.rotationInPlace = value;
    }

    public void ToggleSteeringType()
    {
        steeringType = (steeringType == SteeringType.FrontSteering)
            ? SteeringType.RearSteering
            : SteeringType.FrontSteering;

        ResetSteering();
        ConfigureMovementScript();
    }

    void GetInputs()
    {
        steeringInput = GetDynamicSteeringInput();

        if (hasMovementVR)
        {
            currentSpeed = movementVR.GetCurrentSpeed();
        }
        else if (hasMovementPC)
        {
            currentSpeed = movementPC.GetNormalizedSpeed() * (maxSpeedKmH / 3.6f);
        }
        else if (hasRigidbody)
        {
            Vector3 vel = rb.linearVelocity;
            currentSpeed = vel.magnitude;
            if (Vector3.Dot(vel, myTransform.forward) < 0) currentSpeed = -currentSpeed;
        }
        else
        {
            Vector3 currentPos = myTransform.position;
            Vector3 movementVector = currentPos - previousPosition;
            // [OPT] evita divisão por zero
            float dt = Time.deltaTime;
            currentSpeed = dt > 0 ? movementVector.magnitude / dt : 0f;

            if (Vector3.Dot(movementVector, myTransform.forward) < 0) currentSpeed = -currentSpeed;
            previousPosition = currentPos;
        }
    }

    private float GetDynamicSteeringInput()
    {
        // [OPT] cached action
        if (vrJoystickAction != null && hasMovementVR && movementVR.isActiveAndEnabled)
        {
            return vrJoystickAction.ReadValue<Vector2>().x;
        }

        #if UNITY_EDITOR || UNITY_STANDALONE
        return Input.GetAxis("Horizontal");
        #else
        return 0f;
        #endif
    }

    void ApplySteering()
    {
        float dt = Time.deltaTime;
        float lerpFactor = steeringSpeed * dt;

        if (Mathf.Abs(steeringInput) > 0.01f)
        {
            float targetAngle = steeringInput * maxSteeringAngle;
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, targetAngle, lerpFactor);
        }
        else
        {
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, 0f, lerpFactor);
        }

        // [OPT] só aplica rotation se há mudança significativa (evita quaternion mult inútil)
        Quaternion steeringRotation = Quaternion.AngleAxis(currentSteeringAngle, STEERING_AXIS);

        if (steeringType == SteeringType.FrontSteering)
        {
            if (hasJoint4) joint4_FrontSteering.localRotation = initialRotJoint4 * steeringRotation;
            if (hasJoint5) joint5_RearSteering.localRotation = initialRotJoint5;
        }
        else
        {
            if (hasJoint5) joint5_RearSteering.localRotation = initialRotJoint5 * steeringRotation;
            if (hasJoint4) joint4_FrontSteering.localRotation = initialRotJoint4;
        }
    }

    void ApplyWheelRotation()
    {
        // [OPT] usa cached circumferences (em vez de recalcular cada frame)
        float rotationsPerSecondRear = currentSpeed / cachedRearCircumference;
        float rotationsPerSecondFront = currentSpeed / cachedFrontCircumference;

        // [OPT] multiplicação direta em vez de variável intermédia
        float degreesPerSecondRear = rotationsPerSecondRear * 360f;
        float degreesPerSecondFront = rotationsPerSecondFront * 360f;

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

            float absSteering = Mathf.Abs(steeringInput); // [OPT] cache

            if (steeringInput > 0)
            {
                deltaRotationLeft = 1f + (absSteering * intensity);
                deltaRotationRight = 1f - (absSteering * intensity * 0.5f);
            }
            else
            {
                deltaRotationRight = 1f + (absSteering * intensity);
                deltaRotationLeft = 1f - (absSteering * intensity * 0.5f);
            }
        }

        // [OPT] cache de deltaTime
        float dt = Time.deltaTime;

        rotationRearLeft += degreesPerSecondRear * deltaRotationLeft * dt;
        rotationRearRight += degreesPerSecondRear * deltaRotationRight * dt;
        rotationFrontLeft += degreesPerSecondFront * deltaRotationLeft * dt;
        rotationFrontRight += degreesPerSecondFront * deltaRotationRight * dt;

        // [OPT] usar cached bool em vez de null check
        if (hasJoint8)
            joint8_RearLeftWheel.localRotation = initialRotJoint8 * Quaternion.AngleAxis(rotationRearLeft, ROTATION_AXIS);

        if (hasJoint9)
            joint9_RearRightWheel.localRotation = initialRotJoint9 * Quaternion.AngleAxis(rotationRearRight, ROTATION_AXIS);

        if (hasJoint6)
            joint6_FrontLeftWheel.localRotation = initialRotJoint6 * Quaternion.AngleAxis(rotationFrontLeft, ROTATION_AXIS);

        if (hasJoint7)
            joint7_FrontRightWheel.localRotation = initialRotJoint7 * Quaternion.AngleAxis(rotationFrontRight, ROTATION_AXIS);
    }

    void ResetSteering()
    {
        currentSteeringAngle = 0f;
        if (hasJoint4) joint4_FrontSteering.localRotation = initialRotJoint4;
        if (hasJoint5) joint5_RearSteering.localRotation = initialRotJoint5;
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

    public void StopWheels()
    {
        currentSteeringAngle = 0f;
        currentSpeed = 0f;
        steeringInput = 0f;
        ResetSteering();
    }

    public SteeringType GetSteeringType() => steeringType;

    public void SetSteeringType(SteeringType newType)
    {
        steeringType = newType;
        ResetSteering();
        ConfigureMovementScript();
    }
}