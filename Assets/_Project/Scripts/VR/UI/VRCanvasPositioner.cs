using UnityEngine;
using System.Collections;

/// <summary>
/// Positions the VR menu canvas in front of the camera at a comfortable distance and height.
/// 
/// Recommended values for a comfortable VR menu:
/// - Distance: 1.8m (close enough to read, far enough to see the whole panel)
/// - Height offset: -0.15m (slightly below eye level, more natural)
/// 
/// To prevent the menu from appearing at different heights when returning from a level
/// (because the camera Y position differs depending on where you were in the scene),
/// enable "useFixedWorldHeight" and set "fixedWorldHeight" to a comfortable value (e.g. 1.5m).
/// </summary>
public class VRCanvasPositioner : MonoBehaviour
{
    [Header("--- Camera Reference ---")]
    [Tooltip("Drag the Main Camera (XR HMD) here. If empty, will try to find it automatically.")]
    public Transform vrCamera;

    [Header("--- Position Settings ---")]
    [Tooltip("Distance in meters in front of the camera")]
    public float distance = 1.8f;

    [Tooltip("If true, the canvas appears at a FIXED world height (ignoring camera Y). " +
             "Recommended to keep menu always at the same height regardless of player pose. " +
             "If false, uses camera Y + heightOffset (which can vary).")]
    public bool useFixedWorldHeight = true;

    [Tooltip("World Y position for the canvas when useFixedWorldHeight is true. " +
             "1.5m is a comfortable seated/standing average eye level.")]
    public float fixedWorldHeight = 1.5f;

    [Tooltip("Vertical offset from camera eye level. ONLY used when useFixedWorldHeight is false.")]
    public float heightOffset = -0.15f;

    [Tooltip("If true, the canvas keeps its X rotation level (doesn't tilt up/down with head)")]
    public bool keepLevelTilt = true;

    [Header("--- Behavior ---")]
    [Tooltip("If true, position will only be set once (when menu opens). If false, follows the camera.")]
    public bool positionOnceOnEnable = true;

    [Tooltip("Number of frames to wait before positioning, so the VR camera has time to settle. " +
             "Increase if menu still appears in the wrong position when returning from a level.")]
    public int framesToWaitBeforePositioning = 3;

    private void OnEnable()
    {
        EnsureCameraReference();

        if (positionOnceOnEnable)
        {
            StartCoroutine(PositionCanvasDelayed());
        }
    }

    private void Update()
    {
        if (!positionOnceOnEnable)
        {
            EnsureCameraReference();
            if (vrCamera != null)
            {
                PositionCanvas();
            }
        }
    }

    private void EnsureCameraReference()
    {
        if (vrCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                vrCamera = mainCam.transform;
            }
        }
    }

    private IEnumerator PositionCanvasDelayed()
    {
        for (int i = 0; i < framesToWaitBeforePositioning; i++)
        {
            yield return null;
        }

        EnsureCameraReference();
        PositionCanvas();
    }

    private void PositionCanvas()
    {
        if (vrCamera == null) return;

        // Compute the horizontal forward direction (ignoring vertical tilt)
        Vector3 forward = vrCamera.forward;
        if (keepLevelTilt)
        {
            forward.y = 0;
            forward.Normalize();
        }

        // Compute target position
        Vector3 targetPos = vrCamera.position + forward * distance;

        if (useFixedWorldHeight)
        {
            // Override Y with a fixed world height — menu always appears at the same level
            targetPos.y = fixedWorldHeight;
        }
        else
        {
            // Use camera Y + offset (variable depending on player pose)
            targetPos.y += heightOffset;
        }

        transform.position = targetPos;

        // Make canvas face the camera horizontally
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    /// <summary>
    /// Public method to call from outside (e.g., when opening the menu manually,
    /// or from a "Recenter" button in the UI).
    /// Uses a small delay so the camera has time to be in the right place.
    /// </summary>
    public void RecenterCanvas()
    {
        StopAllCoroutines();
        StartCoroutine(PositionCanvasDelayed());
    }
}