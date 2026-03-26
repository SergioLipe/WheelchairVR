using UnityEngine;

/// <summary>
/// Controls city traffic with straight-line driving and curve-turning at intersections.
/// Features: Stop Zones, NonStop Zones, Player Detection, Failsafes, and External Trigger-based Turning.
/// </summary>
public class CarCityMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    [Tooltip("How fast the car moves forward on a straight road.")]
    public float speed = 10f;

    [Tooltip("Controlled by the Traffic Light. True = Green Light (Go), False = Red Light (Stop).")]
    public bool canMove = true;

    [Header("=== Zone Settings ===")]
    [Tooltip("The Tag this car looks for to stop at red lights.")]
    public string targetStopZoneTag = "StopZone";

    [Tooltip("The Tag for pedestrian crosswalks without traffic lights.")]
    public string crosswalkStopZoneTag = "StopZone_NoLight";

    [Tooltip("The Tag for areas where the car MUST NOT stop for red lights, like the middle of an intersection.")]
    public string neverStopZoneTag = "NonStopZone";

    [Header("=== Collision Sensor Settings ===")]
    [Tooltip("Pushes the sensor origin forward to the front bumper so the car doesn't overlap before stopping.")]
    public float sensorFrontOffset = 1.8f;
    [Tooltip("How far the main forward sensor looks ahead (in meters).")]
    public float frontSensorLength = 6f;

    [Tooltip("How far the oblique (angled) sensors look ahead.")]
    public float obliqueSensorLength = 5f;

    [Tooltip("The angle (in degrees) for the oblique sensors to point left and right.")]
    public float obliqueSensorAngle = 25f;

    [Header("=== Stuck Failsafe Settings ===")]
    [Tooltip("How many seconds to wait behind a side obstacle before ignoring it.")]



    public float maxWaitTime = 7f;

    // --- Internal State Tracking ---
    private bool isInStopZone = false;
    private bool isInNeverStopZone = false;
    private float stuckTimer = 0f;
    private bool ignoreObliqueCars = false;

    // --- Turning Logic Tracking ---
    private bool isTurning = false;
    private float degreesTurned = 0f;
    private float currentTargetAngle = 0f;
    private float turnDirection = 1f;

    // Variables provided by the external ConditionalTurnZone
    private float currentTurnSpeed = 120f;
    private float currentSpeedDuringTurn = 3f;

    void Update()
    {
        // 1. Check sensors separately for Front and Oblique obstacles
        bool centerBlocked;
        bool obliqueBlocked;
        CheckSensors(out centerBlocked, out obliqueBlocked);

        // 2. Check legal movement (green light OR outside of a red light stop zone)
        bool wantsToMove = canMove || !isInStopZone;

        // 3. NON-STOP ZONE OVERRIDE
        if (isInNeverStopZone)
        {
            // Force the car to ignore Red Lights to clear the intersection.
            // Sensors remain active to prevent hitting the player.
            wantsToMove = true;
        }

        // 4. SAFETY SENSOR & FAILSAFE LOGIC
        if (centerBlocked)
        {
            // A car or player is directly in front. Halt movement immediately.
            stuckTimer = 0f;
            ignoreObliqueCars = false;
            return;
        }
        else if (obliqueBlocked && !ignoreObliqueCars)
        {
            // Obstacle only on the sides. Apply the failsafe timer.
            if (wantsToMove)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= maxWaitTime)
                {
                    // Timer reached: ignore the side obstacle so traffic can flow.
                    ignoreObliqueCars = true;
                }
            }
            return;
        }
        else if (!centerBlocked && !obliqueBlocked)
        {
            // The path is clear. Reset timer and flags.
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
    /// Handles driving straight and smoothly curving when forced to turn.
    /// Uses a slower forward speed during the turn to ensure the curve is tight enough.
    /// </summary>
    private void ApplyMovementAndTurning()
    {
        float currentForwardSpeed = speed;

        if (isTurning)
        {
            currentForwardSpeed = currentSpeedDuringTurn;

            float step = currentTurnSpeed * Time.deltaTime;

            // Prevent the car from over-turning past the exact target angle
            if (degreesTurned + step >= currentTargetAngle)
            {
                step = currentTargetAngle - degreesTurned;
                isTurning = false;
            }

            // Apply rotation 
            transform.Rotate(Vector3.up, step * turnDirection);
            degreesTurned += step;
        }

        // Apply forward movement
        transform.Translate(Vector3.forward * currentForwardSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Checks the front and oblique sensors and outputs their specific states.
    /// Uses a 5-Raycast Fan system to eliminate blind spots.
    /// </summary>
    private void CheckSensors(out bool centerBlocked, out bool obliqueBlocked)
    {
        // Slightly elevate the sensor so it doesn't hit the physical road
        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + new Vector3(0, 0.5f, 0);

        Vector3 forwardDir = transform.forward;

        // Outer Angles
        Vector3 outerLeftDir = Quaternion.AngleAxis(-obliqueSensorAngle, Vector3.up) * transform.forward;
        Vector3 outerRightDir = Quaternion.AngleAxis(obliqueSensorAngle, Vector3.up) * transform.forward;

        // Inner Angles 
        float innerAngle = obliqueSensorAngle / 2f;
        Vector3 innerLeftDir = Quaternion.AngleAxis(-innerAngle, Vector3.up) * transform.forward;
        Vector3 innerRightDir = Quaternion.AngleAxis(innerAngle, Vector3.up) * transform.forward;

        // Check Front (Wall of 3 lasers)
        bool centerHit = CheckSingleRay(sensorStartPos, forwardDir, frontSensorLength);
        bool innerLeftHit = CheckSingleRay(sensorStartPos, innerLeftDir, frontSensorLength);
        bool innerRightHit = CheckSingleRay(sensorStartPos, innerRightDir, frontSensorLength);

        // If ANY of the 3 front lasers hit something, the center is blocked
        centerBlocked = centerHit || innerLeftHit || innerRightHit;

        // Check Sides (Outer lasers for the failsafe)
        bool outerLeftHit = CheckSingleRay(sensorStartPos, outerLeftDir, obliqueSensorLength);
        bool outerRightHit = CheckSingleRay(sensorStartPos, outerRightDir, obliqueSensorLength);

        obliqueBlocked = outerLeftHit || outerRightHit;
    }

    /// <summary>
    /// Helper method to fire a single raycast. Ignores trigger zones.
    /// Stops for Players globally. Inside a NonStopZone, ignores other cars.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            // 1. CHECK FOR THE PLAYER
            bool isPlayer = hit.collider.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player");
            if (isPlayer)
            {
                Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                return true;
            }

            // 2. CHECK FOR OTHER CARS
            CarCityMovement otherCar = hit.collider.GetComponentInParent<CarCityMovement>();
            if (otherCar != null && otherCar.gameObject != this.gameObject)
            {
                if (isInNeverStopZone)
                {
                    // Ignore cars while in an intersection
                    continue;
                }
                else
                {
                    // Stop for cars on regular roads
                    Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                    return true;
                }
            }
        }

        Debug.DrawRay(startPos, direction * length, Color.green);
        return false;
    }

    // --- TRIGGER EVENTS FOR ZONES ---

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag) || other.CompareTag(crosswalkStopZoneTag))
        {
            isInStopZone = true;
        }
        else if (other.CompareTag(neverStopZoneTag))
        {
            isInNeverStopZone = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag) || other.CompareTag(crosswalkStopZoneTag)) isInStopZone = true;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag) || other.CompareTag(crosswalkStopZoneTag)) isInStopZone = false;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = false;
    }

    /// <summary>
    /// Forces the car to initiate a turning sequence using parameters provided by an external zone.
    /// </summary>
    public void ForceTurn(float customAngle, float customTurnSpeed, float customSpeedDuringTurn)
    {
        if (!isTurning)
        {
            isTurning = true;
            degreesTurned = 0f;
            currentTargetAngle = Mathf.Abs(customAngle);
            turnDirection = Mathf.Sign(customAngle);

            currentTurnSpeed = customTurnSpeed;
            currentSpeedDuringTurn = customSpeedDuringTurn;
        }
    }
}