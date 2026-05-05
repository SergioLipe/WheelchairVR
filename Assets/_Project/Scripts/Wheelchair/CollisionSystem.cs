using UnityEngine;

/// <summary>
/// Collision detection and management system for the wheelchair
/// Processes CharacterController collisions and manages directional blocking and wall sliding
/// Corrigido para comunicar corretamente com o LevelManager_PC
/// </summary>
public class CollisionSystem : MonoBehaviour
{
    [Header("=== Collision State (Debug) ===")]
    [SerializeField] private bool inCollision = false;
    [SerializeField] private string collidedObject = "";
    [SerializeField] private bool frontBlocked = false;
    [SerializeField] private bool backBlocked = false;
    [SerializeField] private bool wallSliding = false;

    [Header("=== Debug Settings ===")]
    [Tooltip("Enable this to print collision names to the Console and draw red lines in Scene view.")]
    public bool enableCollisionDebug = true;

    [Header("=== Detection Settings ===")]
    [Tooltip("Minimum collision point height to be considered (ignores ground)")]
    [SerializeField] private float minCollisionHeight = 0.2f;

    [Tooltip("Maximum angle with vertical to ignore (90° = perfect horizontal)")]
    [SerializeField] private float maxGroundAngle = 45f;

    [Tooltip("Tags to ignore in collisions (optional)")]
    [SerializeField] private string[] ignoreTags = { "Ground", "Floor", "Terrain" };

    [Tooltip("Layers to ignore in collisions (optional)")]
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

    // Multiple collision handling
    private int collisionCount = 0;
    private float multiCollisionResetTime = 0f;

    // Directional blocking system
    private float frontBlockTimer = 0f;
    private float backBlockTimer = 0f;
    private const float blockingDuration = 0.15f; // Reduced for faster recovery

    // Wall sliding system
    private Vector3 slideDirection = Vector3.zero;
    private float slideTimer = 0f;

    // Controlls time between warnings to LevelManager to prevent spamming
    private float lastManagerUpdate = 0f;
    private float lastSlideTime = 0f;

    /// <summary>
    /// Initialize collision system with necessary references
    /// </summary>
    public void Initialize(CharacterController characterController, Transform transform)
    {
        this.controller = characterController;
        this.wheelchairTransform = transform;

        // Get or create flash component
        flashEffect = GetComponent<CollisionFlashEffect>();
        if (flashEffect == null)
        {
            flashEffect = gameObject.AddComponent<CollisionFlashEffect>();
        }
    }

    /// <summary>
    /// Update collision system
    /// </summary>
    public void Update()
    {
        UpdateBlockingTimers();
        UpdateCollisionState();
        UpdateSlideTimer();
        HandleMultipleCollisions();
    }

    /// <summary>
    /// Updates individual blocking timers for front and back
    /// </summary>
    private void UpdateBlockingTimers()
    {
        // Front block timer
        if (frontBlockTimer > 0)
        {
            frontBlockTimer -= Time.deltaTime;
            if (frontBlockTimer <= 0)
            {
                frontBlocked = false;
            }
        }

        // Back block timer
        if (backBlockTimer > 0)
        {
            backBlockTimer -= Time.deltaTime;
            if (backBlockTimer <= 0)
            {
                backBlocked = false;
            }
        }
    }

    /// <summary>
    /// Updates general collision state
    /// </summary>
    private void UpdateCollisionState()
    {
        // Reset collision state after a brief period
        if (inCollision && Time.time - collisionTime > 0.3f)
        {
            ResetCollisionState();
        }
    }

    /// <summary>
    /// Updates slide timer and clears sliding when expired
    /// </summary>
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

    /// <summary>
    /// Handles multiple simultaneous collisions to prevent getting stuck
    /// </summary>
    private void HandleMultipleCollisions()
    {
        // If multiple collisions detected, start reset timer
        if (collisionCount > 1)
        {
            multiCollisionResetTime += Time.deltaTime;

            // Force reset after brief period to prevent permanent stuck
            if (multiCollisionResetTime > 0.5f)
            {
                ForceResetCollisions();
            }
        }
        else
        {
            multiCollisionResetTime = 0f;
        }

        // Gradually reduce collision count
        if (collisionCount > 0 && Time.time - collisionTime > 0.1f)
        {
            collisionCount = Mathf.Max(0, collisionCount - 1);
        }
    }

