using UnityEngine;

/// <summary>
/// Controls city traffic with straight-line driving and curve-turning at intersections.
/// Features: Stop Zones, Crosswalk Overrides, Player Detection (with root checking), 
/// Failsafes, and TIGHT Trigger-based Turning.
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
    [Tooltip("The Tag this car looks for to stop at red lights.")]
    public string targetStopZoneTag = "StopZone";

    [Tooltip("The Tag for areas where the car MUST NOT stop for red lights, like the middle of a crosswalk.")]
    public string neverStopZoneTag = "CrosswalkZone";

    [Header("=== Collision Sensor Settings ===")]
    [Tooltip("How far the main forward sensor looks ahead (in meters).")]
    public float frontSensorLength = 8f;

    [Tooltip("How far the oblique (angled) sensors look ahead.")]
    public float obliqueSensorLength = 5f;

    [Tooltip("The angle (in degrees) for the oblique sensors to point left and right.")]
    public float obliqueSensorAngle = 25f;

    [Header("=== Stuck Failsafe Settings ===")]
    [Tooltip("How many seconds to wait behind a side obstacle before ignoring it.")]
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

        // 3. CROSSWALK OVERRIDE (FIXED)
        if (isInNeverStopZone)
        {
            // Force the car to ignore Red Lights to clear the intersection.
            // NOTE: We DO NOT disable sensors here anymore. If the player is in front, it will still brake!
            wantsToMove = true;
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
            // Obstacle ONLY on the sides. Apply the failsafe timer.
            if (wantsToMove)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= maxWaitTime)
                {
                    // Timer reached: ignore the side obstacle so traffic can flow
                    ignoreObliqueCars = true;
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
    /// Upgraded to a 5-Raycast Fan system to eliminate blind spots where the player could hide.
    /// </summary>
    private void CheckSensors(out bool centerBlocked, out bool obliqueBlocked)
    {
        // Slightly elevate the sensor so it doesn't hit the physical road
        Vector3 sensorStartPos = transform.position + new Vector3(0, 0.5f, 0);

        // Calculate directions
        Vector3 forwardDir = transform.forward;

        // Outer Angles (e.g., 25 degrees)
        Vector3 outerLeftDir = Quaternion.AngleAxis(-obliqueSensorAngle, Vector3.up) * transform.forward;
        Vector3 outerRightDir = Quaternion.AngleAxis(obliqueSensorAngle, Vector3.up) * transform.forward;

        // Inner Angles (Halfway between center and oblique to fill the blind spot!)
        float innerAngle = obliqueSensorAngle / 2f;
        Vector3 innerLeftDir = Quaternion.AngleAxis(-innerAngle, Vector3.up) * transform.forward;
        Vector3 innerRightDir = Quaternion.AngleAxis(innerAngle, Vector3.up) * transform.forward;

        // --- CHECK FRONT (Now a wall of 3 lasers instead of 1) ---
        bool centerHit = CheckSingleRay(sensorStartPos, forwardDir, frontSensorLength);
        bool innerLeftHit = CheckSingleRay(sensorStartPos, innerLeftDir, frontSensorLength);
        bool innerRightHit = CheckSingleRay(sensorStartPos, innerRightDir, frontSensorLength);

        // If ANY of the 3 front lasers hit something, the center is officially blocked!
        centerBlocked = centerHit || innerLeftHit || innerRightHit;

        // --- CHECK SIDES (The outer lasers for the 7-second failsafe) ---
        bool outerLeftHit = CheckSingleRay(sensorStartPos, outerLeftDir, obliqueSensorLength);
        bool outerRightHit = CheckSingleRay(sensorStartPos, outerRightDir, obliqueSensorLength);

        obliqueBlocked = outerLeftHit || outerRightHit;
    }
    /// <summary>
    /// Helper method to fire a single raycast. Ignores trigger zones.
    /// In normal roads, stops for Cars and Players. 
    /// Inside a CrosswalkZone, ONLY stops for Players and ignores other cars.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            // 1. CHECK FOR THE PLAYER (DEEP SEARCH)
            // We do this first because the Player is the absolute priority.
            bool isPlayer = hit.collider.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player");
            if (isPlayer)
            {
                // ALWAYS trigger the emergency brakes for the player, no matter where the car is!
                Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                return true; 
            }

            // 2. CHECK FOR OTHER CARS
            CarCityMovement otherCar = hit.collider.GetComponentInParent<CarCityMovement>();
            if (otherCar != null && otherCar.gameObject != this.gameObject)
            {
                // If we are in the middle of the intersection (CrosswalkZone), IGNORE the other car!
                if (isInNeverStopZone)
                {
                    continue; // Skip this hit and let the laser keep going through the car
                }
                else
                {
                    // If we are on a normal road, stop for the car ahead normally
                    Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                    return true; 
                }
            }
        }

        // If we hit nothing important, keep the laser green and the path clear
        Debug.DrawRay(startPos, direction * length, Color.green);
        return false; 
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