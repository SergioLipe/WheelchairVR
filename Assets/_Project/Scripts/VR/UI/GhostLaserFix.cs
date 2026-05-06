using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hides the ghost laser and the reticle (bolinha) when the VR controller disconnects or loses tracking.
/// </summary>
public class GhostLaserFix : MonoBehaviour
{
    [Tooltip("Drag the 'Is Tracked' Input Action for this hand")]
    public InputActionReference isTrackedAction;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual lineVisual;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    void Start()
    {
        // Apanha o gestor visual (linha e bolinha) e o interactor (cliques)
        lineVisual = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
    }

    void Update()
    {
        if (isTrackedAction != null && isTrackedAction.action != null)
        {
            float isTracked = isTrackedAction.action.ReadValue<float>();
            bool shouldBeActive = (isTracked > 0.5f);
            
            // Só atualiza se houver uma mudança (poupa performance)
            if (lineVisual != null && lineVisual.enabled != shouldBeActive)
            {
                lineVisual.enabled = shouldBeActive;
                
                if (rayInteractor != null)
                {
                    rayInteractor.enabled = shouldBeActive;
                }
            }
        }
    }
}