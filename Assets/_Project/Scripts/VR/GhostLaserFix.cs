using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// Hides the ghost laser when the VR controller disconnects or loses tracking.
/// </summary>
public class GhostLaserFix : MonoBehaviour
{
    [Tooltip("Drag the 'Is Tracked' Input Action for this hand")]
    public InputActionReference isTrackedAction;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual lineVisual;

    void Start()
    {
        // Apanha a linha vermelha que está neste comando
        lineVisual = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
    }

    void Update()
    {
        // Verifica se a ação existe e se temos a linha
        if (isTrackedAction != null && isTrackedAction.action != null && lineVisual != null)
        {
            // Lê o estado do comando (0 = desligado/perdido, 1 = a ser detetado)
            float isTracked = isTrackedAction.action.ReadValue<float>();
            
            // Liga ou desliga a linha dependendo do estado
            lineVisual.enabled = (isTracked > 0.5f);
        }
    }
}