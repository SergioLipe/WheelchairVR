using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Snaps the VR player into the wheelchair seat position with correct orientation.
/// Auto-calibrates on Start with a delay, and supports manual recentering via button.
/// Meta VRC compliant (recenter feature required by Meta Store).
/// </summary>
public class VRSeatCalibrator : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform seatTarget;

    [Header("Startup Settings")]
    [Tooltip("Seconds to wait before auto-calibrating on scene start")]
    [SerializeField] private float startDelay = 3.0f;

    [Header("Input Setup")]
    [Tooltip("Button to manually trigger recenter (e.g. Right Thumbstick Click or B button)")]
    [SerializeField] private InputActionReference recenterAction;

    [Header("Anti-Spam")]
    [Tooltip("Minimum time between recenter actions (seconds)")]
    [SerializeField] private float recenterCooldown = 0.5f;

    [Header("Feedback (Optional)")]
    [Tooltip("Audio source to play recenter feedback")]
    [SerializeField] private AudioSource feedbackAudio;
    [Tooltip("Sound to play when recentered")]
    [SerializeField] private AudioClip recenterSound;

    [Header("Haptic Feedback (Optional)")]
    [SerializeField] private InputActionReference leftHapticAction;
    [SerializeField] private InputActionReference rightHapticAction;
    [Range(0f, 1f)]
    [SerializeField] private float hapticIntensity = 0.2f;
    [SerializeField] private float hapticDuration = 0.1f;

    // Event that fires when recentered (other scripts can subscribe)
    public System.Action OnCalibrated;

    private float lastRecenterTime = -999f;

    private void OnEnable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.Enable();
            recenterAction.action.performed += OnRecenter;
        }
    }

    private void OnDisable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.performed -= OnRecenter;
        }
    }

   private void Start()
    {
        // 1. Immediate snap right when the scene/countdown starts
        Debug.Log("[VRSeatCalibrator] Immediate calibration for countdown start!");
        Calibrate();

        // 2. Delayed snap (kept as requested to ensure tracking is stable)
        Debug.Log($"[VRSeatCalibrator] Auto-calibrating again in {startDelay} seconds...");
        Invoke(nameof(Calibrate), startDelay);
    }

    private void OnRecenter(InputAction.CallbackContext ctx)
    {
        Calibrate();
    }

    public void Calibrate()
    {
        // Anti-spam protection
        if (Time.time - lastRecenterTime < recenterCooldown) return;
        lastRecenterTime = Time.time;

        // Safety check
        if (xrOrigin == null || headCamera == null || seatTarget == null)
        {
            Debug.LogWarning("[VRSeatCalibrator] Missing references — cannot calibrate.");
            return;
        }

        // 1. Align Rotation
        float yawDiff = seatTarget.eulerAngles.y - headCamera.eulerAngles.y;
        xrOrigin.transform.RotateAround(headCamera.position, Vector3.up, yawDiff);
        Physics.SyncTransforms();

        // 2. Align Position
        Vector3 posDiff = seatTarget.position - headCamera.position;
        xrOrigin.transform.position += posDiff;

        // 3. Feedback
        PlayRecenterFeedback();

        // 4. Fire event
        OnCalibrated?.Invoke();

        Debug.Log("[VRSeatCalibrator] Snapped perfectly to the Wheelchair Seat!");
    }

    private void PlayRecenterFeedback()
    {
        // Audio
        if (feedbackAudio != null && recenterSound != null)
        {
            feedbackAudio.PlayOneShot(recenterSound);
        }

        // Haptics
        SendHaptic(leftHapticAction);
        SendHaptic(rightHapticAction);
    }

    private void SendHaptic(InputActionReference hapticRef)
    {
        if (hapticRef == null || hapticRef.action == null) return;
        foreach (var control in hapticRef.action.controls)
        {
            if (control.device is XRControllerWithRumble rumble)
            {
                rumble.SendImpulse(hapticIntensity, hapticDuration);
                return;
            }
        }
    }
}