using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Switches between two completely separate camera systems: PC and VR.
/// </summary>
public class CameraModeManager : MonoBehaviour
{
    [Header("=== Camera Systems ===")]
    [Tooltip("Drag the CameraPC parent object here")]
    public GameObject pcCameraRoot;

    [Tooltip("Drag the XR Origin (VR) object here")]
    public GameObject vrCameraRoot;

    void Start()
    {
        UpdateCameraMode();
    }

    void Update()
    {
        UpdateCameraMode();
    }

    private void UpdateCameraMode()
    {
        // Check if the VR headset is connected and active on the user's head
        bool isVRActive = XRSettings.isDeviceActive;

        if (isVRActive)
        {
            // VR is ON: Activate XR Origin, deactivate PC Camera
            if (vrCameraRoot != null && !vrCameraRoot.activeSelf) 
            {
                vrCameraRoot.SetActive(true);
            }
            if (pcCameraRoot != null && pcCameraRoot.activeSelf) 
            {
                pcCameraRoot.SetActive(false);
            }
            
            // Unlock mouse just in case VR needs to interact with UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // VR is OFF/Sleeping: Activate PC Camera, deactivate XR Origin
            if (pcCameraRoot != null && !pcCameraRoot.activeSelf) 
            {
                pcCameraRoot.SetActive(true);
            }
            if (vrCameraRoot != null && vrCameraRoot.activeSelf) 
            {
                vrCameraRoot.SetActive(false);
            }
            
            // Lock mouse for PC free look mode
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}