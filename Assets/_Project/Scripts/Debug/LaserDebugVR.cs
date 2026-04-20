using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ferramenta de Debugging para descobrir o que está a bloquear os lasers do VR.
/// </summary>
public class LaserDebugVR : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    void Start()
    {
        // Apanha o teu Laser automaticamente
        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        
        if (rayInteractor == null)
        {
            Debug.LogError("LaserDebugVR: Não encontrei nenhum XR Ray Interactor neste objeto!");
        }
    }

    void Update()
    {
        if (rayInteractor == null) return;

        // 1. Verifica se o laser está a bater num Canvas / UI
        if (rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult uiResult))
        {
            if (uiResult.gameObject != null)
            {
                Debug.Log($"<color=cyan>[LASER UI]</color> O laser está a bater em: <b>{uiResult.gameObject.name}</b> | Layer: {LayerMask.LayerToName(uiResult.gameObject.layer)}");
            }
        }
        // 2. Se não estiver a bater na UI, verifica se bate noutro objeto 3D qualquer (Colisores)
        else if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (hit.collider != null)
            {
                Debug.Log($"<color=orange>[LASER 3D]</color> O laser está a bater em: <b>{hit.collider.gameObject.name}</b> | Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            }
        }
    }
}