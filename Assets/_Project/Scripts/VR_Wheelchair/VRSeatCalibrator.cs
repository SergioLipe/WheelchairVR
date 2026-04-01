using UnityEngine;

public class VRSeatCalibrator : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("The root XR Origin object that moves the whole player")]
    public Transform xrOrigin; 
    
    [Tooltip("The Main Camera representing the player's eyes")]
    public Transform headCamera; 
    
    [Tooltip("An empty GameObject placed where you want the player to sit")]
    public Transform seatTarget; 

    [Header("Settings")]
    public bool calibrateOnStart = true;

    private void Start()
    {
        // Add a tiny delay to ensure Unity's XR system has initialized the headset position first
        if (calibrateOnStart)
        {
            Invoke(nameof(RecenterSeat), 0.5f);
        }
    }

    /// <summary>
    /// Moves and rotates the XR Origin so the Head Camera perfectly aligns with the Seat Target
    /// </summary>
    public void RecenterSeat()
    {
        if (xrOrigin == null || headCamera == null || seatTarget == null) return;

        // 1. Align Rotation (Yaw only)
        // We calculate the difference in rotation and rotate the whole XR Origin around the camera's position
        float angleOffset = seatTarget.eulerAngles.y - headCamera.eulerAngles.y;
        xrOrigin.RotateAround(headCamera.position, Vector3.up, angleOffset);

        // 2. Align Position (X and Z only)
        // We find the distance between the target and the camera, and move the Origin by that amount
        Vector3 positionOffset = seatTarget.position - headCamera.position;
        
        // We set Y to 0 because we want to keep the "Tracking Origin: Floor" height physically accurate
        positionOffset.y = 0f; 

        xrOrigin.position += positionOffset;
    }
}