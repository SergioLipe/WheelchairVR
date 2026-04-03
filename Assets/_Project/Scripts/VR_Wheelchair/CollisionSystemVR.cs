using UnityEngine;

/// <summary>
/// VR-Optimized collision detection and management system.
/// Uses surface normals (Dot Product) for physical impacts and a 
/// proactive BoxCast sensor (Option 2) for the front footrests.
/// Includes Advanced Debugging to track down "phantom" collisions.
/// </summary>
public class CollisionSystemVR : MonoBehaviour
{
    [Header("=== Collision State (Debug) ===")]
    [SerializeField] private bool inCollision = false;
    [SerializeField] private string collidedObject = "";
    [SerializeField] private bool frontBlocked = false;
    [SerializeField] private bool backBlocked = false;
    [SerializeField] private bool wallSliding = false;

    // Stats for collision tracking
    public int TotalCollisions { get; private set; } = 0;
    public int TotalSlides { get; private set; } = 0;

    // Only Count one collision per object until it's resolved to prevent spam
    private bool wasInCollisionState = false;
    private bool wasSlidingState = false;
    
    // NEW: Slide cooldown to prevent micro-bounce spam
    private float lastSlideCountTime = 0f;

    [Header("=== Debug Settings ===")]
    [Tooltip("Enable to print exactly WHAT you hit to the Unity Console")]
    public bool enableCollisionDebug = true;

    [Header("=== Front Sensor (Option 2) ===")]
    [Tooltip("Enable the proactive front sensor for footrests")]
    public bool useFrontSensor = true;

    [Tooltip("How far ahead the sensor checks (meters)")]
    public float sensorLength = 0.3f;

    [Tooltip("Size of the sensor box (Width, Height, Depth)")]
    public Vector3 sensorBoxSize = new Vector3(0.4f, 0.2f, 0.1f);

    [Tooltip("Offset from the center of the wheelchair (X, Y, Z)")]
    public Vector3 sensorOffset = new Vector3(0f, 0.2f, 0.4f);

    [Tooltip("Layers the sensor should detect as obstacles")]
    public LayerMask obstacleLayerMask = ~0;

    [Header("=== Detection Settings ===")]
    [Tooltip("Minimum collision point height to be considered (ignores ground)")]
    [SerializeField] private float minCollisionHeight = 0.2f;

    [Tooltip("Maximum angle with vertical to ignore (90° = perfect horizontal)")]
    [SerializeField] private float maxGroundAngle = 45f;

    [Tooltip("Tags to ignore in collisions")]
    [SerializeField] private string[] ignoreTags = { "Ground", "Floor", "Terrain" };

    [Tooltip("Layers to ignore in collisions")]
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

    // Directional blocking system
    private float frontBlockTimer = 0f;
    private float backBlockTimer = 0f;
    private const float blockingDuration = 0.15f;

    // Wall sliding system
    private Vector3 slideDirection = Vector3.zero;
    private float slideTimer = 0f;

    // Front sensor state
    private bool wasFrontSensorBlockedLastFrame = false;

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
        UpdateBlockingTimers();
        UpdateCollisionState();
        UpdateSlideTimer();
        HandleMultipleCollisions();

        if (useFrontSensor)
        {
            CheckFrontSensor();
        }

        // Update collision stats
        if (inCollision && !wasInCollisionState) TotalCollisions++;
        wasInCollisionState = inCollision;

        // FIXED: Only count a slide if 1 second has passed since the last count
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

    /// <summary>
    /// Option 2: Projects a BoxCast forward to detect obstacles before the physical capsule hits them.
    /// Perfect for long wheelchair footrests.
    /// </summary>
    private void CheckFrontSensor()
    {
        if (wheelchairTransform == null) return;

        Vector3 startPos = wheelchairTransform.position +
                           wheelchairTransform.forward * sensorOffset.z +
                           wheelchairTransform.up * sensorOffset.y +
                           wheelchairTransform.right * sensorOffset.x;

        Vector3 halfExtents = sensorBoxSize / 2f;
        bool hitObstacle = false;

        RaycastHit[] hits = Physics.BoxCastAll(startPos, halfExtents, wheelchairTransform.forward, wheelchairTransform.rotation, sensorLength, obstacleLayerMask);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == wheelchairTransform.root) continue;

            float collisionHeight = hit.point.y - wheelchairTransform.position.y;
            if (collisionHeight < minCollisionHeight) continue;

            float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
            if (angleWithUp < maxGroundAngle) continue;

            bool ignore = false;
            foreach (string tag in ignoreTags)
            {
                if (hit.collider.tag == tag)
                {
                    ignore = true;
                    break;
                }
            }
            if (ignore) continue;

            if (ignoreLayerMask != 0 && ((ignoreLayerMask.value & (1 << hit.collider.gameObject.layer)) != 0)) continue;
            if (hit.collider.GetComponent<Terrain>() != null) continue;
            if (hit.collider.isTrigger) continue; // IGNORE TRIGGERS (Common cause of phantom hits)

            hitObstacle = true;
            collidedObject = hit.collider.gameObject.name;
            collisionPoint = hit.point;

            // --- EXTREME DEBUG FOR FRONT SENSOR ---
            if (enableCollisionDebug && !wasFrontSensorBlockedLastFrame)
            {
                Debug.LogWarning($"<color=cyan>[FRONT SENSOR HIT]</color> Wheelchair stopped by: <b>{collidedObject}</b> | Tag: {hit.collider.tag} | Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                Debug.DrawRay(hit.point, Vector3.up * 2f, Color.cyan, 3f); // Draws a cyan line pointing up where it hit
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
            if (backBlockTimer <= 0) backBlocked = false;
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

    // Physical collisions (sides and back, or front if sensor misses)
    public void ProcessCollision(ControllerColliderHit hit, float currentSpeed, ref float currentSpeedRef)
    {
        if (ShouldIgnoreCollision(hit)) return;

        float timeSinceLastCollision = Time.time - lastValidCollisionTime;
        if (timeSinceLastCollision < 0.05f) return;

        // --- EXTREME DEBUG FOR CAPSULE ---
        if (enableCollisionDebug)
        {
            Debug.LogWarning($"<color=orange>[CAPSULE HIT]</color> Wheelchair touched: <b>{hit.gameObject.name}</b> | Tag: {hit.gameObject.tag} | Layer: {LayerMask.LayerToName(hit.gameObject.layer)}");
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
        if (hit.collider.isTrigger) return true; // IGNORE TRIGGERS (Common cause of phantom hits)
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
            // FIXED: Increased timer from 0.3f to 0.5f to tolerate physics micro-bounces
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
        if (enableCollisionDebug && useFrontSensor)
        {
            Transform t = wheelchairTransform != null ? wheelchairTransform : transform;

            Vector3 startPos = t.position +
                               t.forward * sensorOffset.z +
                               t.up * sensorOffset.y +
                               t.right * sensorOffset.x;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(startPos, t.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.forward * (sensorLength / 2f), new Vector3(sensorBoxSize.x, sensorBoxSize.y, sensorLength));
        }
    }

    // Public Properties
    public bool IsFrontBlocked => frontBlocked;
    public bool IsBackBlocked => backBlocked;
    public bool IsWallSliding => wallSliding;
    public Vector3 SlideDirection => slideDirection;
    public bool IsInCollision => inCollision;
    public string CollidedObject => collidedObject;
    public bool IsStuck => collisionCount > 2 || multiCollisionResetTime > 0.3f;
}