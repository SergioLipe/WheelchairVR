using UnityEngine;

/// <summary>
/// VR-Optimized collision detection and management system.
/// Uses surface normals (Dot Product) for physical impacts and a 
/// proactive BoxCast sensor for the front footrests and rear wheels.
/// </summary>
public class CollisionSystemVR : MonoBehaviour
{
    [Header("=== Initialization Settings ===")]
    [Tooltip("Grace period (seconds) after script turns on to ignore physics jitters.")]
    public float startupGracePeriod = 0.5f;
    private float enableTime = 0f;

    [Header("=== Collision State (Debug) ===")]
    [SerializeField] private bool inCollision = false;
    [SerializeField] private string collidedObject = "";
    [SerializeField] private bool frontBlocked = false;
    [SerializeField] private bool backBlocked = false;
    [SerializeField] private bool wallSliding = false;

    // Stats for collision tracking
    public int TotalCollisions { get; private set; } = 0;
    public int TotalSlides { get; private set; } = 0;

    private bool wasInCollisionState = false;
    private bool wasSlidingState = false;

    private float lastSlideCountTime = 0f;
    private float lastCollisionCountTime = 0f;

    [Header("=== Debug Settings ===")]
    [Tooltip("Enable to print exactly WHAT you hit to the Unity Console")]
    public bool enableCollisionDebug = true;

    [Header("=== Front Sensor ===")]
    [Tooltip("Enable the proactive front sensor for footrests")]
    public bool useFrontSensor = true;
    public float frontSensorLength = 0.2f;
    public Vector3 frontSensorBoxSize = new Vector3(0.38f, 0.2f, 0.1f);
    public Vector3 frontSensorOffset = new Vector3(0f, 0.3f, 0.4f);

    [Header("=== Back Sensor ===")]
    [Tooltip("Enable the proactive back sensor for reversing")]
    public bool useBackSensor = true;
    public float backSensorLength = 0.2f;
    public Vector3 backSensorBoxSize = new Vector3(0.8f, 0.2f, 0.1f);
    public Vector3 backSensorOffset = new Vector3(0f, 0.3f, -0.45f);

    [Header("=== Back Wheel Visual Sensors ===")]
    [Tooltip("Sensors that flash and trigger slide audio when rear wheels scrape a wall.")]
    public bool useBackWheelVisualSensors = true;
    [Tooltip("How far the sensor extends LATERALLY from the wheel")]
    public float backWheelSensorLength = 0.2f;
    [Tooltip("Size of the detection box. Y = height, Z = length along wheelchair forward")]
    public Vector3 backWheelSensorBoxSize = new Vector3(0.15f, 0.2f, 0.3f);
    public Vector3 backLeftWheelOffset = new Vector3(-0.35f, 0.3f, -0.45f);
    public Vector3 backRightWheelOffset = new Vector3(0.35f, 0.3f, -0.45f);

    [Header("=== General Sensor Settings ===")]
    [Tooltip("Layers the sensors should detect as obstacles")]
    public LayerMask obstacleLayerMask = ~0;

    [Header("=== Detection Settings ===")]
    [SerializeField] private float minCollisionHeight = 0.08f;
    [SerializeField] private float maxGroundAngle = 45f;
    [SerializeField] private string[] ignoreTags = { "Ground", "Floor", "Terrain" };
    [SerializeField] private LayerMask ignoreLayerMask;

    // External components
    private CharacterController controller;
    private Transform wheelchairTransform;
    private CollisionFlashEffectVR flashEffect;

    // Collision variables
    private Vector3 collisionNormal = Vector3.zero;
    private Vector3 collisionPoint = Vector3.zero;
    private float collisionTime = 0f;
    private float lastValidCollisionTime = 0f;

    private int collisionCount = 0;
    private float multiCollisionResetTime = 0f;

    private float frontBlockTimer = 0f;
    private float backBlockTimer = 0f;
    private const float blockingDuration = 0.15f;

    private Vector3 slideDirection = Vector3.zero;
    private float slideTimer = 0f;

    private bool wasFrontSensorBlockedLastFrame = false;
    private bool wasBackSensorBlockedLastFrame = false;
    private bool wasLeftWheelBlockedLastFrame = false;
    private bool wasRightWheelBlockedLastFrame = false;

    private void OnEnable()
    {
        enableTime = Time.time;
        ForceResetCollisions();
        wasFrontSensorBlockedLastFrame = false;
        wasBackSensorBlockedLastFrame = false;
        wasLeftWheelBlockedLastFrame = false;
        wasRightWheelBlockedLastFrame = false;
    }

