using UnityEngine;
using TMPro;
using System;
using UnityEngine.Localization.Settings;   // <-- NOVO

public class VRCountdownUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Settings")]
    [SerializeField] private float countdownTime = 3.0f;

    [Header("Messages")]
    [SerializeField] private string topMessage = "Senta-te bem na cadeira!";
    [SerializeField] private string finalMessage = "Vamos lá!";

    [Header("--- Target Scripts to Disable ---")]
    [Tooltip("Drag the object with MovementVR here")]
    [SerializeField] private MovementVR movementScript;

    [Tooltip("Drag the object with CollisionSystemVR here")]
    [SerializeField] private CollisionSystemVR collisionSystem;

    [Header("Hand Manager")]
    [Tooltip("Drag the Camera Offset (HandVisibilityManager) here")]
    [SerializeField] private HandVisibilityManager handManager;

    [Header("Visibility Control")]
    [Tooltip("Add all the GameObjects you want to hide during the countdown")]
    [SerializeField] private GameObject[] objectsToHide;

    [Tooltip("Drag the object with the VRSeatCalibrator script here to sync the appearance")]
    [SerializeField] private VRSeatCalibrator seatCalibrator;

    [Header("=== Audio Settings ===")]
    [Tooltip("The AudioSource that will play the sounds")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Short beep for numbers (3, 2, 1)")]
    [SerializeField] private AudioClip beepSound;
    [Tooltip("Long beep or chime for 'Vamos lá!'")]
    [SerializeField] private AudioClip goSound;

    [Header("Style")]
    [SerializeField] private float textSize = 0.5f;
    [SerializeField] private float numberSize = 1.2f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private Color numberColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color messageColor = Color.white;
    [SerializeField] private Color finalColor = new Color(0.4f, 1f, 0.5f);

    public Action OnCountdownFinished;

    private float timer;
    private bool isCounting = false;
    private bool showingFinal = false;
    private float finalTimer = 0f;

    private int lastSecondPlayed = -1;

    // === [LOCALIZAÇÃO] ===
    private const string TABLE = "GameText";

    private void OnEnable()
    {
        if (seatCalibrator != null)
            seatCalibrator.OnCalibrated += ShowHiddenObjects;
    }

    private void OnDisable()
    {
        if (seatCalibrator != null)
            seatCalibrator.OnCalibrated -= ShowHiddenObjects;
    }

    private void Start()
    {
        // [LOCALIZAÇÃO] Substituir as mensagens pelas versões traduzidas
        topMessage = SafeGet("countdown_sit", topMessage);
        finalMessage = SafeGet("countdown_go", finalMessage);

        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        if (movementScript != null)
            movementScript.enabled = false;

        if (collisionSystem != null)
            collisionSystem.enabled = false;

        if (handManager != null)
            handManager.currentMode = HandVisibilityManager.GameMode.Countdown;

        timer = countdownTime;
        isCounting = true;

        lastSecondPlayed = Mathf.CeilToInt(countdownTime) + 1;

        if (countdownText != null)
            countdownText.alignment = TextAlignmentOptions.Center;
    }

    // [LOCALIZAÇÃO] Helper que busca da tabela com fallback
    private string SafeGet(string key, string fallback)
    {
        try
        {
            string v = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE, key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
        catch
        {
            return fallback;
        }
    }

    private void ShowHiddenObjects()
    {
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
                obj.SetActive(true);
        }
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
                {
                    countdownText.text = "";
                    countdownText.gameObject.SetActive(false);
                }

                if (movementScript != null)
                    movementScript.enabled = true;

                if (collisionSystem != null)
                    collisionSystem.enabled = true;

                if (handManager != null)
                    handManager.currentMode = HandVisibilityManager.GameMode.PlayingLevel;

                OnCountdownFinished?.Invoke();
            }
            return;
        }

        if (!isCounting) return;

        timer -= Time.unscaledDeltaTime;

        if (timer > 0)
        {
            int seconds = Mathf.CeilToInt(timer);

            if (seconds != lastSecondPlayed)
            {
                if (audioSource != null && beepSound != null)
                    audioSource.PlayOneShot(beepSound);
                lastSecondPlayed = seconds;
            }

            if (countdownText != null)
            {
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
            if (audioSource != null && goSound != null)
                audioSource.PlayOneShot(goSound);

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