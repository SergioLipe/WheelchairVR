using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls an invisible Stop Zone acting as a traffic light for cars.
/// Automatically switches between VR and PC timings based on which wheelchair is active.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrafficLightController : MonoBehaviour
{
    [Header("=== Mode Detection ===")]
    [Tooltip("Drag the Wheelchair_VR here. If it's active, it uses VR timings. If not, it uses PC timings.")]
    public GameObject vrWheelchair;

    [Header("=== VR Timings ===")]
    public float vrInitialGreen = 10f;
    public float vrInitialRed = 10f;
    public float vrNormalGreen = 10f;
    public float vrNormalRed = 10f;

    [Header("=== PC Timings ===")]
    public float pcInitialGreen = 5f;
    public float pcInitialRed = 5f;
    public float pcNormalGreen = 5f;
    public float pcNormalRed = 5f;

    [Header("=== Current State ===")]
    [Tooltip("Check this if you want the lane to start Green when the game plays.")]
    public bool isGreen = true;

    // --- Active Timings (chosen when the game starts) ---
    private float activeInitialGreen;
    private float activeInitialRed;
    private float activeNormalGreen;
    private float activeNormalRed;

    // --- Internal Timers and State Tracking ---
    private float timer = 0f;
    private bool isFirstGreen = true;
    private bool isFirstRed = true;

    // List to remember which cars are currently waiting inside this specific Stop Zone
    private List<CarCityMovement> carsInZone = new List<CarCityMovement>();

    void Start()
    {
        // Check if VR Wheelchair exists and is turned on in the Hierarchy
        if (vrWheelchair != null && vrWheelchair.activeInHierarchy)
        {
            // Apply VR Timings
            activeInitialGreen = vrInitialGreen;
            activeInitialRed = vrInitialRed;
            activeNormalGreen = vrNormalGreen;
            activeNormalRed = vrNormalRed;
        }
        else
        {
            // Apply PC Timings
            activeInitialGreen = pcInitialGreen;
            activeInitialRed = pcInitialRed;
            activeNormalGreen = pcNormalGreen;
            activeNormalRed = pcNormalRed;
        }
    }

    void Update()
    {
        // 1. Advance the timer
        timer += Time.deltaTime;

        // 2. Determine the current time limit based on the state and whether it's the first cycle
        float currentLimit;
        if (isGreen)
        {
            currentLimit = isFirstGreen ? activeInitialGreen : activeNormalGreen;
        }
        else
        {
            currentLimit = isFirstRed ? activeInitialRed : activeNormalRed;
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