    public void Initialize(CharacterController characterController, Transform transform)
    {
        this.controller = characterController;
        this.wheelchairTransform = transform;

        flashEffect = GetComponent<CollisionFlashEffectVR>();
        if (flashEffect == null)
        {
            Debug.LogWarning("CollisionFlashEffectVR missing. Adding one automatically.");
            flashEffect = gameObject.AddComponent<CollisionFlashEffectVR>();
        }
    }

    public void Update()
    {
        // Ignora tremores da física nos primeiros instantes após o script ligar
        if (Time.time - enableTime < startupGracePeriod) return;

        UpdateBlockingTimers();
        UpdateCollisionState();
        UpdateSlideTimer();
        HandleMultipleCollisions();

        if (useFrontSensor) CheckFrontSensor();
        if (useBackSensor) CheckBackSensor();
        if (useBackWheelVisualSensors) CheckBackWheelVisualSensors();

        if (inCollision && !wasInCollisionState && !wallSliding)
        {
            if (Time.time - lastCollisionCountTime > 1.0f)
            {
                TotalCollisions++;
                lastCollisionCountTime = Time.time;
            }
        }
        wasInCollisionState = inCollision;

        if (wallSliding && !wasSlidingState)
        {
            if (Time.time - lastSlideCountTime > 1.0f)
            {
                TotalSlides++;
                lastSlideCountTime = Time.time;
            }
        }
        wasSlidingState = wallSliding;
    }

    private void CheckFrontSensor()
    {
        if (wheelchairTransform == null) return;

        Vector3 scaledOffset = Vector3.Scale(frontSensorOffset, wheelchairTransform.lossyScale);
        float scaledLength = frontSensorLength * wheelchairTransform.lossyScale.z;
        Vector3 scaledBoxSize = Vector3.Scale(frontSensorBoxSize, wheelchairTransform.lossyScale);

        Vector3 startPos = wheelchairTransform.position +
                           wheelchairTransform.forward * scaledOffset.z +
                           wheelchairTransform.up * scaledOffset.y +
                           wheelchairTransform.right * scaledOffset.x;

        Vector3 halfExtents = scaledBoxSize / 2f;
        bool hitObstacle = false;

        RaycastHit[] hits = Physics.BoxCastAll(startPos, halfExtents, wheelchairTransform.forward, wheelchairTransform.rotation, scaledLength, obstacleLayerMask);

        foreach (RaycastHit hit in hits)
        {
            if (ShouldIgnoreSensorHit(hit)) continue;

            hitObstacle = true;
            collidedObject = hit.collider.gameObject.name;
            collisionPoint = hit.point;

            if (enableCollisionDebug && !wasFrontSensorBlockedLastFrame)
            {
                Debug.LogWarning($"<color=cyan>[FRONT SENSOR HIT]</color> Stopped by: <b>{collidedObject}</b>");
                Debug.DrawRay(hit.point, Vector3.up * 2f, Color.cyan, 3f);
            }
            break;
        }

        if (hitObstacle)
        {
            frontBlocked = true;
            frontBlockTimer = 0.15f;

            if (!wasFrontSensorBlockedLastFrame)
            {
                float dummySpeed = 0f;
                ProcessFrontCollision(ref dummySpeed);
                inCollision = true;
                collisionTime = Time.time;
            }
            wasFrontSensorBlockedLastFrame = true;
        }
        else
        {
            wasFrontSensorBlockedLastFrame = false;
        }
    }

    private void CheckBackSensor()
    {
        if (wheelchairTransform == null) return;

        Vector3 scaledOffset = Vector3.Scale(backSensorOffset, wheelchairTransform.lossyScale);
        float scaledLength = backSensorLength * wheelchairTransform.lossyScale.z;
        Vector3 scaledBoxSize = Vector3.Scale(backSensorBoxSize, wheelchairTransform.lossyScale);

        Vector3 startPos = wheelchairTransform.position +
                           wheelchairTransform.forward * scaledOffset.z +
                           wheelchairTransform.up * scaledOffset.y +
                           wheelchairTransform.right * scaledOffset.x;

        Vector3 halfExtents = scaledBoxSize / 2f;
        bool hitObstacle = false;

        RaycastHit[] hits = Physics.BoxCastAll(startPos, halfExtents, -wheelchairTransform.forward, wheelchairTransform.rotation, scaledLength, obstacleLayerMask);

        foreach (RaycastHit hit in hits)
        {
            if (ShouldIgnoreSensorHit(hit)) continue;

            hitObstacle = true;
            collidedObject = hit.collider.gameObject.name;
            collisionPoint = hit.point;

            if (enableCollisionDebug && !wasBackSensorBlockedLastFrame)
            {
                Debug.LogWarning($"<color=magenta>[BACK SENSOR HIT]</color> Stopped by: <b>{collidedObject}</b>");
                Debug.DrawRay(hit.point, Vector3.up * 2f, Color.magenta, 3f);
            }
            break;
        }

        if (hitObstacle)
        {
            backBlocked = true;
            backBlockTimer = 0.15f;

            if (!wasBackSensorBlockedLastFrame)
            {
                float dummySpeed = 0f;
                ProcessBackCollision(ref dummySpeed);
                inCollision = true;
                collisionTime = Time.time;
            }
            wasBackSensorBlockedLastFrame = true;
        }
        else
        {
            wasBackSensorBlockedLastFrame = false;
        }
    }

