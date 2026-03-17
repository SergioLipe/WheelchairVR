using UnityEngine;

/// <summary>
/// Controls city traffic with straight-line driving and curve-turning at intersections.
/// Features: Stop Zones, Crosswalk Overrides, Player Detection, Failsafes, and TIGHT Trigger-based Turning.
/// </summary>
public class CarCityMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    [Tooltip("How fast the car moves forward on a straight road.")]
    public float speed = 10f;
    
    [Tooltip("Controlled by the Traffic Light. True = Green Light (Go), False = Red Light (Stop).")]
    public bool canMove = true;

    [Header("=== Turn Settings ===")]
    [Tooltip("The Tag this car looks for to start turning. (e.g., 'TurnZone')")]
    public string turnZoneTag = "TurnZone";
    
    [Tooltip("How many degrees to turn. (e.g., 180 = U-Turn to the adjacent lane, 90 = Right turn)")]
    public float turnAngle = 180f;
    
    [Tooltip("How fast the car rotates. Higher = sharper/faster turn.")]
    public float turnSpeed = 120f;

    [Tooltip("THE SECRET TO A TIGHT TURN: How fast the car moves forward WHILE turning. Lower this for a tighter curve!")]
    public float speedDuringTurn = 3f;

    [Header("=== Zone Settings ===")]
    public string targetStopZoneTag = "StopZone";
    public string neverStopZoneTag = "CrosswalkZone";

    [Header("=== Collision Sensor Settings ===")]
    public float frontSensorLength = 8f;
    public float obliqueSensorLength = 5f;
    public float obliqueSensorAngle = 25f;

    [Header("=== Stuck Failsafe Settings ===")]
    public float maxWaitTime = 7f;

    // --- Internal State Tracking (Zones & Sensors) ---
    private bool isInStopZone = false;
    private bool isInNeverStopZone = false;
    private float stuckTimer = 0f;
    private bool ignoreObliqueCars = false;

    // --- Internal State Tracking (Turning logic) ---
    private bool isTurning = false;
    private float degreesTurned = 0f;
    private float currentTargetAngle = 0f;
    private float turnDirection = 1f;

    void Update()
    {
        // 1. Check sensors separately for Front and Oblique obstacles
        bool centerBlocked;
        bool obliqueBlocked;
        CheckSensors(out centerBlocked, out obliqueBlocked);
        
        // 2. Check legal movement (green light OR outside of a red light stop zone)
        bool wantsToMove = canMove || !isInStopZone;

        // 3. CROSSWALK OVERRIDE (NEVER STOP ZONE)
        if (isInNeverStopZone)
        {
            // Force the car to keep moving, ignore all sensors and red lights
            wantsToMove = true;
            centerBlocked = false;
            obliqueBlocked = false;
        }

        // 4. SAFETY SENSOR & FAILSAFE LOGIC
        if (centerBlocked)
        {
            // A car or PLAYER is DIRECTLY in front! We must STOP and NEVER ignore it.
            stuckTimer = 0f; 
            ignoreObliqueCars = false;
            return; // Halt movement immediately
        }
        else if (obliqueBlocked && !ignoreObliqueCars)
        {
            // Obstacle ONLY on the sides. Apply the 7-second failsafe timer.
            if (wantsToMove)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= maxWaitTime)
                {
                    ignoreObliqueCars = true; // Timer reached: ignore the side obstacle
                }
            }
            return; // Halt movement while waiting for the timer
        }
        else if (!centerBlocked && !obliqueBlocked)
        {
            // The path is completely clear! Reset timer and flags.
            stuckTimer = 0f;
            ignoreObliqueCars = false;
        }

        // 5. APPLY MOVEMENT AND TURNING
        if (wantsToMove)
        {
            ApplyMovementAndTurning();
        }
    }

    /// <summary>
    /// Handles driving straight and smoothly curving when inside a turn sequence.
    /// Uses a slower forward speed during the turn to ensure the curve is tight enough for adjacent lanes.
    /// </summary>
    private void ApplyMovementAndTurning()
    {
        // By default, use the normal road speed
        float currentForwardSpeed = speed;

        // If the car hit a TurnZone and is currently turning
        if (isTurning)
        {
            // Use the slower speed so the turn is super tight!
            currentForwardSpeed = speedDuringTurn;

            // Calculate how much to rotate this specific frame
            float step = turnSpeed * Time.deltaTime;
            
            // Prevent the car from over-turning past the exact target angle
            if (degreesTurned + step >= currentTargetAngle)
            {
                step = currentTargetAngle - degreesTurned;
                isTurning = false; // Turn is complete! It will drive straight again at normal speed.
            }

            // Apply rotation (step * turnDirection makes it turn either right or left)
            transform.Rotate(Vector3.up, step * turnDirection);
            degreesTurned += step;
        }

        // Apply forward movement with whichever speed is currently active
        transform.Translate(Vector3.forward * currentForwardSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Checks the front and oblique sensors and outputs their specific states.
    /// </summary>
    private void CheckSensors(out bool centerBlocked, out bool obliqueBlocked)
    {
        // Slightly elevate the sensor so it doesn't hit the physical road
        Vector3 sensorStartPos = transform.position + new Vector3(0, 0.5f, 0);

        // Calculate sensor directions
        Vector3 forwardDir = transform.forward;
        Vector3 leftDir = Quaternion.AngleAxis(-obliqueSensorAngle, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(obliqueSensorAngle, Vector3.up) * transform.forward;

        // Check Front
        centerBlocked = CheckSingleRay(sensorStartPos, forwardDir, frontSensorLength);

        // Check Sides
        bool leftHit = CheckSingleRay(sensorStartPos, leftDir, obliqueSensorLength);
        bool rightHit = CheckSingleRay(sensorStartPos, rightDir, obliqueSensorLength);
        obliqueBlocked = leftHit || rightHit;
    }

    /// <summary>
    /// Helper method to fire a single raycast. Ignores trigger zones, looks for Cars and Players.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        // Ignore Triggers (like stop zones and turn zones) so sensors only hit physical objects
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            CarCityMovement otherCar = hit.collider.GetComponentInParent<CarCityMovement>();
            
            // Check if the obstacle is the Player (Wheelchair)
            bool isPlayer = hit.collider.CompareTag("Player");

            // Stop if it's ANOTHER car or if it is the PLAYER
            if ((otherCar != null && otherCar.gameObject != this.gameObject) || isPlayer)
            {
                Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                return true; // Obstacle detected
            }
        }

        Debug.DrawRay(startPos, direction * length, Color.green);
        return false; // Path clear
    }

    // --- TRIGGER EVENTS FOR ZONES ---

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) 
        {
            isInStopZone = true;
        }
        else if (other.CompareTag(neverStopZoneTag)) 
        {
            isInNeverStopZone = true;
        }
        else if (other.CompareTag(turnZoneTag) && !isTurning)
        {
            // The car hit a Turn Zone! Start a new turning sequence.
            isTurning = true;
            degreesTurned = 0f;
            
            // Absolute value guarantees we don't mess up the math
            currentTargetAngle = Mathf.Abs(turnAngle); 
            
            // Sign calculates if it's Right (1) or Left (-1)
            turnDirection = Mathf.Sign(turnAngle); 
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) isInStopZone = true;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) isInStopZone = false;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = false;
    }
}