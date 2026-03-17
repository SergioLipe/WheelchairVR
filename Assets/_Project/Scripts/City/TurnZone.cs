using UnityEngine;

/// <summary>
/// Advanced traffic router. Routes most cars in a default direction, 
/// but forces a specific number of cars to take an alternate route.
/// Needs to be placed on an UNTAGGED trigger collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ConditionalTurnZone : MonoBehaviour
{
    [Header("=== Default Routing ===")]
    [Tooltip("The angle most cars will take. (e.g., 0 = Straight, 90 = Right, -90 = Left)")]
    public float defaultTurnAngle = 0f;

    [Tooltip("How fast the car rotates during the default route.")]
    public float defaultTurnSpeed = 120f;

    [Tooltip("How fast the car moves forward while turning on the default route.")]
    public float defaultSpeedDuringTurn = 3f;

    [Tooltip("How many cars MUST take the Default Route before one car switches?")]
    public int carsTakingDefaultRoute = 3;

    [Header("=== Special Routing ===")]
    [Tooltip("The angle for the special car. (e.g., 90 = Right, -90 = Left)")]
    public float specialTurnAngle = 90f;

    [Tooltip("How fast the special car rotates.")]
    public float specialTurnSpeed = 150f;

    [Tooltip("How fast the special car moves forward while turning.")]
    public float specialSpeedDuringTurn = 2.5f;

    private int carCounter = 0;

    private void OnTriggerEnter(Collider other)
    {
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        if (car != null)
        {
            carCounter++;

            if (carCounter > carsTakingDefaultRoute)
            {
                if (specialTurnAngle != 0f) 
                {
                    car.ForceTurn(specialTurnAngle, specialTurnSpeed, specialSpeedDuringTurn);
                }
                
                carCounter = 0;
            }
            else
            {
                if (defaultTurnAngle != 0f)
                {
                    car.ForceTurn(defaultTurnAngle, defaultTurnSpeed, defaultSpeedDuringTurn);
                }
            }
        }
    }
}