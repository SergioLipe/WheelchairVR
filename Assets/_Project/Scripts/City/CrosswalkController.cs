using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages player waiting areas and car stopping areas.
/// Allows cars that have already entered the crosswalk (Player Zone) to keep moving.
/// </summary>
public class CrosswalkController : MonoBehaviour
{
    [Header("=== Zone Setup ===")]
    [Tooltip("The BoxColliders covering the sidewalks and the crosswalk.")]
    public BoxCollider[] playerZones;

    [Tooltip("The BoxColliders on the roads where the cars must stop.")]
    public BoxCollider[] carZones;

    [Header("=== Advanced Stopping Logic ===")]
    [Tooltip("How far (in meters) the car must be inside the Player Zone to keep going.\n0 = exactly on the edge.\nNegative values (e.g., -1.5) account for the car's front bumper if the pivot is at the rear wheels.")]
    public float crosswalkEntryMargin = 0f;

    // Keeps track of cars we have stopped
    private List<CarCityMovement> yieldingCars = new List<CarCityMovement>();

    void Update()
    {
        if (playerZones == null || carZones == null || playerZones.Length == 0 || carZones.Length == 0)
        {
            return;
        }

        bool playerIsWaiting = CheckForPlayer();
        ControlCars(playerIsWaiting);
    }

    /// <summary>
    /// Scans all player zone colliders to see if the wheelchair is inside any of them.
    /// </summary>
    private bool CheckForPlayer()
    {
        foreach (BoxCollider pZone in playerZones)
        {
            if (pZone == null) continue;

            Vector3 boxCenter = pZone.transform.TransformPoint(pZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(pZone.size, pZone.transform.lossyScale) * 0.5f;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, pZone.transform.rotation);
            
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Scans all car zone colliders and stops cars UNLESS they are already inside the Player Zone.
    /// </summary>
    private void ControlCars(bool shouldStop)
    {
        List<CarCityMovement> carsCurrentlyInZone = new List<CarCityMovement>();

        foreach (BoxCollider cZone in carZones)
        {
            if (cZone == null) continue;

            Vector3 boxCenter = cZone.transform.TransformPoint(cZone.center);
            Vector3 boxHalfExtents = Vector3.Scale(cZone.size, cZone.transform.lossyScale) * 0.5f;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, cZone.transform.rotation);
            
            foreach (Collider hit in hits)
            {
                CarCityMovement car = hit.GetComponentInParent<CarCityMovement>();
                
                if (car != null && !carsCurrentlyInZone.Contains(car))
                {
                    carsCurrentlyInZone.Add(car);

                    bool forceStop = shouldStop;

                    // --- CROSSWALK ENTRY CHECK ---
                    // If the crosswalk is triggered, check if the car is ALREADY inside the crosswalk
                    if (forceStop)
                    {
                        foreach (BoxCollider pZone in playerZones)
                        {
                            if (pZone == null) continue;

                            // Convert car position to the player zone's local space
                            Vector3 localPos = pZone.transform.InverseTransformPoint(car.transform.position);
                            Vector3 extents = pZone.size * 0.5f;

                            // Calculate the effective boundary using your margin
                            float checkX = Mathf.Max(0, extents.x - crosswalkEntryMargin);
                            float checkZ = Mathf.Max(0, extents.z - crosswalkEntryMargin);

                            // If the car is inside this boundary, it has entered the crosswalk!
                            if (Mathf.Abs(localPos.x) <= checkX && 
                                Mathf.Abs(localPos.y) <= extents.y + 2f && 
                                Mathf.Abs(localPos.z) <= checkZ)
                            {
                                forceStop = false; // Let it pass!
                                break; 
                            }
                        }
                    }

                    car.isYielding = forceStop;
                    
                    if (forceStop && !yieldingCars.Contains(car))
                    {
                        yieldingCars.Add(car);
                    }
                }
            }
        }

        // Free cars that left the zone
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
            else if (!pastCar.isYielding)
            {
                yieldingCars.RemoveAt(i);
            }
        }
    }
}