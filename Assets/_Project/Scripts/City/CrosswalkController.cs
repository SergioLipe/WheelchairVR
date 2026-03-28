using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A single controller that manages multiple player waiting areas and multiple car stopping areas.
/// Perfect for multi-lane roads and two-way crosswalks.
/// </summary>
public class CrosswalkController : MonoBehaviour
{
    [Header("=== Zone Setup ===")]
    [Tooltip("The BoxColliders on the sidewalks where the wheelchair waits.")]
    public BoxCollider[] playerZones;

    [Tooltip("The BoxColliders on the roads where the cars must stop.")]
    public BoxCollider[] carZones;

    // Keeps track of cars we have stopped, just in case they slide out of the zone
    private List<CarCityMovement> yieldingCars = new List<CarCityMovement>();

    void Update()
    {
        // Safety check: Don't run if no zones are assigned
        if (playerZones == null || carZones == null || playerZones.Length == 0 || carZones.Length == 0)
        {
            return;
        }

        // 1. Check if the player is currently inside ANY of the Player Zones
        bool playerIsWaiting = CheckForPlayer();

        // 2. Control the cars in ALL Car Zones based on the player's presence
        ControlCars(playerIsWaiting);
    }

    /// <summary>
    /// Scans all player zone colliders to see if the wheelchair is inside any of them.
    /// Uses mathematically accurate Box bounds that respect rotation and scale.
    /// </summary>
    private bool CheckForPlayer()
    {
        foreach (BoxCollider pZone in playerZones)
        {
            if (pZone == null) continue;

            // CORRECT MATH: Calculate exact world center and exact scaled half-extents
            Vector3 boxCenter = pZone.transform.TransformPoint(pZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(pZone.size, pZone.transform.lossyScale) * 0.5f;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, pZone.transform.rotation);
            
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                {
                    // If the player is in ANY zone, return true immediately
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Scans all car zone colliders and updates the yielding state of any cars inside them.
    /// Uses mathematically accurate Box bounds that respect rotation and scale.
    /// </summary>
    private void ControlCars(bool shouldStop)
    {
        List<CarCityMovement> carsCurrentlyInZone = new List<CarCityMovement>();

        // Loop through every car zone you assigned in the inspector
        foreach (BoxCollider cZone in carZones)
        {
            if (cZone == null) continue;

            //Calculate exact world center and exact scaled half-extents
            Vector3 boxCenter = cZone.transform.TransformPoint(cZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(cZone.size, cZone.transform.lossyScale) * 0.5f;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, cZone.transform.rotation);
            
            foreach (Collider hit in hits)
            {
                CarCityMovement car = hit.GetComponentInParent<CarCityMovement>();
                
                // If it's a car, and we haven't already processed it in another zone
                if (car != null && !carsCurrentlyInZone.Contains(car))
                {
                    carsCurrentlyInZone.Add(car);
                    car.isYielding = shouldStop;
                    
                    if (shouldStop && !yieldingCars.Contains(car))
                    {
                        yieldingCars.Add(car);
                    }
                }
            }
        }

        // Free any cars that are no longer in ANY zone so they don't get stuck forever
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