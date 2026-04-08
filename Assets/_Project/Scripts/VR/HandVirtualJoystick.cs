using UnityEngine;

/// <summary>
/// Joystick virtual controlado por hand tracking.
/// Comporta-se como o manípulo físico no apoio de braço da cadeira.
/// Output é um Vector2 com a mesma forma de um thumbstick (-1..1).
/// </summary>
public class HandVirtualJoystick : MonoBehaviour
{
    [Header("=== Referências ===")]
    [Tooltip("Transform da mão (ex: RightWhiteHand). Tem de seguir a mão real via hand tracking.")]
    public Transform handTransform;

    [Tooltip("Ponto âncora do joystick. Coloca como filho da cadeira, no sítio do apoio de braço.")]
    public Transform joystickAnchor;

    [Tooltip("Opcional: visual de uma alavanca que se inclina conforme a mão empurra.")]
    public Transform stickVisual;

    [Header("=== Ativação (agarrar/largar) ===")]
    [Tooltip("Distância à âncora a partir da qual a mão 'agarra' o joystick.")]
    public float grabRadius = 0.12f;

    [Tooltip("Distância de libertação (maior que a de agarrar para evitar flicker).")]
    public float releaseRadius = 0.20f;

    [Tooltip("Se ativo, ignora a distância e está sempre ligado. Útil para testar sem hand tracking.")]
    public bool alwaysActive = false;

    [Header("=== Geometria do joystick ===")]
    [Tooltip("Deslocamento máximo da mão (em metros) que mapeia para input máximo. ~10cm é natural.")]
    public float maxOffset = 0.10f;

    [Tooltip("Inclinação visual da alavanca no offset máximo (graus).")]
    public float maxStickTilt = 30f;

    [Header("=== Debug ===")]
    [SerializeField] private Vector2 output = Vector2.zero;
    [SerializeField] private bool isGrabbing = false;

    public Vector2 Output => output;
    public bool IsActive => isGrabbing || alwaysActive;

    void Update()
{
    if (handTransform == null || joystickAnchor == null)
    {
        output = Vector2.zero;
        return;
    }

    // Histerese de agarrar/largar (distância real no mundo)
    float dist = Vector3.Distance(handTransform.position, joystickAnchor.position);

    if (alwaysActive)
        isGrabbing = true;
    else if (!isGrabbing && dist <= grabRadius)
        isGrabbing = true;
    else if (isGrabbing && dist > releaseRadius)
        isGrabbing = false;

    if (!isGrabbing)
    {
        output = Vector2.zero;
        if (stickVisual != null)
            stickVisual.localRotation = Quaternion.Slerp(
                stickVisual.localRotation, Quaternion.identity, Time.deltaTime * 10f);
        return;
    }

    // Offset em WORLD space (metros reais), imune a scale
    Vector3 worldOffset = handTransform.position - joystickAnchor.position;

    // Projetar em eixos do pivot (sem escala) usando os seus vetores direcionais
    float forwardAmount = Vector3.Dot(worldOffset, joystickAnchor.forward);
    float rightAmount   = Vector3.Dot(worldOffset, joystickAnchor.right);

    float x = Mathf.Clamp(rightAmount / maxOffset, -1f, 1f);
    float y = Mathf.Clamp(forwardAmount / maxOffset, -1f, 1f);

    output = new Vector2(x, y);

    // Feedback visual: a alavanca inclina-se
    if (stickVisual != null)
    {
        float tiltX = -y * maxStickTilt;
        float tiltZ = -x * maxStickTilt;
        stickVisual.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
    }
}

    // Desenha os raios no editor para alinhares a âncora visualmente
    void OnDrawGizmosSelected()
    {
        if (joystickAnchor == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(joystickAnchor.position, grabRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(joystickAnchor.position, releaseRadius);
        Gizmos.color = Color.cyan;
        Gizmos.matrix = joystickAnchor.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(maxOffset * 2, 0.01f, maxOffset * 2));
    }
}