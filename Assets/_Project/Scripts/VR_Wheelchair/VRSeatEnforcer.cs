using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRSeatEnforcer : MonoBehaviour
{
    // --- VARIABLES ---

    [Header("Core References")]
    [SerializeField] private Transform headCamera;

    [Tooltip("MUST be the SeatTarget object on the wheelchair!")]
    [SerializeField] private Transform seatCenter;

    [SerializeField] private Image fadeImage;
    [SerializeField] private VRSeatCalibrator seatCalibrator;

    [Header("Warning Text")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private string warningMessage = "Volta para a cadeira!\n\nCarrega no botão do analógico\npara recentrar";
    [SerializeField] private Color warningColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private float warningPulseSpeed = 2f;

    [Header("Lean Boundaries (meters)")]
    [SerializeField] private float maxForward = 0.4f;
    [SerializeField] private float maxBack = 0.15f;
    [SerializeField] private float maxSide = 0.35f;
    [SerializeField] private float maxUp = 0.3f;
    [SerializeField] private float maxDown = 0.2f;

    [Header("Fade Settings")]
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField] private float fadeStartPercent = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Internal state trackers
    private float currentFadeAlpha = 0f;
    private Color fadeColor = Color.black;
    private float calibratedHeadHeight = 0f;
    private bool heightCalibrated = false;
    private float debugTimer = 0f;

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

        // --- POSITION MATH ---
        Vector3 localOffset = seatCenter.InverseTransformPoint(headCamera.position);
        float heightDelta = headCamera.position.y - calibratedHeadHeight;

        float violation = 0f;

        if (localOffset.z > 0) violation = Mathf.Max(violation, FadeRatio(localOffset.z, maxForward));
        if (localOffset.z < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(localOffset.z), maxBack));
        violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(localOffset.x), maxSide));
        if (heightDelta > 0) violation = Mathf.Max(violation, FadeRatio(heightDelta, maxUp));
        if (heightDelta < 0) violation = Mathf.Max(violation, FadeRatio(Mathf.Abs(heightDelta), maxDown));

        // --- DEBUG PRINTING ---
        if (showDebugLogs)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer > 2f)
            {
                debugTimer = 0f;
                Debug.Log($"[Enforcer] fwd/back:{localOffset.z:F2} side:{localOffset.x:F2} " +
                          $"height:{heightDelta:F2} violation:{violation:F2}");
            }
        }

        // --- FADE APPLICATION ---
        float targetAlpha = Mathf.Clamp(violation, 0f, 0.85f);
        currentFadeAlpha = Mathf.Lerp(currentFadeAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (fadeImage != null)
        {
            fadeColor.a = currentFadeAlpha;
            fadeImage.color = fadeColor;
        }

        // --- WARNING TEXT ---
        if (currentFadeAlpha > 0.5f)
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

        float pulse = Mathf.Lerp(0.7f, 1f,
            (Mathf.Sin(Time.unscaledTime * warningPulseSpeed) + 1f) / 2f);

        string hex = ColorUtility.ToHtmlStringRGBA(warningColor);

        warningText.text =
            $"<color=#{hex}><size=150%><b>Oops!</b></size>\n\n" +
            $"<size=80%>{warningMessage}</size></color>";

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
        if (headCamera != null)
        {
            calibratedHeadHeight = headCamera.position.y;
            heightCalibrated = true;

            currentFadeAlpha = 0f;
            if (fadeImage != null)
            {
                fadeColor.a = 0f;
                fadeImage.color = fadeColor;
            }

            HideWarning();

            Debug.Log($"[VRSeatEnforcer] Limits Reset! Height calibrated at {calibratedHeadHeight}");
        }
    }

    // --- EDITOR VISUALS ---

    private void OnDrawGizmosSelected()
    {
        if (seatCenter == null) return;

        Gizmos.matrix = seatCenter.localToWorldMatrix;

        Gizmos.color = new Color(0, 1, 0, 0.15f);
        Vector3 center = new Vector3(0, 0, (maxForward - maxBack) * 0.5f);
        Vector3 size = new Vector3(maxSide * 2f, 0.1f, maxForward + maxBack);
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * maxForward);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(Vector3.zero, 0.03f);
    }
}