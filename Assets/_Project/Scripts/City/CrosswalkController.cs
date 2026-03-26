using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A single controller that manages both the player waiting area and the car stopping area.
/// </summary>
public class CrosswalkController : MonoBehaviour
{
    [Header("=== Zone Setup ===")]
    [Tooltip("The BoxCollider on the sidewalk where the wheelchair waits.")]
    public BoxCollider playerZone;

    [Tooltip("The BoxCollider on the road where the cars must stop.")]
    public BoxCollider carZone;

    // Keeps track of cars we have stopped, just in case they slide out of the zone
    private List<CarCityMovement> yieldingCars = new List<CarCityMovement>();

    void Update()
    {
        if (playerZone == null || carZone == null)
        {
            Debug.LogWarning("CrosswalkController is missing its zone references!");
            return;
        }

        // 1. Check if the player is currently inside the Player Zone
        bool playerIsWaiting = CheckForPlayer();

        // 2. Control the cars in the Car Zone based on the player's presence
        ControlCars(playerIsWaiting);
    }

    /// <summary>
    /// Scans the player zone collider to see if the wheelchair is inside.
    /// </summary>
    private bool CheckForPlayer()
    {
        Collider[] hits = Physics.OverlapBox(playerZone.bounds.center, playerZone.bounds.extents, playerZone.transform.rotation);
        
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Scans the car zone collider and updates the yielding state of any cars inside.
    /// </summary>
    private void ControlCars(bool shouldStop)
    {
        Collider[] hits = Physics.OverlapBox(carZone.bounds.center, carZone.bounds.extents, carZone.transform.rotation);
        
        List<CarCityMovement> carsCurrentlyInZone = new List<CarCityMovement>();

        foreach (Collider hit in hits)
        {
            CarCityMovement car = hit.GetComponentInParent<CarCityMovement>();
            if (car != null)
            {
                carsCurrentlyInZone.Add(car);
                car.isYielding = shouldStop;
                
                if (shouldStop && !yieldingCars.Contains(car))
                {
                    yieldingCars.Add(car);
                }
            }
        }

        // Free any cars that are no longer in the zone so they don't get stuck forever
        for (int i = yieldingCars.Count - 1; i >= 0; i--)
        {
            CarCityMovement pastCar = yieldingCars[i];
            
            if (pastCar == null) 
            {
                yieldingCars.RemoveAt(i);
                continue;
            }

            if (!carsCurrentlyInZone.Contains(pastCar))
            {
                pastCar.isYielding = false;
                yieldingCars.RemoveAt(i);
            }
        }
    }
}