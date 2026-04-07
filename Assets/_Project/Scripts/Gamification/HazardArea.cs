using UnityEngine;
using TMPro;

/// <summary>
/// Universal hazard zone. Stops the player, shows a custom message, 
/// and activates the Game Over panel for both PC and VR.
/// </summary>
public class HazardArea : MonoBehaviour
{
    [Header("=== Hazard Settings ===")]
    [Tooltip("The exact message to show when hitting this specific hazard")]
    [TextArea]
    public string hazardMessage = "Warning";

    [Tooltip("CHECK THIS FOR CROSSWALKS: If the player is already inside when this turns on, they won't die.")]
    public bool allowSafeExitIfAlreadyInside = false;

    [Header("=== UI References (PC) ===")]
    public TMP_Text warningTextPC;
    public GameObject hazardPanelPC;

    [Header("=== UI References (VR) ===")]
    public TMP_Text warningTextVR;
    public GameObject hazardPanelVR;
    public Transform vrCamera;
    
    [Header("=== VR Hand Manager ===")]
    [Tooltip("Drag the Camera Offset (HandVisibilityManager) here")]
    public HandVisibilityManager handVisibilityManager;

    public float vrPanelDistance = 1.5f;

    private static bool isGameOver = false;
    private bool playerIsSafe = false;

    private void Start()
    {
        isGameOver = false; 
    }

    private void OnEnable()
    {
        if (allowSafeExitIfAlreadyInside)
        {
            playerIsSafe = false; 

            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null)
            {
                Collider[] hits = Physics.OverlapBox(myCollider.bounds.center, myCollider.bounds.extents, transform.rotation);
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Player"))
                    {
                        playerIsSafe = true; 
                        break;
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isGameOver) return;

        if (other.CompareTag("Player"))
        {
            if (allowSafeExitIfAlreadyInside && playerIsSafe)
            {
                return;
            }

            // --- GAME OVER LOGIC ---
            isGameOver = true;

            // 1. Pára o tempo (Impede qualquer cadeira de andar)
            Time.timeScale = 0f;

            // 2. BLOQUEIA O MENU DE PAUSA!
            // Ao dizer ao Level Manager que o nível não está ativo, a Pausa deixa de funcionar.
            if (LevelManagerVR.Instance != null)
            {
                LevelManagerVR.Instance.isLevelActive = false;
            }

            // 3. === LÓGICA DO PC ===
            if (warningTextPC != null) warningTextPC.text = hazardMessage;
            if (hazardPanelPC != null) hazardPanelPC.SetActive(true);
            
            // Liberta e mostra o rato para o jogador de PC poder clicar
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 4. === LÓGICA DO VR ===
            if (warningTextVR != null) warningTextVR.text = hazardMessage;
            if (hazardPanelVR != null)
            {
                // Teletransporta o painel para a frente da cara
                if (vrCamera != null)
                {
                    Vector3 spawnPos = vrCamera.position + (vrCamera.forward * vrPanelDistance);
                    spawnPos.y = vrCamera.position.y; 
                    hazardPanelVR.transform.position = spawnPos;
                    hazardPanelVR.transform.LookAt(vrCamera);
                    hazardPanelVR.transform.Rotate(0, 180, 0); 
                }
                
                hazardPanelVR.SetActive(true);

                // Avisa o gestor de mãos para ligar os lasers (modo Pausa)
                if (handVisibilityManager != null)
                {
                    handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PauseMenu;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsSafe = false;
        }
    }
}