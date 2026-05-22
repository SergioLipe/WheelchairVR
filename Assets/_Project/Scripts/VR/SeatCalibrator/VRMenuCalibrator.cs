using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class VRMenuCalibrator : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform menuTarget;

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
        if (xrOrigin == null || headCamera == null || menuTarget == null) return;

        // 1. ROTAÇÃO PERFEITA (O caminho mais curto)
        // O Mathf.DeltaAngle impede que o Unity se confunda entre 0 e 360 graus
        float yawDiff = Mathf.DeltaAngle(headCamera.eulerAngles.y, menuTarget.eulerAngles.y);
        
        // Roda o mundo usando a TUA CABEÇA como o centro do pião (adeus órbitas!)
        xrOrigin.transform.RotateAround(headCamera.position, Vector3.up, yawDiff);

        // 2. POSIÇÃO (Trancando a altura)
        Vector3 posDiff = menuTarget.position - headCamera.position;
        posDiff.y = 0f; // Bloqueia a altura para não ires parar ao chão

        // Move a sala para te colar ao menu
        xrOrigin.transform.position += posDiff;
        
        Debug.Log("[VRMenuCalibrator] Fixo! O jogador está no centro e a olhar em frente.");
    }
}