using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRSeatEnforcer : MonoBehaviour
{
    // --- VARIABLES ---

    [Header("Core References")]
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform seatCenter;
    [SerializeField] private Image fadeImage;
    [SerializeField] private VRSeatCalibrator seatCalibrator;

    [Header("Warning Text")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private string warningHeader = "Foste longe de mais."; // Adicionado para o novo design
    [SerializeField] private string warningMessage = "Volta para a cadeira!\n\nCarrega no botão do analógico\npara recentrar";
    [SerializeField] private Color warningColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private float warningPulseSpeed = 2f;

    [Header("Lean Boundaries (meters)")]
    [SerializeField] private float maxForward = 0.4f;
    [Tooltip("Strict back limit when looking straight ahead (blocked by physical seat).")]
    [SerializeField] private float maxBackStraight = 0.05f;
    [Tooltip("Relaxed back limit when turning head to look over the shoulder.")]
    [SerializeField] private float maxBackLookingAround = 0.25f;
    [SerializeField] private float maxSide = 0.35f;
    [SerializeField] private float maxUp = 0.3f;
    [SerializeField] private float maxDown = 0.2f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField] private float fadeStartPercent = 0.7f;
    [Range(0f, 1f)]
    [Tooltip("1.0 is pitch black, 0.5 is semi-transparent.")]
    [SerializeField] private float maxDarkness = 1.0f;

    // Internal state trackers
    private float currentFadeAlpha = 0f;
    private Color fadeColor = Color.black;
    private float calibratedLocalHeight = 0f;
    private bool heightCalibrated = false;

    // --- UNITY LIFECYCLE METHODS ---

    private void Awake()
    {
        if (fadeImage != null)
        {
            fadeColor = Color.black;
            fadeColor.a = 0f;
            fadeImage.color = fadeColor;
        }
    }

    private void OnEnable()
    {
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated += ResetHeightCalibration;
        }
    }

    private void OnDisable()
    {
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated -= ResetHeightCalibration;
        }
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            fadeColor.a = 0f;
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;
        }

        if (warningText != null)
        {
            warningText.text = "";
            warningText.raycastTarget = false;
        }
    }

    private void LateUpdate()
    {
        if (headCamera == null || seatCenter == null) return;

        if (!heightCalibrated)
        {
            if (fadeImage != null)
            {
                fadeColor.a = 0f;
                fadeImage.color = fadeColor;
            }
            HideWarning();
            return;
        }

        // --- BULLETPROOF POSITION MATH ---
        Vector3 worldOffset = headCamera.position - seatCenter.position;
        
        float currentZ = Vector3.Dot(worldOffset, seatCenter.forward);
        float currentX = Vector3.Dot(worldOffset, seatCenter.right);
        float currentY = Vector3.Dot(worldOffset, seatCenter.up);
        
        float heightDelta = currentY - calibratedLocalHeight;

        // --- DYNAMIC BACK LIMIT CALCULATION ---
        Vector3 headForwardLevel = Vector3.ProjectOnPlane(headCamera.forward, seatCenter.up).normalized;
        Vector3 seatForwardLevel = Vector3.ProjectOnPlane(seatCenter.forward, seatCenter.up).normalized;
        
        float headTurnAngle = Vector3.Angle(seatForwardLevel, headForwardLevel);

        float turnRatio = Mathf.Clamp01((headTurnAngle - 30f) / 60f);
        float dynamicMaxBack = Mathf.Lerp(maxBackStraight, maxBackLookingAround, turnRatio);

        // --- VIOLATION CHECKS ---
        float violation = 0f;

        if (currentZ > 0) violation = Mathf.Max(violation, FadeRatio(currentZ, maxForward));
        if (currentZ < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(currentZ), dynamicMaxBack));
        
        violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(currentX), maxSide));
        if (heightDelta > 0) violation = Mathf.Max(violation, FadeRatio(heightDelta, maxUp));
        if (heightDelta < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(heightDelta), maxDown));

        // --- FADE APPLICATION ---
        float targetAlpha = Mathf.Clamp(violation, 0f, maxDarkness);
        currentFadeAlpha = Mathf.Lerp(currentFadeAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (fadeImage != null)
        {
            fadeColor.a = currentFadeAlpha;
            fadeImage.color = fadeColor;
        }

        // --- WARNING TEXT ---
        if (currentFadeAlpha > (maxDarkness * 0.6f))
            ShowWarning();
        else
            HideWarning();
    }

    // --- HELPER METHODS ---

    private float FadeRatio(float distance, float limit)
    {
        float ratio = distance / Mathf.Max(limit, 0.01f);
        if (ratio <= fadeStartPercent) return 0f;
        return Mathf.Clamp01((ratio - fadeStartPercent) / (1f - fadeStartPercent));
    }

    private void ShowWarning()
    {
        if (warningText == null) return;
        
        float pulse = Mathf.Lerp(0.7f, 1f, (Mathf.Sin(Time.unscaledTime * warningPulseSpeed) + 1f) / 2f);
        string hex = ColorUtility.ToHtmlStringRGBA(warningColor);

        // --- ADVANCED RICH TEXT FORMATTING ---
        // Cabeçalho tático usando a tua warningColor
        string headerPart = $"<color=#{hex}><size=140%><b>[ AVISO DE SEGURANÇA ]</b></size></color>";
        
        // Aviso principal a branco
        string oopsPart = $"<color=#FFFFFF><size=110%><b>Oops! {warningHeader}</b></size></color>";
        
        // Instruções em cinzento para melhor leitura
        string messagePart = $"<color=#CCCCCC><size=80%>{warningMessage}</size></color>";

        warningText.text = $"{headerPart}\n\n{oopsPart}\n\n{messagePart}";
        warningText.alpha = pulse;
    }

    private void HideWarning()
    {
        if (warningText == null) return;
        warningText.text = "";
        warningText.alpha = 1f;
    }

    public void ResetHeightCalibration()
    {
        if (headCamera != null && seatCenter != null)
        {
            Vector3 worldOffset = headCamera.position - seatCenter.position;
            calibratedLocalHeight = Vector3.Dot(worldOffset, seatCenter.up);
            heightCalibrated = true;

            currentFadeAlpha = 0f;
            if (fadeImage != null)
            {
                fadeColor.a = 0f;
                fadeImage.color = fadeColor;
            }

            HideWarning();
        }
    }

    // --- EDITOR VISUALS ---

    private void OnDrawGizmosSelected()
    {
        if (seatCenter == null) return;

        Gizmos.matrix = Matrix4x4.TRS(seatCenter.position, seatCenter.rotation, Vector3.one);

        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Vector3 center = new Vector3(0, 0, (maxForward - maxBackStraight) * 0.5f);
        Vector3 size = new Vector3(maxSide * 2f, 0.1f, maxForward + maxBackStraight);
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * maxForward);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(Vector3.zero, 0.03f);
    }
}