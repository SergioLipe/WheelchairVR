using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the star collection. Disables visuals instead of the whole object 
/// to allow the coroutine to finish and show the end panel.
/// </summary>
public class FinishLevelTrigger : MonoBehaviour
{
    [Header("--- Finish Settings ---")]
    [Tooltip("The visual part of the star (MeshRenderer or SpriteRenderer)")]
    public GameObject starVisual;

    [Tooltip("Delay in seconds (Real Time) before showing the results")]
    public float finishDelay = 0.5f;

    [Header("--- Audio Settings ---")]
    [Tooltip("The sound effect to play when the star is collected")]
    public AudioClip finishSound;

    [Tooltip("Volume of the finish sound (0.0 to 1.0)")]
    [Range(0f, 1f)]
    public float finishVolume = 1f;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the player touched the star
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;

            // Using transform.position instead of Camera.main to prevent crashes ---
            if (finishSound != null)
            {
                AudioSource.PlayClipAtPoint(finishSound, transform.position, finishVolume);
            }

            // 1. HIDE VISUALS ONLY (Keep the GameObject active for the coroutine)
            HideStarVisuals();

            // 2. Freeze the game
            Time.timeScale = 0f;

            // 3. Disable movement
            Movement wheelchair = other.GetComponent<Movement>();
            if (wheelchair != null) 
            {
                wheelchair.enabled = false;
            }

            // 4. Start the sequence
            StartCoroutine(FinishSequence());
        }
    }

    private void HideStarVisuals()
    {
        if (starVisual == null) return;

        // Try to hide 3D mesh
        Renderer mesh = starVisual.GetComponent<Renderer>();
        if (mesh != null) mesh.enabled = false;

        // Try to hide UI Image (if it's a Canvas star)
        UnityEngine.UI.Image img = starVisual.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.enabled = false;

        // If it has children (like particles or multiple meshes), hide them too
        foreach (Renderer r in starVisual.GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }
    }

    private IEnumerator FinishSequence()
    {
        // Wait using Realtime because Time.timeScale is 0
        yield return new WaitForSecondsRealtime(finishDelay);

        // 5. Trigger the LevelManager to show the panel
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.FinishLevel();
        }
    }
}