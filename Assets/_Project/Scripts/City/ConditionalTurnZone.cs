using UnityEngine;
using System.Collections.Generic;

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

    // Counter to track how many cars have passed
    private int carCounter = 0;
    
    // --- MEMORY BANK ---
    // A HashSet keeps track of EVERY car currently touching the intersection.
    // This guarantees that a car's rear wheels won't trigger the turn logic a second time.
    private HashSet<CarCityMovement> carsInZone = new HashSet<CarCityMovement>();

    private void OnTriggerEnter(Collider other)
    {
        // Attempt to find the CarCityMovement script on the object that entered the trigger
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        // Proceed ONLY if it is a valid car AND it is NOT already in our memory bank
        if (car != null && !carsInZone.Contains(car))
        {
            // Lock the car into memory so further colliders (like back wheels) are ignored
            carsInZone.Add(car); 
            carCounter++;

            // Check if it's time to send a car on the special route
            if (carCounter > carsTakingDefaultRoute)
            {
                if (specialTurnAngle != 0f) 
                {
                    car.ForceTurn(specialTurnAngle, specialTurnSpeed, specialSpeedDuringTurn);
                }
                
                // Reset the counter after the special car passes
                carCounter = 0;
            }
            else
            {
                // Send the car on the default route
                if (defaultTurnAngle != 0f)
                {
                    car.ForceTurn(defaultTurnAngle, defaultTurnSpeed, defaultSpeedDuringTurn);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // When an object leaves, check if it's a car
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();
        
        // Unity calls OnTriggerExit when ALL colliders of an object have fully left the trigger zone.
        // Once the car is completely out of the intersection, remove it from memory so it can be routed again elsewhere.
        if (car != null && carsInZone.Contains(car))
        {
            carsInZone.Remove(car);
        }
    }
}