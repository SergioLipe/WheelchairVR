using UnityEngine;

/// <summary>
/// Positions the VR menu canvas in front of the camera at a comfortable distance and height.
/// Run once at Start (or when the menu opens).
/// 
/// Recommended values for a comfortable VR menu:
/// - Distance: 1.8m (close enough to read, far enough to see the whole panel)
/// - Height offset: -0.15m (slightly below eye level, more natural)
/// </summary>
public class VRCanvasPositioner : MonoBehaviour
{
    [Header("--- Camera Reference ---")]
    [Tooltip("Drag the Main Camera (XR HMD) here. If empty, will try to find it automatically.")]
    public Transform vrCamera;

    [Header("--- Position Settings ---")]
    [Tooltip("Distance in meters in front of the camera")]
    public float distance = 1.8f;

    [Tooltip("Vertical offset from eye level (0 = eye level, negative = below)")]
    public float heightOffset = -0.15f;

    [Tooltip("If true, the canvas keeps its X rotation level (doesn't tilt up/down with head)")]
    public bool keepLevelTilt = true;

    [Header("--- Behavior ---")]
    [Tooltip("If true, position will only be set once (when menu opens). If false, follows the camera.")]
    public bool positionOnceOnEnable = true;

    private void OnEnable()
    {
        // Auto-find camera if not set
        if (vrCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) vrCamera = mainCam.transform;
        }

        if (positionOnceOnEnable)
        {
            PositionCanvas();
        }
    }

    private void Update()
    {
        if (!positionOnceOnEnable && vrCamera != null)
        {
            PositionCanvas();
        }
    }

    private void PositionCanvas()
    {
        if (vrCamera == null) return;

        // Get forward direction, optionally ignoring vertical tilt
        Vector3 forward = vrCamera.forward;
        if (keepLevelTilt)
        {
            forward.y = 0;
            forward.Normalize();
        }

        // Position the canvas
        Vector3 targetPos = vrCamera.position + forward * distance;
        targetPos.y += heightOffset;
        transform.position = targetPos;

        // Make canvas face the camera
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    /// <summary>
    /// Public method to call from outside (e.g., when opening the menu manually,
    /// or from a "Recenter" button in the UI).
    /// </summary>
    public void RecenterCanvas()
    {
        PositionCanvas();
    }
}