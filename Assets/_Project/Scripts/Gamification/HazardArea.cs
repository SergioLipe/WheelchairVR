using UnityEngine;
using TMPro;

/// <summary>
/// Universal hazard zone. Stops the player, shows a custom message, 
/// and activates the Game Over panel.
/// </summary>
public class HazardArea : MonoBehaviour
{
    [Header("=== Hazard Settings ===")]
    [Tooltip("The exact message to show when hitting this specific hazard")]
    [TextArea]
    public string hazardMessage = "Warning";

    [Tooltip("CHECK THIS FOR CROSSWALKS: If the player is already inside when this turns on, they won't die.")]
    public bool allowSafeExitIfAlreadyInside = false;

    [Header("=== UI References ===")]
    [Tooltip("Drag the Warning Text UI here")]
    public TMP_Text warningTextUI;
    
    [Tooltip("Drag the Game Over Panel you copied here")]
    public GameObject hazardPanel; 

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

            // 1. Pára o tempo (tal como na Pausa! Isto já impede a cadeira de andar)
            Time.timeScale = 0f;

            // 2. Mostra o aviso e ativa o painel
            if (warningTextUI != null)
            {
                warningTextUI.text = hazardMessage;
            }
            
            if (hazardPanel != null)
            {
                hazardPanel.SetActive(true);
            }

            // 3. Liberta o rato (tal como na Pausa!)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Já NÃO desligamos o MovementPC.enabled = false. 
            // Assim ele continua a proteger o rato de ser escondido pela câmara!
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