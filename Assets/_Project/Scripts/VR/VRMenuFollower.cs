using UnityEngine;

/// <summary>
/// Mantém um Canvas VR sempre confortavelmente à frente do jogador.
/// Usa "lazy follow": o menu só se reposiciona quando o jogador olha para longe dele,
/// evitando que cole rigidamente à cabeça (o que causa enjoo).
/// </summary>
public class VRMenuFollower : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("A câmara da cabeça (Main Camera dentro do XR Origin)")]
    [SerializeField] private Transform headCamera;

    [Tooltip("O Canvas/menu a posicionar (este objeto, se deixares vazio)")]
    [SerializeField] private Transform menuTransform;

    [Header("Placement")]
    [Tooltip("Distância à frente da câmara (metros)")]
    [SerializeField] private float distance = 2.0f;

    [Tooltip("Altura relativa aos olhos (0 = à altura dos olhos, negativo = abaixo)")]
    [SerializeField] private float heightOffset = -0.2f;

    [Header("Follow Behaviour")]
    [Tooltip("Ângulo (graus) que o jogador pode virar a cabeça antes do menu reposicionar")]
    [SerializeField] private float followThreshold = 30f;

    [Tooltip("Velocidade de reposicionamento. Maior = mais rápido")]
    [SerializeField] private float followSpeed = 4f;

    [Tooltip("Se ligado, o menu reposiciona sempre suavemente (sem zona morta)")]
    [SerializeField] private bool alwaysFollow = false;

    [Header("Startup")]
    [Tooltip("Segundos a esperar antes do primeiro snap (deixa o XR inicializar)")]
    [SerializeField] private float startupDelay = 0.5f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isRepositioning = false;
    private bool isReady = false;

    private void Start()
    {
        if (menuTransform == null) menuTransform = transform;
        if (headCamera == null && Camera.main != null) headCamera = Camera.main.transform;

        Invoke(nameof(InitialSnap), startupDelay);
    }

    private void InitialSnap()
    {
        SnapToFront();
        isReady = true;
    }

    private void LateUpdate()
    {
        if (!isReady || headCamera == null || menuTransform == null) return;

        // Direção da cabeça projetada no plano horizontal
        Vector3 headForward = headCamera.forward;
        headForward.y = 0f;
        if (headForward.sqrMagnitude < 0.001f) return; // câmara a olhar perfeitamente para cima/baixo
        headForward.Normalize();

        // Direção atual do menu em relação à cabeça
        Vector3 toMenu = menuTransform.position - headCamera.position;
        toMenu.y = 0f;
        if (toMenu.sqrMagnitude < 0.001f) return;
        toMenu.Normalize();

        // Ângulo entre onde olho e onde está o menu
        float angle = Vector3.Angle(headForward, toMenu);

        // Decide se precisa de reposicionar
        if (alwaysFollow || angle > followThreshold)
        {
            isRepositioning = true;
        }

        if (isRepositioning)
        {
            ComputeTarget();
            menuTransform.position = Vector3.Lerp(menuTransform.position, targetPosition, Time.deltaTime * followSpeed);
            menuTransform.rotation = Quaternion.Slerp(menuTransform.rotation, targetRotation, Time.deltaTime * followSpeed);

            // Para de reposicionar quando já está alinhado
            if (Vector3.Distance(menuTransform.position, targetPosition) < 0.05f)
            {
                isRepositioning = false;
            }
        }
    }

    private void ComputeTarget()
    {
        Vector3 headForward = headCamera.forward;
        headForward.y = 0f;
        headForward.Normalize();

        targetPosition = headCamera.position + headForward * distance;
        targetPosition.y = headCamera.position.y + heightOffset;

        // O menu vira-se para o jogador
        Vector3 lookDir = targetPosition - headCamera.position;
        lookDir.y = 0f;
        targetRotation = Quaternion.LookRotation(lookDir);
    }

    /// <summary>
    /// Coloca o menu imediatamente à frente (sem suavização).
    /// Chama isto no botão de recenter.
    /// </summary>
    public void SnapToFront()
    {
        if (headCamera == null) return;
        ComputeTarget();
        menuTransform.position = targetPosition;
        menuTransform.rotation = targetRotation;
        isRepositioning = false;
    }
}