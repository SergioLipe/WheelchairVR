using UnityEngine;

public class WaypointPatrol : MonoBehaviour
{
    [Header("=== Path Settings ===")]
    public Transform[] waypoints;
    public float speed = 1.2f;
    public float rotationSpeed = 5.0f;
    
    [Header("=== Safety Sensors ===")]
    [Tooltip("How far the pedestrian looks straight ahead.")]
    public float frontDetectionDistance = 2.0f; 
    
    [Tooltip("How far the pedestrian looks to the sides.")]
    public float obliqueDetectionDistance = 1.2f;
    
    [Tooltip("The angle (in degrees) for the side sensors.")]
    public float obliqueSensorAngle = 30f;
    
    [Tooltip("The tag assigned to the player/wheelchair.")]
    public string playerTag = "Player";

    private int currentWaypointIndex = 0;
    private Animator anim;
    private bool isWaiting = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (waypoints.Length == 0) return;

        CheckForPlayer();

        if (isWaiting)
        {
            // Pause animation and stop moving
            if (anim != null) anim.speed = 0f; 
            return; 
        }
        else
        {
            // Resume animation
            if (anim != null) anim.speed = 1f;
        }

        // Normal movement logic
        Transform target = waypoints[currentWaypointIndex];
        
        Vector3 direction = target.position - transform.position;
        direction.y = 0; 

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, new Vector3(target.position.x, transform.position.y, target.position.z), speed * Time.deltaTime);

        float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.position.x, 0, target.position.z));
        
        if (distance < 0.2f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    private void CheckForPlayer()
    {
        // Shoot raycasts from chest height (1 meter up)
        Vector3 origin = transform.position + Vector3.up * 1.0f; 
        
        Vector3 forwardDir = transform.forward;
        Vector3 leftObliqueDir = Quaternion.AngleAxis(-obliqueSensorAngle, Vector3.up) * transform.forward;
        Vector3 rightObliqueDir = Quaternion.AngleAxis(obliqueSensorAngle, Vector3.up) * transform.forward;
        
        // Draws yellow lines in the Scene view so you can see the sensor fan!
        Debug.DrawRay(origin, forwardDir * frontDetectionDistance, Color.yellow);
        Debug.DrawRay(origin, leftObliqueDir * obliqueDetectionDistance, Color.yellow);
        Debug.DrawRay(origin, rightObliqueDir * obliqueDetectionDistance, Color.yellow);

        // If ANY of the 3 lasers hit the player, the pedestrian stops
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
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, distance))
        {
            // Check if the hit object or its root parent has the Player tag
            if (hit.collider.CompareTag(playerTag) || hit.collider.transform.root.CompareTag(playerTag))
            {
                return true;
            }
        }
        return false;
    }
}