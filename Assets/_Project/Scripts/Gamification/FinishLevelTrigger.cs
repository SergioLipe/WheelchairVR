using UnityEngine;
using System.Collections;

/// <summary>
/// Universal Finish Trigger. Works for both PC and VR.
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
    
    // Variáveis para sabermos quem tocou na meta
    private bool wasPC = false;
    private bool wasVR = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && (other.CompareTag("Player") || other.transform.root.CompareTag("Player")))
        {
            hasTriggered = true;

            if (finishSound != null)
            {
                AudioSource.PlayClipAtPoint(finishSound, transform.position, finishVolume);
            }

            HideStarVisuals();
            Time.timeScale = 0f;

            // 1. Descobre quem é que tocou na meta (PC ou VR) e desliga os controlos
            MonoBehaviour movementPC = other.transform.root.GetComponent("Movement") as MonoBehaviour;
            if (movementPC != null) 
            {
                movementPC.enabled = false;
                wasPC = true;
            }

            MonoBehaviour movementVR = other.transform.root.GetComponent("MovementVR") as MonoBehaviour;
            if (movementVR != null) 
            {
                movementVR.enabled = false;
                wasVR = true;
            }

            // Failsafe: se bateu com a cabeça e não encontrou logo o script na raiz
            if (!wasPC && !wasVR)
            {
                if (other.GetComponentInChildren<Movement>() != null) wasPC = true;
                else if (other.GetComponentInChildren<MovementVR>() != null) wasVR = true;
                // Por defeito, assumimos PC se tudo o resto falhar
                else wasPC = true; 
            }

            StartCoroutine(FinishSequence());
        }
    }

    private void HideStarVisuals()
    {
        if (starVisual == null) return;

        Renderer mesh = starVisual.GetComponent<Renderer>();
        if (mesh != null) mesh.enabled = false;

        UnityEngine.UI.Image img = starVisual.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.enabled = false;

        foreach (Renderer r in starVisual.GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }
    }

    private IEnumerator FinishSequence()
    {
        yield return new WaitForSecondsRealtime(finishDelay);

        bool panelShown = false;

        // O GRANDE SEGREDO DA CORREÇÃO:
        // Se a cadeira do PC cortou a meta, SÓ chamamos o Gestor do PC!
        if (wasPC && LevelManager_PC.Instance != null)
        {
            LevelManager_PC.Instance.FinishLevel();
            panelShown = true;
        }
        // Se a cadeira de VR cortou a meta, SÓ chamamos o Gestor de VR!
        else if (wasVR && LevelManagerVR.Instance != null)
        {
            LevelManagerVR.Instance.FinishLevel();
            panelShown = true;
        }

        if (!panelShown)
        {
            Debug.LogWarning("FinishLevelTrigger: Não encontrei o LevelManager correto para esta plataforma!");
        }
    }
}