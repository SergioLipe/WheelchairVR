using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages switching between completely separate PC and VR player rigs.
/// Must be placed on an independent GameObject (like GameManager), NOT inside the rigs.
/// </summary>
public class PlayerRigManager : MonoBehaviour
{
    [Header("=== Player Rigs ===")]
    [Tooltip("Drag the Wheelchair_PC object here")]
    public GameObject wheelchairPC;

    [Tooltip("Drag the Wheelchair_VR object here")]
    public GameObject wheelchairVR;

    /// <summary>
    /// Helper: returns true if we are currently in the Main Menu scene.
    /// In that case, we must NOT lock/hide the cursor (player needs it for the UI).
    /// </summary>
    private bool IsInMainMenu()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "MainMenu" || sceneName.Contains("Menu");
    }

    void Start()
    {
        UpdateRigMode();
    }

    void Update()
    {
        // Continuously check if VR headset is put on or taken off
        UpdateRigMode();
    }

    private void UpdateRigMode()
    {
        bool isVRActive = XRSettings.isDeviceActive;
        if (isVRActive)
        {
            // VR is ON: Activate VR Rig, deactivate PC Rig
            if (wheelchairVR != null && !wheelchairVR.activeSelf)
            {
                wheelchairVR.SetActive(true);
            }
            if (wheelchairPC != null && wheelchairPC.activeSelf)
            {
                wheelchairPC.SetActive(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // VR is OFF: Activate PC Rig, deactivate VR Rig
            if (wheelchairPC != null && !wheelchairPC.activeSelf)
            {
                wheelchairPC.SetActive(true);
            }
            if (wheelchairVR != null && wheelchairVR.activeSelf)
            {
                wheelchairVR.SetActive(false);
            }

            // CRITICAL FIX: Only lock the cursor during gameplay, never in the Main Menu.
            // In the menu the player needs the mouse visible to click buttons.
            if (IsInMainMenu())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}