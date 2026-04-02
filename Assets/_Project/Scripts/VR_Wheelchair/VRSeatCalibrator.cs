using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class VRSeatCalibrator : MonoBehaviour
{
    // --- VARIABLES ---

    [Header("Core References")]
    [SerializeField] private XROrigin xrOrigin; // The root of the VR player that moves the whole room
    [SerializeField] private Transform headCamera; // The player's actual VR headset position/rotation
    [SerializeField] private Transform seatTarget; // The exact physical spot the player should be sitting

    [Header("Startup Settings")]
    // Exactly how many seconds to wait before auto-clicking recenter. Set to 3 for your 3-second delay.
    [SerializeField] private float startDelay = 3.0f;

    [Header("Input Setup")]
    // The physical controller button (e.g., Right Thumbstick Click) used to trigger manual recentering
    [SerializeField] private InputActionReference recenterAction;

    // --- EVENTS ---

    // Tells the Enforcer script (and any other scripts listening) that we just recentered.
    // This is how the two scripts communicate without directly interfering with each other.
    public System.Action OnCalibrated;

    // --- UNITY LIFECYCLE METHODS ---

    private void OnEnable()
    {
        // When this script is turned on, start listening for the recenter button press
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.Enable();
            recenterAction.action.performed += OnRecenter; // Call 'OnRecenter' when pressed
        }
    }

    private void OnDisable()
    {
        // Stop listening for the button when the script is turned off to prevent errors
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.performed -= OnRecenter;
        }
    }

    private void Start()
    {
        // Print a message so you know the countdown actually started
        Debug.Log($"[VRSeatCalibrator] Timer started! Auto-recentering in {startDelay} seconds...");

        // This literally just calls the Calibrate() function after the exact delay you set.
        // It acts exactly the same as you pressing the physical button, just automatically after X seconds.
        Invoke(nameof(Calibrate), startDelay);
    }

    // --- INPUT CALLBACKS ---

    // This method is triggered whenever the player presses the designated recenter button
    private void OnRecenter(InputAction.CallbackContext ctx)
    {
        Calibrate();
    }

    // --- CORE LOGIC ---

    // The main function that magically snaps the player into the wheelchair
    public void Calibrate()
    {
        // Safety check: if we forgot to drag the objects in the Inspector, stop here to avoid a crash
        if (xrOrigin == null || headCamera == null || seatTarget == null) return;

        // --- 1. Align Rotation ---
        // Calculate the difference in the Y-axis (left/right turning) between where the headset 
        // is looking and where the wheelchair is facing.
        float yawDiff = seatTarget.eulerAngles.y - headCamera.eulerAngles.y;

        // Rotate the entire VR room (XR Origin) AROUND the player's head. 
        // This ensures the player's physical head stays in the same spot while the world spins to align.
        xrOrigin.transform.RotateAround(headCamera.position, Vector3.up, yawDiff);

        // Force Unity's physics engine to instantly update positions after the sudden rotation
        Physics.SyncTransforms();

        // --- 2. Align Position (The "Glue") ---
        // Calculate the exact world distance from the current headset position to the perfect seat position
        Vector3 posDiff = seatTarget.position - headCamera.position;

        // We set Y to 0 because we DO NOT want to change the player's real-world height.
        // The "Tracking Origin Mode: Floor" handles the height. If we didn't zero this out,
        // the player would get buried into the floor or float in the air.
        posDiff.y = 0f;

        // Move the whole world (XR Origin) by that exact distance so the headset lands perfectly on the seat
        xrOrigin.transform.position += posDiff;

        // --- 3. Fire the Event ---
        // Shout out to the Enforcer script: "Hey, I moved the player! Update your boundaries!"
        OnCalibrated?.Invoke();

        // Print a confirmation message in the Unity Console for debugging
        Debug.Log("[VRSeatCalibrator] Snapped perfectly to the Wheelchair Seat!");
    }
}