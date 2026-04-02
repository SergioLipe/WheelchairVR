using UnityEngine;

/// <summary>
/// VR-Optimized collision detection and management system.
/// Uses surface normals (Dot Product) instead of hit points to ensure 
/// precise detection of frontal, rear, and lateral impacts in VR.
/// </summary>
public class CollisionSystemVR : MonoBehaviour
{
    [Header("=== Collision State (Debug) ===")]
    [SerializeField] private bool inCollision = false;
    [SerializeField] private string collidedObject = "";
    [SerializeField] private bool frontBlocked = false;
    [SerializeField] private bool backBlocked = false;
    [SerializeField] private bool wallSliding = false;

    [Header("=== Debug Settings ===")]
    public bool enableCollisionDebug = true;

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
    private CollisionFlashEffect flashEffect;

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

    // Spam prevention timers
    private float lastManagerUpdate = 0f;
    private float lastSlideTime = 0f;

    public void Initialize(CharacterController characterController, Transform transform)
    {
        this.controller = characterController;
        this.wheelchairTransform = transform;

        flashEffect = GetComponent<CollisionFlashEffect>();
        if (flashEffect == null)
        {
            Debug.LogWarning("CollisionFlashEffect missing. Adding one automatically.");
            flashEffect = gameObject.AddComponent<CollisionFlashEffect>();
        }
    }

    public void Update()
    {
        UpdateBlockingTimers();
        UpdateCollisionState();
        UpdateSlideTimer();
        HandleMultipleCollisions();
    }

    private void UpdateBlockingTimers()
    {
        if (frontBlockTimer > 0)
        {
            frontBlockTimer -= Time.deltaTime;
            if (frontBlockTimer <= 0) frontBlocked = false;
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

    public void ProcessCollision(ControllerColliderHit hit, float currentSpeed, ref float currentSpeedRef)
    {
        if (ShouldIgnoreCollision(hit)) return;

        if (enableCollisionDebug)
        {
            Debug.DrawRay(hit.point, hit.normal * 1.5f, Color.red, 2f);
        }

        float timeSinceLastCollision = Time.time - lastValidCollisionTime;
        if (timeSinceLastCollision < 0.05f) return;

        // VR FIX: Use the surface normal to determine direction, not the hit point.
        // -hit.normal points from the obstacle directly towards the player.
        Vector3 impactDirection = -hit.normal;
        impactDirection.y = 0;

        if (impactDirection.sqrMagnitude < 0.001f) return;
        impactDirection.Normalize();

        // Calculate Dot Product to determine hit location accurately
        // 1.0 = dead front, -1.0 = dead back, 0.0 = perfect side
        float forwardDot = Vector3.Dot(wheelchairTransform.forward, impactDirection);
        bool collisionProcessed = false;

        // Frontal hit (Greater than 0.5)
        if (forwardDot > 0.5f && !frontBlocked)
        {
            ProcessFrontCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        // Rear hit (Less than -0.5)
        else if (forwardDot < -0.5f && !backBlocked)
        {
            ProcessBackCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        // Side hit (Between -0.5 and 0.5)
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

        // Optional LevelManager hook
        /*
        if (Time.time > lastManagerUpdate + 1.0f && LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterStrongCollision("Front Obstacle");
            lastManagerUpdate = Time.time;
        }
        */
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

        // Calculate slide direction (tangent to the wall)
        Vector3 projection = Vector3.Project(wheelchairTransform.forward, collisionNormal);
        slideDirection = (wheelchairTransform.forward - projection).normalized;

        if (Mathf.Abs(controller.velocity.magnitude) > 0.1f)
        {
            wallSliding = true;
            slideTimer = 0.3f;
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

    // Public Properties
    public bool IsFrontBlocked => frontBlocked;
    public bool IsBackBlocked => backBlocked;
    public bool IsWallSliding => wallSliding;
    public Vector3 SlideDirection => slideDirection;
    public bool IsInCollision => inCollision;
    public string CollidedObject => collidedObject;
    public bool IsStuck => collisionCount > 2 || multiCollisionResetTime > 0.3f;
}