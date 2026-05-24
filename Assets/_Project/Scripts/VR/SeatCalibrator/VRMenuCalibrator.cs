using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class VRMenuCalibrator : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform menuTarget; // O ponto onde o jogador "senta"
    [SerializeField] private Transform canvasUI;   // <--- NOVO: O alvo para onde ele tem de olhar!

    [Header("Input Setup")]
    [SerializeField] private InputActionReference recenterAction;

    private void OnEnable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.Enable();
            recenterAction.action.performed += OnRecenter;
        }
    }

    private void OnDisable()
    {
        if (recenterAction != null && recenterAction.action != null)
            recenterAction.action.performed -= OnRecenter;
    }

    private void Start()
    {
        Invoke(nameof(CalibrateMenu), 1.0f);
    }

    private void OnRecenter(InputAction.CallbackContext ctx) => CalibrateMenu();

    public void CalibrateMenu()
    {
        if (xrOrigin == null || headCamera == null || menuTarget == null || canvasUI == null) return;

        // 1. POSIÇÃO (Move o jogador para o sítio primeiro, mantendo a altura)
        Vector3 alturaTrancada = new Vector3(menuTarget.position.x, headCamera.position.y, menuTarget.position.z);
        xrOrigin.MoveCameraToWorldLocation(alturaTrancada);

        // 2. MATEMÁTICA PURA (Calcula a linha reta exata até ao centro do Canvas)
        Vector3 direcaoParaCanvas = canvasUI.position - headCamera.position;
        direcaoParaCanvas.y = 0f; // Garante que ficas a olhar de frente, e não para o chão ou teto

        // 3. ROTAÇÃO (Obriga o Unity a apontar a cabeça exatamente para essa linha)
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, direcaoParaCanvas.normalized);
        
        Debug.Log("[VRMenuCalibrator] Recentrado com Matemática! Adeus ilusões de ótica!");
    }
}