using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls an invisible Stop Zone acting as a traffic light for cars.
/// It automatically toggles the "canMove" variable of any CarCityMovement scripts inside it.
/// Allows for a unique duration on the very first light cycle to sync intersections perfectly.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrafficLightController : MonoBehaviour
{
    [Header("=== Initial Timing Settings ===")]
    [Tooltip("How long the invisible light stays green on the VERY FIRST cycle.")]
    public float initialGreenDuration = 10f;

    [Tooltip("How long the invisible light stays red on the VERY FIRST cycle.")]
    public float initialRedDuration = 10f;

    [Header("=== Normal Loop Timing Settings ===")]
    [Tooltip("How long the invisible light stays green (cars can go).")]
    public float greenLightDuration = 10f;

    [Tooltip("How long the invisible light stays red (cars must stop).")]
    public float redLightDuration = 10f;

    [Header("=== Current State ===")]
    [Tooltip("Check this if you want the lane to start Green when the game plays.")]
    public bool isGreen = true;

    // --- Internal Timers and State Tracking ---
    private float timer = 0f;
    private bool isFirstGreen = true;
    private bool isFirstRed = true;

    // List to remember which cars are currently waiting inside this specific Stop Zone
    private List<CarCityMovement> carsInZone = new List<CarCityMovement>();

    void Update()
    {
        // 1. Advance the timer
        timer += Time.deltaTime;

        // 2. Determine the current time limit based on the state and whether it's the first cycle
        float currentLimit;
        if (isGreen)
        {
            currentLimit = isFirstGreen ? initialGreenDuration : greenLightDuration;
        }
        else
        {
            currentLimit = isFirstRed ? initialRedDuration : redLightDuration;
        }

        // 3. Swap the light state if the timer reaches the limit
        if (timer >= currentLimit)
        {
            // Reset the timer for the next phase
            timer = 0f;

            // Mark the current initial phase as completed so it uses normal durations next time
            if (isGreen) 
            {
                isFirstGreen = false;
            }
            else 
            {
                isFirstRed = false;
            }

            // Toggle between true and false (Green to Red, or Red to Green)
            isGreen = !isGreen;

            // Immediately update all cars that are waiting in the zone
            UpdateWaitingCars();
        }
    }

    /// <summary>
    /// When a car enters the Stop Zone, add it to our list and tell it the current state.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Look for the CarCityMovement script on the object that entered the trigger
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        if (car != null)
        {
            // If the car isn't already in our list, add it
            if (!carsInZone.Contains(car))
            {
                carsInZone.Add(car);
            }

            // Tell the car if it can keep moving (Green) or if it must stop (Red)
            car.canMove = isGreen;
        }
    }

    /// <summary>
    /// When a car successfully leaves the Stop Zone, remove it from the list.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        CarCityMovement car = other.GetComponentInParent<CarCityMovement>();

        if (car != null)
        {
            // Remove the car from our waiting list
            if (carsInZone.Contains(car))
            {
                carsInZone.Remove(car);
            }

            // Once the car leaves the intersection entirely, it is free to drive normally again
            car.canMove = true;
        }
    }

    /// <summary>
    /// Applies the current traffic light state to all cars currently stopped in the zone.
    /// </summary>
    private void UpdateWaitingCars()
    {
        // Safety check: Remove any cars from the list that might have been deleted/destroyed 
        // (e.g., if a car fell off the map while waiting at a red light)
        carsInZone.RemoveAll(item => item == null);

        // Update the 'canMove' boolean for every car waiting in this intersection
        foreach (CarCityMovement car in carsInZone)
        {
            car.canMove = isGreen;
        }
    }
}