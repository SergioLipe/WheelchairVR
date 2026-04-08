using UnityEngine;
using TMPro;

public class VRCountdownUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Settings")]
    [SerializeField] private float countdownTime = 3.0f;

    [Header("Messages")]
    [SerializeField] private string topMessage = "Senta-te bem na cadeira!";
    [SerializeField] private string finalMessage = "Vamos lá!";

    [Header("References")]
    [SerializeField] private MovementVR movementScript;

    [Header("Style")]
    [SerializeField] private float textSize = 0.5f;
    [SerializeField] private float numberSize = 1.2f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private Color numberColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color messageColor = Color.white;
    [SerializeField] private Color finalColor = new Color(0.4f, 1f, 0.5f);

    private float timer;
    private bool isCounting = false;
    private bool showingFinal = false;
    private float finalTimer = 0f;

    private void Start()
    {
        if (movementScript != null)
            movementScript.inputLocked = true;

        timer = countdownTime;
        isCounting = true;

        if (countdownText != null)
            countdownText.alignment = TextAlignmentOptions.Center;
    }

    private void Update()
    {
        if (showingFinal)
        {
            finalTimer -= Time.unscaledDeltaTime;
            if (finalTimer <= 0)
            {
                showingFinal = false;
                if (countdownText != null)
                    countdownText.text = "";

                if (movementScript != null)
                    movementScript.inputLocked = false;
            }
            return;
        }

        if (!isCounting) return;

        timer -= Time.unscaledDeltaTime;

        if (timer > 0)
        {
            if (countdownText != null)
            {
                int seconds = Mathf.CeilToInt(timer);

                float pulse = Mathf.Lerp(0.9f, 1.1f,
                    (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) / 2f);

                float numSize = numberSize * pulse;

                string msgHex = ColorUtility.ToHtmlStringRGBA(messageColor);
                string numHex = ColorUtility.ToHtmlStringRGBA(numberColor);

                countdownText.text =
                    $"<color=#{msgHex}><size={textSize}>{topMessage}</size></color>\n\n" +
                    $"<color=#{numHex}><size={numSize:F2}><b>{seconds}</b></size></color>\n" +
                    $"<color=#{msgHex}><size={textSize * 0.5f:F2}>● ● ●</size></color>";
            }
        }
        else
        {
            isCounting = false;
            showingFinal = true;
            finalTimer = 1f;

            if (countdownText != null)
            {
                string finalHex = ColorUtility.ToHtmlStringRGBA(finalColor);
                countdownText.text =
                    $"<color=#{finalHex}><size={textSize * 1.2f:F2}><b>{finalMessage}</b></size></color>";
            }
        }
    }
}