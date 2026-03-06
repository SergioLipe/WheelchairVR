using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls an invisible Stop Zone acting as a traffic light for cars.
/// It automatically toggles the "canMove" variable of any CarMovement scripts inside it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrafficLightController : MonoBehaviour
{
    [Header("=== Traffic Light Timers ===")]
    [Tooltip("How long the invisible light stays green (cars can go).")]
    public float greenLightDuration = 10f;
    
    [Tooltip("How long the invisible light stays red (cars must stop).")]
    public float redLightDuration = 10f;

    [Header("=== Current State ===")]
    [Tooltip("Check this if you want the lane to start Green when the game plays.")]
    public bool isGreen = true;

    // Internal timer to track light changes
    private float timer = 0f;

    // List to remember which cars are currently waiting inside this specific Stop Zone
    private List<CarMovement> carsInZone = new List<CarMovement>();

    void Update()
    {
        // 1. Advance the timer
        timer += Time.deltaTime;
        
        // 2. Check which duration limit we are currently using
        float currentLimit = isGreen ? greenLightDuration : redLightDuration;

        // 3. Swap the light state if the timer reaches the limit
        if (timer >= currentLimit)
        {
            timer = 0f;
            isGreen = !isGreen; // Toggle between true/false
            
            // Immediately update all cars waiting in the zone
            UpdateWaitingCars();
        }
    }

    /// <summary>
    /// When a car enters the Stop Zone, add it to our list and tell it the current state.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        CarMovement car = other.GetComponentInParent<CarMovement>();
        
        if (car != null)
        {
            if (!carsInZone.Contains(car))
            {
                carsInZone.Add(car);
            }
            
            // Tell the car if it can keep moving or if it must stop
            car.canMove = isGreen;
        }
    }

    /// <summary>
    /// When a car successfully leaves the Stop Zone, remove it from the list.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        CarMovement car = other.GetComponentInParent<CarMovement>();
        
        if (car != null)
        {
            if (carsInZone.Contains(car))
            {
                carsInZone.Remove(car);
            }
            
            // Once the car leaves the intersection entirely, it is free to drive normally
            car.canMove = true; 
        }
    }

    /// <summary>
    /// Applies the current traffic light state to all cars currently stopped in the zone.
    /// </summary>
    private void UpdateWaitingCars()
    {
        // Safety check: Remove any cars from the list that might have been deleted/destroyed
        carsInZone.RemoveAll(item => item == null);

        // Update the 'canMove' boolean for every car waiting
        foreach (CarMovement car in carsInZone)
        {
            car.canMove = isGreen;
        }
    }
}