    private void CheckBackWheelVisualSensors()
    {
        if (wheelchairTransform == null) return;

        bool leftHit = ProcessWheelVisualSensor(backLeftWheelOffset, "Left", wasLeftWheelBlockedLastFrame);
        bool rightHit = ProcessWheelVisualSensor(backRightWheelOffset, "Right", wasRightWheelBlockedLastFrame);

        wasLeftWheelBlockedLastFrame = leftHit;
        wasRightWheelBlockedLastFrame = rightHit;
    }

    private bool ProcessWheelVisualSensor(Vector3 offset, string sideName, bool wasBlockedLastFrame)
    {
        Vector3 scaledOffset = Vector3.Scale(offset, wheelchairTransform.lossyScale);
        float scaledLength = backWheelSensorLength * wheelchairTransform.lossyScale.x;
        Vector3 scaledBoxSize = Vector3.Scale(backWheelSensorBoxSize, wheelchairTransform.lossyScale);

        Vector3 startPos = wheelchairTransform.position +
                           wheelchairTransform.forward * scaledOffset.z +
                           wheelchairTransform.up * scaledOffset.y +
                           wheelchairTransform.right * scaledOffset.x;

        Vector3 sideDir = (sideName == "Left") ? -wheelchairTransform.right : wheelchairTransform.right;
        Vector3 boxCenter = startPos + (sideDir * (scaledLength / 2f));
        Vector3 halfExtents = scaledBoxSize / 2f;

        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, wheelchairTransform.rotation, obstacleLayerMask);

        foreach (Collider hitCollider in hits)
        {
            if (hitCollider.transform.root == wheelchairTransform.root) continue;
            if (hitCollider.isTrigger) continue;
            if (hitCollider.GetComponent<Terrain>() != null) continue;

            bool ignore = false;
            foreach (string tag in ignoreTags)
            {
                if (hitCollider.tag == tag) { ignore = true; break; }
            }
            if (ignore) continue;

            if (ignoreLayerMask != 0 && ((ignoreLayerMask.value & (1 << hitCollider.gameObject.layer)) != 0)) continue;

            if (flashEffect != null && !wasBlockedLastFrame)
            {
                if (sideName == "Left") flashEffect.LeftSideFlash();
                else flashEffect.RightSideFlash();
            }

            if (enableCollisionDebug && !wasBlockedLastFrame)
            {
                Debug.Log($"<color=yellow>[{sideName.ToUpper()} WHEEL SCRAPE]</color> <b>{hitCollider.gameObject.name}</b>");
            }

            // Ativa o slide se a cadeira estiver efetivamente em movimento!
            if (Mathf.Abs(controller.velocity.magnitude) > 0.1f)
            {
                float movementSign = Vector3.Dot(controller.velocity, wheelchairTransform.forward) >= 0 ? 1f : -1f;
                slideDirection = wheelchairTransform.forward * movementSign;
                
                wallSliding = true;
                slideTimer = 0.25f;
                inCollision = true;
                collisionTime = Time.time;
            }

            return true;
        }

