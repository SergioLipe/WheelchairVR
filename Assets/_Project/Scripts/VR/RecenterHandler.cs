using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// Allows the user to recenter the VR view by pressing a button.
/// Required by Meta for VRC compliance.
/// </summary>
public class RecenterHandler : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("XR Origin in the scene (usually inside the wheelchair)")]
    public XROrigin xrOrigin;

    [Header("=== Input ===")]
    [Tooltip("Input action to trigger recenter (e.g. Right controller Menu/B button)")]
    public InputActionReference recenterAction;

    [Header("=== Settings ===")]
    [Tooltip("Should we keep Y position (height) when recentering?")]
    public bool keepHeight = true;

    [Tooltip("Should we recenter rotation as well? (yaw only)")]
    public bool recenterRotation = true;

    [Header("=== Optional Feedback ===")]
    [Tooltip("Sound to play on recenter")]
    public AudioSource feedbackAudio;
    public AudioClip recenterSound;

    private void OnEnable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.Enable();
            recenterAction.action.performed += OnRecenterPressed;
        }
    }

    private void OnDisable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.performed -= OnRecenterPressed;
        }
    }

    private void OnRecenterPressed(InputAction.CallbackContext ctx)
    {
        Recenter();
    }

    public void Recenter()
    {
        if (xrOrigin == null)
        {
            Debug.LogWarning("[RecenterHandler] No XROrigin assigned!");
            return;
        }

        if (xrOrigin.Camera == null)
        {
            Debug.LogWarning("[RecenterHandler] XROrigin has no Camera!");
            return;
        }

        Transform cameraTransform = xrOrigin.Camera.transform;

        // [FIX] Recenter position — move XR Origin so camera is at desired position
        Vector3 cameraOffset = cameraTransform.position - xrOrigin.transform.position;
        if (!keepHeight) cameraOffset.y = 0f;
        else cameraOffset.y = 0f; // never move vertical with recenter

        xrOrigin.transform.position -= new Vector3(cameraOffset.x, 0f, cameraOffset.z);

        // Recenter rotation (yaw only)
        if (recenterRotation)
        {
            float cameraYaw = cameraTransform.eulerAngles.y;
            float originYaw = xrOrigin.transform.eulerAngles.y;
            float yawDelta = cameraYaw - originYaw;
            xrOrigin.transform.Rotate(0f, -yawDelta, 0f);
        }

        // Feedback
        if (feedbackAudio != null && recenterSound != null)
        {
            feedbackAudio.PlayOneShot(recenterSound);
        }

        Debug.Log("[RecenterHandler] View recentered");
    }
}