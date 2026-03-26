using UnityEngine;

/// <summary>
/// Acts as a "Lane Magnet" for straight roads. 
/// Gently pulls cars sideways to the exact center line of the lane.
/// Uses a Dot Product filter to ignore cross-traffic at intersections.
/// </summary>
public class RoadLaneAligner : MonoBehaviour
{
    [Tooltip("How strongly the lane pulls the car to the center. Higher = faster snap.")]
    public float alignmentSpeed = 3f;

    [Tooltip("How aligned the car must be to the road to be pulled (0.8 = roughly 36 degrees tolerance).")]
    public float requiredAlignment = 0.8f;

    private void OnTriggerStay(Collider other)
    {
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        // Check if it's a car, and ensure it's not currently in the middle of a forced turn
        if (car != null && !car.isTurning) 
        {
    
            // Compare the magnet's forward direction (Z-axis) with the car's forward direction.
            float directionMatch = Vector3.Dot(transform.forward, car.transform.forward);

            // Only pull the car if it is driving in the same general direction as the magnet (directionMatch > 0.8).
            // Cross-traffic will have a directionMatch near 0, so they will be completely ignored!
            if (directionMatch >= requiredAlignment)
            {
                // 1. Convert the car's world position into the road's local space
                Vector3 localCarPos = transform.InverseTransformPoint(car.transform.position);

                // 2. Smoothly pull the X position to 0 (the exact center of the road)
                localCarPos.x = Mathf.Lerp(localCarPos.x, 0f, Time.deltaTime * alignmentSpeed);

                // 3. Convert back to world space and apply
                car.transform.position = transform.TransformPoint(localCarPos);
            }
        }
    }
}