        return false;
    }

    private bool ShouldIgnoreSensorHit(RaycastHit hit)
    {
        if (hit.collider.transform.root == wheelchairTransform.root) return true;

        float collisionHeight = hit.point.y - wheelchairTransform.position.y;
        if (collisionHeight < minCollisionHeight) return true;

        float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
        if (angleWithUp < maxGroundAngle) return true;

        foreach (string tag in ignoreTags)
        {
            if (hit.collider.tag == tag) return true;
        }

        // CORREÇÃO: hit.collider.gameObject.layer está perfeito
        if (ignoreLayerMask != 0 && ((ignoreLayerMask.value & (1 << hit.collider.gameObject.layer)) != 0)) return true;
        if (hit.collider.GetComponent<Terrain>() != null) return true;
        if (hit.collider.isTrigger) return true;

        return false;
    }

    private void UpdateBlockingTimers()
    {
        if (frontBlockTimer > 0)
        {
            frontBlockTimer -= Time.deltaTime;
            if (frontBlockTimer <= 0 && !wasFrontSensorBlockedLastFrame) frontBlocked = false;
        }

        if (backBlockTimer > 0)
        {
            backBlockTimer -= Time.deltaTime;
            if (backBlockTimer <= 0 && !wasBackSensorBlockedLastFrame) backBlocked = false;
        }
    }

    private void UpdateCollisionState()
    {
        if (inCollision && Time.time - collisionTime > 0.3f)
        {
            ResetCollisionState();
        }
    }

    private void UpdateSlideTimer()
    {
        if (slideTimer > 0)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0)
            {
                wallSliding = false;
                slideDirection = Vector3.zero;
            }
        }
    }

    private void HandleMultipleCollisions()
    {
        if (collisionCount > 1)
        {
            multiCollisionResetTime += Time.deltaTime;
            if (multiCollisionResetTime > 0.5f) ForceResetCollisions();
        }
        else
        {
            multiCollisionResetTime = 0f;
        }

        if (collisionCount > 0 && Time.time - collisionTime > 0.1f)
        {
            collisionCount = Mathf.Max(0, collisionCount - 1);
        }
    }

    public void ProcessCollision(ControllerColliderHit hit, float currentSpeed, ref float currentSpeedRef)
    {
        // O Grace Period: Ignora o embate físico no milissegundo em que o script acorda
        if (!isActiveAndEnabled || Time.time - enableTime < startupGracePeriod) return;

        if (ShouldIgnoreCollision(hit)) return;

        float timeSinceLastCollision = Time.time - lastValidCollisionTime;
        if (timeSinceLastCollision < 0.05f) return;

        if (enableCollisionDebug)
        {
            Debug.LogWarning($"<color=orange>[CAPSULE HIT]</color> Wheelchair touched: <b>{hit.gameObject.name}</b>");
            Debug.DrawRay(hit.point, hit.normal * 1.5f, Color.red, 2f);
        }

        Vector3 impactDirection = -hit.normal;
        impactDirection.y = 0;

        if (impactDirection.sqrMagnitude < 0.001f) return;
        impactDirection.Normalize();

        float forwardDot = Vector3.Dot(wheelchairTransform.forward, impactDirection);
        bool collisionProcessed = false;

        if (forwardDot > 0.5f && !frontBlocked)
        {
            ProcessFrontCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        else if (forwardDot < -0.5f && !backBlocked)
        {
            ProcessBackCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        else if (Mathf.Abs(forwardDot) <= 0.5f)
        {
            ProcessSideCollision(hit, impactDirection);
            collisionProcessed = true;
        }

        if (collisionProcessed)
        {
            inCollision = true;
            collidedObject = hit.gameObject.name;
            collisionPoint = hit.point;
            collisionTime = Time.time;
            lastValidCollisionTime = Time.time;
            collisionCount++;
            collisionCount = Mathf.Min(collisionCount, 3);
        }
    }

    private bool ShouldIgnoreCollision(ControllerColliderHit hit)
    {
        float collisionHeight = hit.point.y - wheelchairTransform.position.y;
        if (collisionHeight < minCollisionHeight) return true;

        float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
        if (angleWithUp < maxGroundAngle) return true;

        if (ignoreTags != null && ignoreTags.Length > 0 && !string.IsNullOrEmpty(hit.gameObject.tag))
        {
            foreach (string tag in ignoreTags)
            {
                if (hit.gameObject.tag == tag) return true;
            }
        }

        if (ignoreLayerMask != 0 && ((ignoreLayerMask.value & (1 << hit.gameObject.layer)) != 0)) return true;
        if (hit.gameObject.GetComponent<Terrain>() != null) return true;
        if (hit.collider.isTrigger) return true;
        if (hit.moveDirection.y < -0.3f) return true;
        if (hit.normal.y > 0.7f) return true;

        return false;
    }

    private void ProcessFrontCollision(ref float currentSpeedRef)
    {
        frontBlocked = true;
        frontBlockTimer = blockingDuration;

        if (currentSpeedRef > 0) currentSpeedRef = 0;
        if (flashEffect != null) flashEffect.FrontFlash();
    }

    private void ProcessBackCollision(ref float currentSpeedRef)
    {
        backBlocked = true;
        backBlockTimer = blockingDuration;

        if (currentSpeedRef < 0) currentSpeedRef = 0;
        if (flashEffect != null) flashEffect.BackFlash();
    }

    private void ProcessSideCollision(ControllerColliderHit hit, Vector3 impactDirection)
    {
        collisionNormal = hit.normal;

        Vector3 projection = Vector3.Project(wheelchairTransform.forward, collisionNormal);
        slideDirection = (wheelchairTransform.forward - projection).normalized;

        if (Mathf.Abs(controller.velocity.magnitude) > 0.1f)
        {
            wallSliding = true;
            slideTimer = 0.5f; 
        }

        float side = Vector3.Dot(wheelchairTransform.right, impactDirection);

        if (flashEffect != null)
        {
            if (side > 0) flashEffect.RightSideFlash();
            else flashEffect.LeftSideFlash();
        }
    }

    private void ResetCollisionState()
    {
        inCollision = false;
        collidedObject = "";
        collisionCount = 0;
        multiCollisionResetTime = 0f;
    }

    private void ForceResetCollisions()
    {
        frontBlocked = false;
        backBlocked = false;
        wallSliding = false;
        slideDirection = Vector3.zero;
        frontBlockTimer = 0f;
        backBlockTimer = 0f;
        slideTimer = 0f;
        ResetCollisionState();
    }

    public void ClearSlide()
    {
        wallSliding = false;
        slideDirection = Vector3.zero;
        slideTimer = 0f;
    }

    private void OnDrawGizmos()
    {
        Transform t = wheelchairTransform != null ? wheelchairTransform : transform;

        if (enableCollisionDebug && useFrontSensor)
        {
            Vector3 scaledOffsetF = Vector3.Scale(frontSensorOffset, t.lossyScale);
            float scaledLengthF = frontSensorLength * t.lossyScale.z;
            Vector3 scaledBoxSizeF = Vector3.Scale(frontSensorBoxSize, t.lossyScale);

            Vector3 startPosF = t.position +
                                t.forward * scaledOffsetF.z +
                                t.up * scaledOffsetF.y +
                                t.right * scaledOffsetF.x;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(startPosF, t.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.forward * (scaledLengthF / 2f), new Vector3(scaledBoxSizeF.x, scaledBoxSizeF.y, scaledLengthF));
        }

        if (enableCollisionDebug && useBackSensor)
        {
            Vector3 scaledOffsetB = Vector3.Scale(backSensorOffset, t.lossyScale);
            float scaledLengthB = backSensorLength * t.lossyScale.z;
            Vector3 scaledBoxSizeB = Vector3.Scale(backSensorBoxSize, t.lossyScale);

            Vector3 startPosB = t.position +
                                t.forward * scaledOffsetB.z +
                                t.up * scaledOffsetB.y +
                                t.right * scaledOffsetB.x;

            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(startPosB, t.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.back * (scaledLengthB / 2f), new Vector3(scaledBoxSizeB.x, scaledBoxSizeB.y, scaledLengthB));
        }

        if (enableCollisionDebug && useBackWheelVisualSensors)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);

            float scaledLengthBL = backWheelSensorLength * t.lossyScale.x;
            Vector3 scaledBoxSizeBL = Vector3.Scale(backWheelSensorBoxSize, t.lossyScale);

            Vector3 scaledOffsetBL = Vector3.Scale(backLeftWheelOffset, t.lossyScale);
            Vector3 startPosBL = t.position + t.forward * scaledOffsetBL.z + t.up * scaledOffsetBL.y + t.right * scaledOffsetBL.x;
            Gizmos.matrix = Matrix4x4.TRS(startPosBL, t.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.left * (scaledLengthBL / 2f), new Vector3(scaledLengthBL, scaledBoxSizeBL.y, scaledBoxSizeBL.z));

            Vector3 scaledOffsetBR = Vector3.Scale(backRightWheelOffset, t.lossyScale);
            Vector3 startPosBR = t.position + t.forward * scaledOffsetBR.z + t.up * scaledOffsetBR.y + t.right * scaledOffsetBR.x;
            Gizmos.matrix = Matrix4x4.TRS(startPosBR, t.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.right * (scaledLengthBL / 2f), new Vector3(scaledLengthBL, scaledBoxSizeBL.y, scaledBoxSizeBL.z));
        }
    }

    public bool IsFrontBlocked => frontBlocked;
    public bool IsBackBlocked => backBlocked;
    public bool IsWallSliding => wallSliding;
    public Vector3 SlideDirection => slideDirection;
    public bool IsInCollision => inCollision;
    public string CollidedObject => collidedObject;
    public bool IsStuck => collisionCount > 2 || multiCollisionResetTime > 0.3f;
}