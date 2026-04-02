using UnityEngine;
using UnityEngine.UI;

public class VRSeatEnforcer : MonoBehaviour
{
    // --- VARIABLES ---
    
    [Header("Core References")]
    [SerializeField] private Transform headCamera; // Represents the player's VR headset
    
    [Tooltip("MUST be the SeatTarget object on the wheelchair!")]
    [SerializeField] private Transform seatCenter; // The perfect center point on the wheelchair
    
    [SerializeField] private Image fadeImage; // The black UI image used to blind the player
    [SerializeField] private VRSeatCalibrator seatCalibrator; // Reference to your calibration script

    [Header("Lean Boundaries (meters)")]
    // These define the invisible "box" around the player
    [SerializeField] private float maxForward = 0.4f;
    [SerializeField] private float maxBack = 0.15f;
    [SerializeField] private float maxSide = 0.35f;
    [SerializeField] private float maxUp = 0.3f;
    [SerializeField] private float maxDown = 0.2f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeSpeed = 10f; // How fast the screen goes black
    [SerializeField] private float fadeStartPercent = 0.7f; // At what % of the limit the screen starts darkening (0.7 = 70%)

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true; // Toggle to print info in the Unity Console

    // Internal state trackers
    private float currentFadeAlpha = 0f; // Current blackness level (0 to 1)
    private Color fadeColor = Color.black; 
    private float calibratedHeadHeight = 0f; // Stores the ideal Y position of the player's head
    private bool heightCalibrated = false; // Checks if the player has been calibrated at least once
    private float debugTimer = 0f; // Limits how often debug messages are printed

    // --- UNITY LIFECYCLE METHODS ---

    private void Awake()
    {
        // When the object wakes up, make sure the fade image is completely transparent
        if (fadeImage != null)
        {
            fadeColor = Color.black;
            fadeColor.a = 0f; // 'a' is Alpha (transparency). 0 is invisible.
            fadeImage.color = fadeColor;
        }
    }

    private void OnEnable()
    {
        // Listen for the Calibrate event so we instantly fix boundaries
        // When the calibrator shouts "I calibrated!", this script runs ResetHeightCalibration()
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated += ResetHeightCalibration;
        }
    }

    private void OnDisable()
    {
        // Stop listening when disabled to prevent memory leaks
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated -= ResetHeightCalibration;
        }
    }

    private void Start()
    {
        // Double-check the image is transparent at the start of the game
        // Also disable raycastTarget so the invisible image doesn't block UI button clicks
        if (fadeImage != null)
        {
            fadeColor.a = 0f;
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;
        }
    }

    private void LateUpdate()
    {
        // Safety check: if missing crucial references, do nothing
        if (headCamera == null || seatCenter == null) return;

        // Instead of checking a boolean, we just wait until the event gives us the height.
        // If not calibrated yet, keep the screen completely clear
        if (!heightCalibrated)
        {
            if (fadeImage != null)
            {
                fadeColor.a = 0f;
                fadeImage.color = fadeColor;
            }
            return;
        }

        // --- POSITION MATH ---
        // Convert the headset's world position into a local position relative to the seat
        // This means Z is always forward/backwards relative to the chair, not the room
        Vector3 localOffset = seatCenter.InverseTransformPoint(headCamera.position);
        
        // Calculate how much higher/lower the head is compared to the perfect calibrated height
        float heightDelta = headCamera.position.y - calibratedHeadHeight;

        // 'violation' tracks the worst rule being broken (0 = no rule broken, 1 = fully outside limit)
        float violation = 0f;

        // Check Front/Back limits (Z axis)
        if (localOffset.z > 0) violation = Mathf.Max(violation, FadeRatio(localOffset.z, maxForward));
        if (localOffset.z < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(localOffset.z), maxBack));
        
        // Check Left/Right limits (X axis) using absolute value (works for both sides)
        violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(localOffset.x), maxSide));
        
        // Check Up/Down limits (Y axis)
        if (heightDelta > 0) violation = Mathf.Max(violation, FadeRatio(heightDelta, maxUp));
        if (heightDelta < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(heightDelta), maxDown));

        // --- DEBUG PRINTING ---
        // Only print every 2 seconds so it doesn't spam and lag the Unity Console
        if (showDebugLogs)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer > 2f)
            {
                debugTimer = 0f;
                Debug.Log($"[Enforcer] fwd/back:{localOffset.z:F2} side:{localOffset.x:F2} " +
                          $"height:{heightDelta:F2} violation:{violation:F2}");
            }
        }

        // --- FADE APPLICATION ---
        // Clamp violation between 0 and 1 just to be safe
        float targetAlpha = Mathf.Clamp01(violation);
        
        // Smoothly transition the current blackness level towards the target level
        currentFadeAlpha = Mathf.Lerp(currentFadeAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // Apply the new color to the UI Image
        if (fadeImage != null)
        {
            fadeColor.a = currentFadeAlpha;
            fadeImage.color = fadeColor;
        }
    }

    // --- HELPER METHODS ---

    // Calculates how far along the "fade zone" the player is.
    // If they are under the start percent (e.g., < 70%), it returns 0.
    // As they move from 70% to 100% of the limit, it returns a value from 0 to 1.
    private float FadeRatio(float distance, float limit)
    {
        float ratio = distance / Mathf.Max(limit, 0.01f); // Prevents dividing by zero
        if (ratio <= fadeStartPercent) return 0f;
        return Mathf.Clamp01((ratio - fadeStartPercent) / (1f - fadeStartPercent));
    }

    // This is called by the VRSeatCalibrator event
    public void ResetHeightCalibration()
    {
        if (headCamera != null)
        {
            // Save the exact current height of the headset
            calibratedHeadHeight = headCamera.position.y;
            heightCalibrated = true;
            
            // Instantly clear the screen alpha so it doesn't stay black after recentering
            currentFadeAlpha = 0f;
            if (fadeImage != null)
            {
                fadeColor.a = 0f;
                fadeImage.color = fadeColor;
            }
            
            Debug.Log($"[VRSeatEnforcer] Limits Reset! Height calibrated at {calibratedHeadHeight}");
        }
    }

    // --- EDITOR VISUALS ---
    
    // This draws the green transparent box and blue arrow in the Unity Scene view
    // It only runs inside the Unity Editor, not in the built game
    private void OnDrawGizmosSelected()
    {
        if (seatCenter == null) return;
        
        // Set the drawing matrix to match the seat's position and rotation
        Gizmos.matrix = seatCenter.localToWorldMatrix;

        // Draw the transparent green box representing the boundaries
        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Vector3 center = new Vector3(0, 0, (maxForward - maxBack) * 0.5f);
        Vector3 size = new Vector3(maxSide * 2f, 0.1f, maxForward + maxBack);
        Gizmos.DrawCube(center, size);
        
        // Draw the solid green outline
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);

        // Draw a blue line indicating the "Forward" direction
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * maxForward);
        
        // Draw a yellow dot at the exact center (0,0,0) of the SeatTarget
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(Vector3.zero, 0.03f);
    }
}