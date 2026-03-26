using UnityEngine;

/// <summary>
/// Controls a pedestrian crosswalk. Detects the Player and activates a Stop Zone to halt traffic.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CrosswalkController : MonoBehaviour
{
    [Header("=== Crosswalk Settings ===")]
    [Tooltip("The invisible collider on the road that stops the cars (Tag should be 'StopZone_NoLight').")]
    public Collider carStopZone;

    private void Start()
    {
        // Ensure the road is clear by default when the game starts
        if (carStopZone != null)
        {
            carStopZone.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is the Player (wheelchair)
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            if (carStopZone != null)
            {
                carStopZone.enabled = true; // Activate the invisible wall to stop cars
                Debug.Log("Player entered the crosswalk. Traffic stopped.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the Player left the crosswalk entirely
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            if (carStopZone != null)
            {
                carStopZone.enabled = false; // Deactivate the wall so cars can move
                Debug.Log("Player left the crosswalk. Traffic resuming.");
            }
        }
    }
}