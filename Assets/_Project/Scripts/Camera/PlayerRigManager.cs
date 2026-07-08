using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages switching between completely separate PC and VR player rigs.
/// Must be placed on an independent GameObject (like GameManager), NOT inside the rigs.
/// [FOG] Also disables the fog on PC and keeps it on VR.
/// </summary>
public class PlayerRigManager : MonoBehaviour
{
    [Header("=== Player Rigs ===")]
    [Tooltip("Drag the Wheelchair_PC object here")]
    public GameObject wheelchairPC;
    [Tooltip("Drag the Wheelchair_VR object here")]
    public GameObject wheelchairVR;

    [Header("=== Fog Settings ===")]
    [Tooltip("If true, fog is disabled on PC and kept on VR")]
    public bool disableFogOnPC = true;

    // Guarda o estado inicial do fog (ligado no projeto) para o VR
    private bool initialFogState;
    private bool fogStateCached = false;

    private bool IsInMainMenu()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "MainMenu" || sceneName.Contains("Menu");
    }

    void Start()
    {
        // Guardar o estado do fog tal como está definido no projeto (para o VR)
        if (!fogStateCached)
        {
            initialFogState = RenderSettings.fog;
            fogStateCached = true;
        }

        UpdateRigMode();
    }

    void Update()
    {
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

            // [FOG] Em VR, mantém o fog tal como estava no projeto
            RenderSettings.fog = initialFogState;

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

            // [FOG] No PC, desliga o fog (se a opção estiver ativa)
            if (disableFogOnPC)
            {
                RenderSettings.fog = false;
            }
            else
            {
                RenderSettings.fog = initialFogState;
            }

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