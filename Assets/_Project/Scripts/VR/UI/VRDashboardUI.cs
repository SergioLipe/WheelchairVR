using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;   // <-- NOVO: para localização

/// <summary>
/// Professional VR Dashboard UI Controller
/// Manages the Left Tablet (Stats: Time, Collisions, Slides) 
/// and the Right Tablet (Mode, Speed, replaced dynamically by Emergency Brake).
/// Uses TextMeshPro Rich Text for a clean, modern, and readable layout.
/// 
/// [LOCALIZAÇÃO] As labels (Colisões, Deslizes, TRAVÃO, INTERIOR, etc.) são
/// carregadas da String Table "GameText" uma vez (no início e quando o idioma
/// muda), e guardadas em variáveis para o Update não perder performance.
/// </summary>
public class VRDashboardUI : MonoBehaviour
{
    [Header("=== Core References ===")]
    public MovementVR wheelchairController;
    public CollisionSystemVR collisionSystem;

    [Header("=== Countdown Reference ===")]
    [Tooltip("Drag the object with the VRCountdownUI script here")]
    public VRCountdownUI countdownScript;

    [Header("=== Left Dashboard (Stats) ===")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI collisionsText;
    public TextMeshProUGUI slidesText;

    [Header("=== Right Dashboard (Mode, Speed & Brake) ===")]
    public TextMeshProUGUI modeText;

    [Tooltip("Drag the new TextMeshPro for Speed here")]
    public TextMeshProUGUI speedText;

    [Tooltip("Format for the speed number. '0.0' for one decimal, '0' for whole numbers.")]
    public string speedFormat = "0.0";

    // --- Custom Timer Variables ---
    private float timeElapsed = 0f;
    private bool isTimerRunning = false;

    // ==========================================================
    // [LOCALIZAÇÃO] Cache das strings traduzidas
    // Buscadas uma vez (não a cada frame) para não perder performance
    // ==========================================================
    private const string TABLE = "GameText";   // nome da tua String Table

    private string lblColisoes  = "Colisões";   // fallback caso a tabela falhe
    private string lblDeslizes  = "Deslizes";
    private string lblTravao    = "TRAVÃO";
    private string lblInterior  = "INTERIOR";
    private string lblExterior  = "EXTERIOR";
    private string lblDesligado = "DESLIGADO";

    private void OnEnable()
    {
        // Start listening to the countdown script
        if (countdownScript != null)
        {
            countdownScript.OnCountdownFinished += StartTimer;
        }
        else
        {
            isTimerRunning = true;
        }

        // [LOCALIZAÇÃO] Carregar as labels traduzidas e re-carregar se o idioma mudar
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RefreshLabels();
    }

    private void OnDisable()
    {
        if (countdownScript != null)
        {
            countdownScript.OnCountdownFinished -= StartTimer;
        }

        // [LOCALIZAÇÃO] Deixar de ouvir mudanças de idioma
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    // [LOCALIZAÇÃO] Chamado automaticamente quando o jogador/sistema muda de idioma
    private void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
    {
        RefreshLabels();
    }

    // [LOCALIZAÇÃO] Vai buscar cada label à String Table e guarda em variável
    private void RefreshLabels()
    {
        // GetLocalizedString devolve o texto no idioma atualmente selecionado.
        // Se a key não existir, o Unity devolve um aviso mas não rebenta.
        lblColisoes  = SafeGet("colisoes",  lblColisoes);
        lblDeslizes  = SafeGet("deslizes",  lblDeslizes);
        lblTravao    = SafeGet("travao",    lblTravao);
        lblInterior  = SafeGet("interior",  lblInterior);
        lblExterior  = SafeGet("exterior",  lblExterior);
        lblDesligado = SafeGet("desligado", lblDesligado);
    }

    // Helper: busca uma string da tabela; se falhar, devolve o fallback
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

    private void StartTimer()
    {
        isTimerRunning = true;
    }

    void Update()
    {
        if (wheelchairController == null || collisionSystem == null) return;

        UpdateTimerDisplay();
        UpdateStatsDisplay();
        UpdateRightDashboardDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timeText == null) return;

        if (isTimerRunning)
        {
            timeElapsed += Time.deltaTime;
        }

        int minutes = Mathf.FloorToInt(timeElapsed / 60f);
        int seconds = Mathf.FloorToInt(timeElapsed % 60f);

        // O tempo "00:00" não se traduz (números universais)
        timeText.text = $"<size=130%><b>{minutes:00}:{seconds:00}</b></size>";
    }

    private void UpdateStatsDisplay()
    {
        if (collisionsText != null)
        {
            // [LOCALIZAÇÃO] usa a label traduzida + o número
            collisionsText.text = $"{lblColisoes}: <color=#FF4D4D><b>{collisionSystem.TotalCollisions}</b></color>";
        }

        if (slidesText != null)
        {
            slidesText.text = $"{lblDeslizes}: <color=#FFB84D><b>{collisionSystem.TotalSlides}</b></color>";
        }
    }

    private void UpdateRightDashboardDisplay()
    {
        // --- CÁLCULO DA VELOCIDADE ---
        float currentKmh = Mathf.Abs(wheelchairController.GetCurrentSpeed()) * 3.6f;
        string speedString = $"{currentKmh.ToString(speedFormat)} km/h"; // km/h não se traduz

        // 1. Check if the emergency brake is active first
        if (wheelchairController.IsEmergencyBraking())
        {
            if (modeText != null)
            {
                // [LOCALIZAÇÃO] TRAVÃO traduzido
                modeText.text = $"<size=150%><b>{lblTravao}</b></size>";
                modeText.color = new Color(1f, 0.2f, 0.2f, 1f);
            }

            if (speedText != null)
            {
                speedText.text = "0.0 km/h";
            }
            return;
        }

        // 2. If no brake is applied, show the current speed mode
        if (modeText != null)
        {
            string modeString = "";
            Color modeColor = Color.white;

            switch (wheelchairController.currentMode)
            {
                case MovementVR.SpeedMode.Slow:
                    // [LOCALIZAÇÃO] INTERIOR traduzido
                    modeString = $"<size=150%><b>{lblInterior}</b></size>";
                    modeColor = new Color(1f, 0.9f, 0.5f, 1f);
                    break;
                case MovementVR.SpeedMode.Normal:
                    modeString = $"<size=150%><b>{lblExterior}</b></size>";
                    modeColor = new Color(0.6f, 1f, 0.7f, 1f);
                    break;
                case MovementVR.SpeedMode.Off:
                    modeString = $"<size=150%><b>{lblDesligado}</b></size>";
                    modeColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                    break;
            }

            modeText.text = modeString;
            modeText.color = modeColor;
        }

        // 3. Atualiza o texto da velocidade
        if (speedText != null)
        {
            speedText.text = speedString;
        }
    }
}