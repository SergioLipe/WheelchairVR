using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a spawn point for the traffic system.
/// Cars will teleport here when they get lost or stuck for too long.
/// </summary>
public class TrafficSpawnZone : MonoBehaviour
{
    // A shared global list of all spawn zones in the entire game.
    // We store the script itself so cars can read the customCarRotation.
    public static List<TrafficSpawnZone> allSpawnZones = new List<TrafficSpawnZone>();

    [Header("=== Spawn Settings ===")]
    [Tooltip("Type the exact rotation (X, Y, Z) the car should have when spawning here.")]
    public Vector3 customCarRotation = new Vector3(0f, 0f, 0f);

    private void OnEnable()
    {
        // When the game starts (or this zone is enabled), add it to the master list
        if (!allSpawnZones.Contains(this))
        {
            allSpawnZones.Add(this);
        }
    }

    private void OnDisable()
    {
        // If you delete or disable this zone, remove it from the list so cars don't teleport to a dead zone
        allSpawnZones.Remove(this);
    }

    // --- VISUAL GUIDE (EDITOR ONLY) ---
    // This draws helpful shapes in the Unity Editor so you can see your invisible zones
    // and know exactly which way the cars will face when they spawn.
    private void OnDrawGizmos()
    {
        // 1. Draw the green box representing the spawn area
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Transparent Green
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(2.5f, 1f, 5f));

        // 2. Reset matrix to draw the arrow based strictly on your MANUAL rotation
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.cyan; // Cyan color to stand out on the road
        
        // Calculate the forward direction based on the numbers you typed in the Inspector
        Vector3 customForward = Quaternion.Euler(customCarRotation) * Vector3.forward;
        Vector3 arrowTip = transform.position + (customForward * 4f);

        // Draw the main line of the arrow
        Gizmos.DrawRay(transform.position, customForward * 4f);
        
        // Calculate and draw the two lines that make up the arrowhead
        Vector3 right = Quaternion.LookRotation(customForward) * Quaternion.Euler(0, 160, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(customForward) * Quaternion.Euler(0, -160, 0) * Vector3.forward;
        Gizmos.DrawRay(arrowTip, right * 1f);
        Gizmos.DrawRay(arrowTip, left * 1f);
    }
}