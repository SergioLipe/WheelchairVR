using UnityEngine;

/// <summary>
/// Controls the forward movement of a car.
/// Features:
/// - Stops at specific Stop Zones during red lights.
/// - Uses multiple raycast sensors (front and oblique) to avoid collisions.
/// - Failsafe mechanism: Bypasses indefinitely stopped vehicles AFTER 7 seconds, 
///   BUT ONLY if they are detected by the oblique (side) sensors.
/// - Frontal Block: If a car is directly in front, it will NEVER ignore it.
/// - Crosswalk Override: NEVER stops when inside a designated crosswalk zone.
/// </summary>
public class CarMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    [Tooltip("How fast the car moves forward.")]
    public float speed = 10f;

    [Tooltip("Controlled by the Traffic Light. True = Green Light (Go), False = Red Light (Stop).")]
    public bool canMove = true;

    [Header("=== Zone Settings ===")]
    [Tooltip("The Tag this car looks for to stop at red lights. (e.g., 'StopZone')")]
    public string targetStopZoneTag = "StopZone";

    [Tooltip("The Tag for areas where the car MUST keep moving. (e.g., 'CrosswalkZone')")]
    public string neverStopZoneTag = "CrosswalkZone";

    [Header("=== Collision Sensor Settings ===")]
    [Tooltip("How far the main forward sensor looks ahead (in meters).")]
    public float frontSensorLength = 8f;

    [Tooltip("How far the oblique (angled) sensors look ahead.")]
    public float obliqueSensorLength = 5f;

    [Tooltip("The angle (in degrees) for the oblique sensors to point left and right.")]
    public float obliqueSensorAngle = 25f;

    [Header("=== Stuck Failsafe Settings ===")]
    [Tooltip("How many seconds to wait behind an oblique obstacle before ignoring it.")]
    public float maxWaitTime = 7f;

    // Internal state tracking
    private bool isInStopZone = false;
    private bool isInNeverStopZone = false;
    private float stuckTimer = 0f;
    private bool ignoreObliqueCars = false;

    void Update()
    {
        // 1. Check sensors separately for Front and Oblique obstacles
        bool centerBlocked = false;
        bool obliqueBlocked = false;
        CheckSensors(out centerBlocked, out obliqueBlocked);
        
        // 2. Check legal movement (green light OR outside of a red light stop zone)
        bool wantsToMove = canMove || !isInStopZone;

        // 3. CROSSWALK OVERRIDE (NEVER STOP ZONE)
        if (isInNeverStopZone)
        {
            // Force the car to keep moving, ignore all sensors
            wantsToMove = true;
            centerBlocked = false;
            obliqueBlocked = false;
        }

        // 4. SAFETY SENSOR & FAILSAFE LOGIC
        if (centerBlocked)
        {
            // A car is DIRECTLY in front! We must STOP and NEVER ignore it.
            stuckTimer = 0f; 
            ignoreObliqueCars = false;
            return; // Halt movement immediately
        }
        else if (obliqueBlocked && !ignoreObliqueCars)
        {
            // A car is ONLY on the sides. Apply the 7-second failsafe timer.
            if (wantsToMove)
            {
                stuckTimer += Time.deltaTime;
                
                if (stuckTimer >= maxWaitTime)
                {
                    // Timer reached the limit: ignore the side obstacle and proceed
                    ignoreObliqueCars = true;
                }
            }
            
            // Halt movement while waiting for the timer
            return; 
        }
        else if (!centerBlocked && !obliqueBlocked)
        {
            // The path is completely clear! Reset timer and flags.
            stuckTimer = 0f;
            ignoreObliqueCars = false;
        }

        // 5. APPLY MOVEMENT
        if (wantsToMove)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Checks the front and oblique sensors and outputs their specific states.
    /// </summary>
    private void CheckSensors(out bool centerBlocked, out bool obliqueBlocked)
    {
        Vector3 sensorStartPos = transform.position + new Vector3(0, 0.5f, 0);

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
    /// Helper method to fire a single raycast and check for another CarMovement script.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            CarMovement otherCar = hit.collider.GetComponentInParent<CarMovement>();

            // If we hit a car, and it is NOT ourselves
            if (otherCar != null && otherCar.gameObject != this.gameObject)
            {
                Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                return true;
            }
        }

        Debug.DrawRay(startPos, direction * length, Color.green);
        return false;
    }

    // --- TRIGGER EVENTS FOR ZONES ---

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag)) isInStopZone = true;
        else if (other.CompareTag(neverStopZoneTag)) isInNeverStopZone = true;
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