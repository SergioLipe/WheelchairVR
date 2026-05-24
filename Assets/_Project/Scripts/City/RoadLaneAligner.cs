using UnityEngine;

/// <summary>
/// Acts as a "Lane Magnet" for straight roads. 
/// Gently pulls cars sideways to the center of the lane.
/// Uses Dot Product filter to ignore cross-traffic at intersections.
/// </summary>
public class RoadLaneAligner : MonoBehaviour
{
    [Tooltip("How strongly the lane pulls the car to the center. Higher = faster snap.")]
    public float alignmentSpeed = 3f;

    [Tooltip("How aligned the car must be to the road to be pulled (0.8 = roughly 36 degrees tolerance).")]
    public float requiredAlignment = 0.8f;

    [Header("=== Optimization ===")]
    [Tooltip("Skip alignment every N FixedUpdates. 1=every, 2=half rate, 3=third")]
    [Range(1, 5)]
    public int updateFrequency = 2;

    // [OPT] Cache de transform
    private Transform myTransform;
    private int frameCounter = 0;

    void Awake()
    {
        myTransform = transform;
    }

    private void OnTriggerStay(Collider other)
    {
        // [OPT] Throttle — não alinha cada FixedUpdate
        frameCounter++;
        if (frameCounter % updateFrequency != 0) return;

        // [OPT] GetComponentInParent is expensive — try GetComponent first
        CarCityMovement car = other.GetComponent<CarCityMovement>();
        if (car == null)
        {
            car = other.GetComponentInParent<CarCityMovement>();
            if (car == null) return;
        }

        // Failsafe reset
        car.timeOffMagnet = 0f;

        if (car.isTurning) return;

        // [OPT] cache transform properties
        Transform carTransform = car.transform;
        Vector3 myForward = myTransform.forward;
        Vector3 carForward = carTransform.forward;

        // Directional filter
        float directionMatch = Vector3.Dot(myForward, carForward);

        if (directionMatch >= requiredAlignment)
        {
            // [OPT] cache car position
            Vector3 carWorldPos = carTransform.position;

            Vector3 localCarPos = myTransform.InverseTransformPoint(carWorldPos);
            
            // [OPT] cache deltaTime — FixedUpdate so we use fixedDeltaTime
            localCarPos.x = Mathf.Lerp(localCarPos.x, 0f, Time.fixedDeltaTime * alignmentSpeed * updateFrequency);

            carTransform.position = myTransform.TransformPoint(localCarPos);
        }
    }
}