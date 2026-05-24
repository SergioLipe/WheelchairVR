using UnityEngine;

/// <summary>
/// Waypoint patrol for pedestrians with player detection.
/// Optimized for VR: throttled raycasts, cached transforms, no debug overhead in builds.
/// </summary>
public class WaypointPatrol : MonoBehaviour
{
    [Header("=== Path Settings ===")]
    public Transform[] waypoints;
    public float speed = 1.2f;
    public float rotationSpeed = 5.0f;

    [Header("=== Safety Sensors ===")]
    public float frontDetectionDistance = 2.0f;
    public float obliqueDetectionDistance = 1.2f;
    public float obliqueSensorAngle = 30f;
    public string playerTag = "Player";

    [Header("=== Optimization ===")]
    [Tooltip("Layers to detect (Player layer recommended).")]
    public LayerMask detectionLayerMask = ~0;

    [Tooltip("How often to check for player (per second). 5-10 is plenty.")]
    [Range(2, 30)]
    public int sensorChecksPerSecond = 8;

    [Tooltip("Draw debug rays in Scene view (auto-disabled in builds)")]
    public bool drawDebugRays = false;

    private int currentWaypointIndex = 0;
    private Animator anim;
    private bool isWaiting = false;

    // [OPT] Cache de transform
    private Transform myTransform;
    private float lastSensorCheck = 0f;
    private float sensorInterval;

    // [OPT] Pre-allocated buffer for raycast
    private static readonly RaycastHit[] s_RaycastBuffer = new RaycastHit[4];

    // [OPT] Pre-calculated sensor directions (recalcula no Awake/quando rotates)
    private float lastForwardAngle = float.MaxValue;
    private Vector3 cachedForward;
    private Vector3 cachedLeftOblique;
    private Vector3 cachedRightOblique;

    void Awake()
    {
        myTransform = transform;
        sensorInterval = 1f / sensorChecksPerSecond;
    }

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (waypoints.Length == 0) return;

        // [OPT] Throttle sensor check (não precisa cada frame)
        if (Time.time - lastSensorCheck >= sensorInterval)
        {
            CheckForPlayer();
            lastSensorCheck = Time.time;
        }

        if (isWaiting)
        {
            if (anim != null) anim.speed = 0f;
            return;
        }
        else
        {
            if (anim != null) anim.speed = 1f;
        }

        // [OPT] cache transform
        Vector3 myPos = myTransform.position;
        Transform target = waypoints[currentWaypointIndex];
        Vector3 targetPos = target.position;

        Vector3 direction = targetPos - myPos;
        direction.y = 0;

        // [OPT] sqrMagnitude em vez de magnitude
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            myTransform.rotation = Quaternion.Slerp(myTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 targetPosFlat = new Vector3(targetPos.x, myPos.y, targetPos.z);
        myTransform.position = Vector3.MoveTowards(myPos, targetPosFlat, speed * Time.deltaTime);

        // [OPT] sqrMagnitude para distance check
        Vector3 flatDelta = new Vector3(myTransform.position.x - targetPos.x, 0f, myTransform.position.z - targetPos.z);
        if (flatDelta.sqrMagnitude < 0.04f) // 0.2^2
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    private void CheckForPlayer()
    {
        Vector3 origin = myTransform.position + Vector3.up;
        Vector3 forwardDir = myTransform.forward;

        // [OPT] Inline rotation math (em vez de Quaternion.AngleAxis × 2)
        float angleRad = obliqueSensorAngle * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angleRad);
        float cos = Mathf.Cos(angleRad);

        Vector3 leftObliqueDir = new Vector3(
            forwardDir.x * cos - forwardDir.z * sin,
            forwardDir.y,
            forwardDir.x * sin + forwardDir.z * cos
        );

        Vector3 rightObliqueDir = new Vector3(
            forwardDir.x * cos + forwardDir.z * sin,
            forwardDir.y,
            -forwardDir.x * sin + forwardDir.z * cos
        );

        #if UNITY_EDITOR
        if (drawDebugRays)
        {
            Debug.DrawRay(origin, forwardDir * frontDetectionDistance, Color.yellow);
            Debug.DrawRay(origin, leftObliqueDir * obliqueDetectionDistance, Color.yellow);
            Debug.DrawRay(origin, rightObliqueDir * obliqueDetectionDistance, Color.yellow);
        }
        #endif

        if (CheckSingleRay(origin, forwardDir, frontDetectionDistance) ||
            CheckSingleRay(origin, leftObliqueDir, obliqueDetectionDistance) ||
            CheckSingleRay(origin, rightObliqueDir, obliqueDetectionDistance))
        {
            isWaiting = true;
        }
        else
        {
            isWaiting = false;
        }
    }

    private bool CheckSingleRay(Vector3 origin, Vector3 direction, float distance)
    {
        // [OPT] RaycastNonAlloc (zero garbage)
        int hitCount = Physics.RaycastNonAlloc(origin, direction, s_RaycastBuffer, distance, detectionLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = s_RaycastBuffer[i].collider;
            if (col.CompareTag(playerTag) || col.transform.root.CompareTag(playerTag))
            {
                return true;
            }
        }
        return false;
    }
}