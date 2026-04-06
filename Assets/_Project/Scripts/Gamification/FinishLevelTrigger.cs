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

    private void OnTriggerEnter(Collider other)
    {
        // 1. O SEGREDO DO VR: Verifica se quem bateu foi o Player, OU se a "Raiz/Pai" de quem bateu é o Player.
        // Assim, se bateres com o comando ou com a cabeça, ele deteta a cadeira na mesma!
        if (!hasTriggered && (other.CompareTag("Player") || other.transform.root.CompareTag("Player")))
        {
            hasTriggered = true;

            if (finishSound != null)
            {
                AudioSource.PlayClipAtPoint(finishSound, transform.position, finishVolume);
            }

            // 2. Esconde a Estrela
            HideStarVisuals();

            // 3. Pára o tempo
            Time.timeScale = 0f;

            // 4. Desliga o movimento (Procurando na 'Raiz' para não falhar)
            MonoBehaviour movementPC = other.transform.root.GetComponent("Movement") as MonoBehaviour;
            if (movementPC != null) movementPC.enabled = false;

            MonoBehaviour movementVR = other.transform.root.GetComponent("MovementVR") as MonoBehaviour;
            if (movementVR != null) movementVR.enabled = false;

            // 5. Inicia a sequência de fim
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
        // Espera o tempo definido
        yield return new WaitForSecondsRealtime(finishDelay);

        bool panelShown = false;

        // Tenta acionar o Gestor de PC
        if (LevelManager_PC.Instance != null)
        {
            LevelManager_PC.Instance.FinishLevel();
            panelShown = true;
        }

        // Tenta acionar o Gestor de VR
        if (LevelManagerVR.Instance != null)
        {
            LevelManagerVR.Instance.FinishLevel();
            panelShown = true;
        }

        // Se por algum motivo não encontrar nenhum gestor, avisa na consola
        if (!panelShown)
        {
            Debug.LogWarning("FinishLevelTrigger: Não encontrei nem o LevelManager_PC nem o LevelManagerVR na cena!");
        }
    }
}