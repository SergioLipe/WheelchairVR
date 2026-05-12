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

            // Vamos buscar o BoxCollider específico em vez de um Collider genérico
            BoxCollider myBox = GetComponent<BoxCollider>();
            if (myBox != null)
            {
                // 1. Calcula o centro exato no mundo
                Vector3 boxCenter = transform.TransformPoint(myBox.center);

                // 2. Calcula o tamanho exato, ignorando se a escala tem o sinal de menos (-)
                Vector3 boxHalfExtents = Vector3.Scale(myBox.size, transform.lossyScale) * 0.5f;
                boxHalfExtents = new Vector3(Mathf.Abs(boxHalfExtents.x), Mathf.Abs(boxHalfExtents.y), Mathf.Abs(boxHalfExtents.z));

                // 3. Procura o jogador com precisão absoluta
                Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation);
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
        // Checks if the script component is enabled in the Inspector. If not, exit the function.
        if (!this.enabled) return;

        if (isGameOver) return;

        if (other.CompareTag("Player"))
        {
            // === A LINHA DETETIVE ===
            Debug.Log("<color=red>HAZARD ATIVADO PELO OBJETO: </color>" + other.gameObject.name);

            if (allowSafeExitIfAlreadyInside && playerIsSafe)
            {
                return;
            }

            // --- GAME OVER LOGIC ---
            isGameOver = true;

            // 1. Pára o tempo (Impede qualquer cadeira de andar)
            Time.timeScale = 0f;

            // 2. BLOQUEIA O MENU DE PAUSA!
            if (LevelManagerVR.Instance != null)
            {
                LevelManagerVR.Instance.isLevelActive = false;
            }

            // 3. === LÓGICA DO PC ===
            if (warningTextPC != null) warningTextPC.text = hazardMessage;
            if (hazardPanelPC != null) hazardPanelPC.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 4. === LÓGICA DO VR ===
            if (warningTextVR != null) warningTextVR.text = hazardMessage;
            if (hazardPanelVR != null)
            {
                if (vrCamera != null)
                {
                    Vector3 spawnPos = vrCamera.position + (vrCamera.forward * vrPanelDistance);
                    spawnPos.y = vrCamera.position.y;
                    hazardPanelVR.transform.position = spawnPos;
                    hazardPanelVR.transform.LookAt(vrCamera);
                    hazardPanelVR.transform.Rotate(0, 180, 0);
                }

                hazardPanelVR.SetActive(true);

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