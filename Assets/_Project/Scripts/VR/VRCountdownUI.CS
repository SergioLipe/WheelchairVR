using UnityEngine;
using TMPro;
using System;

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
    
    // Variável para garantir que o som só toca uma vez por cada segundo
    private int lastSecondPlayed = -1;

    private void OnEnable()
    {
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated += ShowHiddenObjects;
        }
    }

    private void OnDisable()
    {
        if (seatCalibrator != null)
        {
            seatCalibrator.OnCalibrated -= ShowHiddenObjects;
        }
    }

    private void Start()
    {
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }

        // Desligar os scripts por completo no início
        if (movementScript != null)
        {
            movementScript.enabled = false;
        }

        if (collisionSystem != null)
        {
            collisionSystem.enabled = false;
        }

        // Força o Canvas a aparecer
        if (handManager != null)
        {
            handManager.currentMode = HandVisibilityManager.GameMode.Countdown;
        }

        timer = countdownTime;
        isCounting = true;
        
        // Força o primeiro som a tocar mal o script arranca
        lastSecondPlayed = Mathf.CeilToInt(countdownTime) + 1;

        if (countdownText != null)
            countdownText.alignment = TextAlignmentOptions.Center;
    }

    private void ShowHiddenObjects()
    {
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
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

                // LIGA OS SCRIPTS DE VOLTA QUANDO ACABA O COUNTDOWN
                if (movementScript != null)
                {
                    movementScript.enabled = true;
                }

                if (collisionSystem != null)
                {
                    collisionSystem.enabled = true;
                }

                // Volta ao normal (esconde o Canvas)
                if (handManager != null)
                {
                    handManager.currentMode = HandVisibilityManager.GameMode.PlayingLevel;
                }

                OnCountdownFinished?.Invoke();
            }
            return;
        }

        if (!isCounting) return;

        timer -= Time.unscaledDeltaTime;

        if (timer > 0)
        {
            int seconds = Mathf.CeilToInt(timer);

            // ---> LÓGICA DO ÁUDIO PARA OS NÚMEROS (3, 2, 1) <---
            if (seconds != lastSecondPlayed)
            {
                if (audioSource != null && beepSound != null)
                {
                    audioSource.PlayOneShot(beepSound);
                }
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
            // ---> LÓGICA DO ÁUDIO PARA O COMEÇO DO NÍVEL (Vamos lá!) <---
            if (audioSource != null && goSound != null)
            {
                audioSource.PlayOneShot(goSound);
            }

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