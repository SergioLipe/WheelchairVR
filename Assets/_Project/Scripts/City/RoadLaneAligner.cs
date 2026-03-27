using UnityEngine;

/// <summary>
/// Acts as a "Lane Magnet" for straight roads. 
/// Gently pulls cars sideways to the exact center line of the lane to fix positional drift.
/// Uses a Dot Product filter to ignore cross-traffic at intersections.
/// Also resets the car's teleport timer so the traffic system knows the car is safely on a road.
/// </summary>
public class RoadLaneAligner : MonoBehaviour
{
    [Tooltip("How strongly the lane pulls the car to the center. Higher = faster snap.")]
    public float alignmentSpeed = 3f;

    [Tooltip("How aligned the car must be to the road to be pulled (0.8 = roughly 36 degrees tolerance).")]
    public float requiredAlignment = 0.8f;

    private void OnTriggerStay(Collider other)
    {
        // Attempt to find the CarCityMovement script on the object that entered the trigger
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        // Proceed ONLY if it is a valid car
        if (car != null) 
        {
            // --- FAILSAFE RESET ---
            // Tell the car it is safely inside a lane, preventing the 5-second teleport failsafe from triggering
            car.timeOffMagnet = 0f;

            // We DO NOT align them if they are in the middle of a forced intersection turn
            if (!car.isTurning)
            {
                // --- DIRECTIONAL FILTER (Cross-Traffic Protection) ---
                // Compare the magnet's forward direction (Z-axis) with the car's forward direction.
                // This returns 1 if parallel, 0 if perpendicular (crossing), and -1 if backwards.
                float directionMatch = Vector3.Dot(transform.forward, car.transform.forward);

                // Only pull the car if it is driving in the same general direction as the magnet.
                // Cross-traffic will have a directionMatch near 0, so they will be completely ignored!
                if (directionMatch >= requiredAlignment)
                {
                    // 1. Convert the car's world position into the road's local space
                    Vector3 localCarPos = transform.InverseTransformPoint(car.transform.position);

                    // 2. In local space, X is left/right. We smoothly Lerp the X position to 0 (the exact center of the road)
                    localCarPos.x = Mathf.Lerp(localCarPos.x, 0f, Time.deltaTime * alignmentSpeed);

                    // 3. Convert the position back to world space and apply it to the car
                    car.transform.position = transform.TransformPoint(localCarPos);
                }
            }
        }
    }
}