    /// <summary>
    /// Process CharacterController collisions
    /// </summary>
    public void ProcessCollision(ControllerColliderHit hit, float currentSpeed, ref float currentSpeedRef)
    {
        // Check if should ignore this collision
        if (ShouldIgnoreCollision(hit))
            return;

        // Debugging: Print collision info and draw hit point in Scene view
        if (enableCollisionDebug)
        {
            // Print the exact object name, tag, and layer to the Unity Console
            Debug.Log($"<color=orange>[Collision Debug]</color> Wheelchair hit: <b>{hit.gameObject.name}</b> | Tag: {hit.gameObject.tag}");

            // Draw a red line in the Scene view showing exactly WHERE the hit happened
            Debug.DrawRay(hit.point, hit.normal * 1.5f, Color.red, 2f);
        }

        // Prevent collision spam
        float timeSinceLastCollision = Time.time - lastValidCollisionTime;
        if (timeSinceLastCollision < 0.05f)
            return;

        // Determine collision direction
        Vector3 dirToObstacle = (hit.point - wheelchairTransform.position);
        dirToObstacle.y = 0;

        // Safety check for zero vector
        if (dirToObstacle.sqrMagnitude < 0.001f)
            return;

        dirToObstacle.Normalize();

        float angle = Vector3.Angle(wheelchairTransform.forward, dirToObstacle);

        // Process collision based on angle with priority system
        bool collisionProcessed = false;

        if (angle < 60f && !frontBlocked) // Front collision
        {
            ProcessFrontCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        else if (angle > 120f && !backBlocked) // Back collision
        {
            ProcessBackCollision(ref currentSpeedRef);
            collisionProcessed = true;
        }
        else if (angle >= 60f && angle <= 120f) // Side collision
        {
            ProcessSideCollision(hit, dirToObstacle);
            collisionProcessed = true;
        }

        // Only update state if collision was processed
        if (collisionProcessed)
        {
            inCollision = true;
            collidedObject = hit.gameObject.name;
            collisionPoint = hit.point;
            collisionTime = Time.time;
            lastValidCollisionTime = Time.time;
            collisionCount++;

            // Limit collision count
            collisionCount = Mathf.Min(collisionCount, 3);
        }
    }

    /// <summary>
    /// Checks if should ignore collision based on multiple criteria
    /// </summary>
    private bool ShouldIgnoreCollision(ControllerColliderHit hit)
    {
        // 1. Check collision point height (ignore ground)
        float collisionHeight = hit.point.y - wheelchairTransform.position.y;
        if (collisionHeight < minCollisionHeight)
        {
            return true;
        }

        // 2. Check surface normal angle (ignore horizontal surfaces)
        float angleWithUp = Vector3.Angle(hit.normal, Vector3.up);
        if (angleWithUp < maxGroundAngle)
        {
            return true;
        }

        // 3. Check object name (backwards compatibility)
        if (IsFloorObject(hit.gameObject.name))
        {
            return true;
        }

        // 4. Check object tag (only if tags array is not empty and tag exists)
        if (ignoreTags != null && ignoreTags.Length > 0 && !string.IsNullOrEmpty(hit.gameObject.tag))
        {
            string objectTag = hit.gameObject.tag;
            foreach (string tag in ignoreTags)
            {
                if (!string.IsNullOrEmpty(tag) && objectTag == tag)
                {
                    return true;
                }
            }
        }

        // 5. Check object layer
        if (ignoreLayerMask != 0)
        {
            int objectLayer = hit.gameObject.layer;
            if ((ignoreLayerMask.value & (1 << objectLayer)) != 0)
            {
                return true;
            }
        }

        // 6. Check if object has Terrain component
        if (hit.gameObject.GetComponent<Terrain>() != null)
        {
            return true;
        }

        // 7. Check vertical collision velocity (ignore collisions from below)
        if (hit.moveDirection.y < -0.3f)
        {
            return true;
        }

        // 8. Additional check: if collision normal is mostly pointing up (ground-like)
        if (hit.normal.y > 0.7f)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes front collision
    /// </summary>
    private void ProcessFrontCollision(ref float currentSpeedRef)
    {
        frontBlocked = true;
        frontBlockTimer = blockingDuration;

        // Only stop if moving forward
        if (currentSpeedRef > 0)
        {
            currentSpeedRef = 0;
        }

        if (flashEffect != null)
            flashEffect.FrontFlash();

        // Checks if its been 1 second since last warning to LevelManager_PC to prevent spamming
        if (Time.time > lastManagerUpdate + 1.0f)
        {
            if (LevelManager_PC.Instance != null)
            {
                LevelManager_PC.Instance.RegisterStrongCollision("Front Obstacle");
                lastManagerUpdate = Time.time; // Update the timer HERE
            }
        }
    }

    /// <summary>
    /// Processes back collision
    /// </summary>
    private void ProcessBackCollision(ref float currentSpeedRef)
    {
        backBlocked = true;
        backBlockTimer = blockingDuration;

        // Only stop if moving backward
        if (currentSpeedRef < 0)
        {
            currentSpeedRef = 0;
        }

        if (flashEffect != null)
            flashEffect.BackFlash();

        // Checks if its been 1 second since last warning to LevelManager_PC to prevent spamming
        if (Time.time > lastManagerUpdate + 1.0f)
        {
            if (LevelManager_PC.Instance != null)
            {
                LevelManager_PC.Instance.RegisterStrongCollision("Back Obstacle");
                lastManagerUpdate = Time.time; // Update the timer HERE
            }
        }
    }

    /// <summary>
    /// Processes side collision and calculates slide direction
    /// </summary>
    private void ProcessSideCollision(ControllerColliderHit hit, Vector3 dirToObstacle)
    {
        collisionNormal = hit.normal;

        // Calculate tangential slide direction using vector projection
        Vector3 projection = Vector3.Project(wheelchairTransform.forward, collisionNormal);
        slideDirection = (wheelchairTransform.forward - projection).normalized;

        // Only enable sliding if moving with some speed
        if (Mathf.Abs(controller.velocity.magnitude) > 0.1f)
        {
            wallSliding = true;
            slideTimer = 0.3f; // Slide for a brief period

            // Checks if 1 second has passed since the last registered slide.
            if (Time.time > lastSlideTime + 1.0f)
            {
                if (LevelManager_PC.Instance != null)
                {
                    LevelManager_PC.Instance.RegisterSlide();
                    // Update the specific slide timer HERE, inside the if block!
                    lastSlideTime = Time.time;
                }
            }
        }

        // Determine if left or right side for visual effects
        float side = Vector3.Dot(wheelchairTransform.right, dirToObstacle);

        if (flashEffect != null)
        {
            if (side > 0)
                flashEffect.RightSideFlash();
            else
                flashEffect.LeftSideFlash();
        }
    }

    /// <summary>
    /// Checks if object name indicates it's a floor/ground
    /// </summary>
    private bool IsFloorObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string name = objectName.ToLower();
        return name.Contains("plane") ||
               name.Contains("ground") ||
               name.Contains("floor") ||
               name.Contains("terrain") ||
               name.Contains("chao") ||
               name.Contains("piso") ||
               name.Contains("solo");
    }

    /// <summary>
    /// Reset collision state
    /// </summary>
    private void ResetCollisionState()
    {
        inCollision = false;
        collidedObject = "";
        collisionCount = 0;
        multiCollisionResetTime = 0f;
    }

    /// <summary>
    /// Force reset all collisions (used when stuck)
    /// </summary>
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

    /// <summary>
    /// Clear wall sliding state
    /// </summary>
    public void ClearSlide()
    {
        wallSliding = false;
        slideDirection = Vector3.zero;
        slideTimer = 0f;
    }

    // ===== PUBLIC PROPERTIES =====

    public bool IsFrontBlocked => frontBlocked;
    public bool IsBackBlocked => backBlocked;
    public bool IsWallSliding => wallSliding;
    public Vector3 SlideDirection => slideDirection;
    public bool IsInCollision => inCollision;
    public string CollidedObject => collidedObject;

    // Additional property to check if stuck
    public bool IsStuck => collisionCount > 2 || multiCollisionResetTime > 0.3f;
}