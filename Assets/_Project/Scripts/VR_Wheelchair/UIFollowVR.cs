using UnityEngine;

/// <summary>
/// Makes the UI smoothly follow the VR player's gaze.
/// Works even when the game is paused (Time.timeScale = 0).
/// </summary>
public class UIFollowVR : MonoBehaviour
{
    [Header("--- Target ---")]
    [Tooltip("Drag your Main Camera (VR Camera) here")]
    public Transform vrCamera;

    [Header("--- Follow Settings ---")]
    [Tooltip("Distance from the player's face")]
    public float distance = 1.2f;

    [Tooltip("Height offset (negative values move it slightly down)")]
    public float heightOffset = -0.1f;

    [Tooltip("How smooth/fast the menu catches up (Higher = faster)")]
    public float smoothSpeed = 6f;

    private void Start()
    {
        // Se te esqueceres de arrastar a câmara, ele tenta encontrá-la sozinho
        if (vrCamera == null && Camera.main != null)
        {
            vrCamera = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (vrCamera == null) return;

        // 1. Calcula a direção para onde o jogador está a olhar (apenas na horizontal)
        Vector3 forwardFlat = vrCamera.forward;
        forwardFlat.y = 0; // Ignora o olhar para cima/baixo para o menu não inclinar de forma estranha
        forwardFlat.Normalize();

        // 2. Calcula a posição alvo (à frente da cara + ajuste de altura)
        Vector3 targetPosition = vrCamera.position + (forwardFlat * distance);
        targetPosition.y = vrCamera.position.y + heightOffset;

        // 3. Calcula a rotação para que o painel olhe sempre para a câmara
        Vector3 directionToFace = transform.position - vrCamera.position;
        directionToFace.y = 0; // Mantém o painel perfeitamente vertical
        Quaternion targetRotation = Quaternion.LookRotation(directionToFace);

        // 4. Move e Roda suavemente! 
        // (Usamos unscaledDeltaTime para funcionar mesmo no Menu de Pausa com o jogo congelado)
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.unscaledDeltaTime * smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.unscaledDeltaTime * smoothSpeed);
    }
}