using UnityEngine;
using TMPro;

/// <summary>
/// Universal hazard zone. Stops the player, shows a custom message, 
/// and activates the Game Over panel.
/// Features a "Safe Exit" toggle for Crosswalks to prevent unfair Game Overs.
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

    // Static variable to ensure Game Over only triggers once per level load
    private static bool isGameOver = false;

    // Tracks if the player was already crossing when the red light turned on
    private bool playerIsSafe = false;

    private void Start()
    {
        // Reset the variable every time the level starts
        isGameOver = false; 
    }

    private void OnEnable()
    {
        // When the red light turns on (enabling this object), check if the player is already inside!
        if (allowSafeExitIfAlreadyInside)
        {
            playerIsSafe = false; // Reset safety flag

            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null)
            {
                // Create an invisible box to see who is inside right now
                Collider[] hits = Physics.OverlapBox(myCollider.bounds.center, myCollider.bounds.extents, transform.rotation);
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Player"))
                    {
                        // The player was already here! Give them a free pass to finish crossing.
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
            // If they were already inside when the light turned red, IGNORE the collision!
            if (allowSafeExitIfAlreadyInside && playerIsSafe)
            {
                return;
            }

            // --- GAME OVER LOGIC ---
            isGameOver = true;

            // 1. Completely stop the wheelchair's movement script
            Movement movementScript = other.GetComponent<Movement>();
            if (movementScript != null)
            {
                movementScript.enabled = false; 
            }

            // 2. Stop the physical wheels
            WheelController wheels = other.GetComponent<WheelController>();
            if (wheels != null)
            {
                wheels.StopWheels();
            }

            // 3. Show the message and activate the dark panel
            if (warningTextUI != null)
            {
                warningTextUI.text = hazardMessage;
            }
            
            if (hazardPanel != null)
            {
                hazardPanel.SetActive(true);
            }

            // 4. Force the mouse cursor to appear so the player can click "TENTAR NOVAMENTE"
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Once the player successfully leaves the crosswalk, they are no longer safe.
        // If they try to turn back and re-enter on a red light, it will be Game Over!
        if (other.CompareTag("Player"))
        {
            playerIsSafe = false;
        }
    }
}