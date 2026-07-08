using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;

/// <summary>
/// Painel de calibracao do input (PC).
/// Le as definicoes do perfil ativo, mostra os sliders do modo escolhido,
/// escreve de volta no perfil, grava em disco e reaplica ao MovementPC em cena.
/// Nao sabe nada de timers/estrelas: trata so da calibracao.
/// </summary>
public class CalibrationMenu : MonoBehaviour
{
    [Header("--- Paineis ---")]
    [Tooltip("O proprio painel de calibracao (este objeto, ou o painel raiz)")]
    public GameObject calibrationPanel;
    [Tooltip("O painel de pausa, para onde o botao Voltar regressa")]
    public GameObject pausePanel;

    [Header("--- Escolha de modo ---")]
    [Tooltip("Dropdown com: 0=Teclado, 1=Rato/Rock, 2=Comando")]
    public TMP_Dropdown modeDropdown;

    [Header("--- Grupos de sliders por modo ---")]
    [Tooltip("Caixa com os sliders do modo Rato/Rock")]
    public GameObject rockGroup;
    [Tooltip("Caixa com os sliders do modo Comando")]
    public GameObject comandoGroup;

    [Header("--- Sliders: Rato/Rock ---")]
    public Slider rockSensitivity;
    public Slider rockTurnStrength;
    public Slider rockDeadzone;

    [Header("--- Sliders: Comando ---")]
    public Slider comandoSensitivity;
    public Slider comandoTurnStrength;
    public Slider comandoDeadzone;

    [Header("--- Textos de valor (numero ao vivo) ---")]
    public TMP_Text rockSensitivityValue;
    public TMP_Text rockTurnStrengthValue;
    public TMP_Text rockDeadzoneValue;
    public TMP_Text comandoSensitivityValue;
    public TMP_Text comandoTurnStrengthValue;
    public TMP_Text comandoDeadzoneValue;

    // Evita que os sliders gravem enquanto os estamos a preencher por codigo
    private bool isLoading = false;

    // Chamado pelo botao "Calibracao" do menu de pausa (via LevelManager) ou ao ativar o painel
    // Chamado pelo botao "Calibracao" do menu de pausa ou ao ativar o painel
    void OnEnable()
    {
        LoadFromProfile();
        RefreshLocalizedTexts();   // [LOCALIZAÇÃO] força os textos a reaplicar o idioma atual
    }

    // [LOCALIZAÇÃO] Percorre todos os Localize String Event deste painel e força refresh
    private void RefreshLocalizedTexts()
    {
        LocalizeStringEvent[] localizers = GetComponentsInChildren<LocalizeStringEvent>(true);
        foreach (LocalizeStringEvent loc in localizers)
        {
            if (loc != null) loc.RefreshString();
        }
    }

    private InputSettings GetSettings()
    {
        if (ProfileManager.Instance == null) return null;
        if (ProfileManager.Instance.currentPlayer == null) return null;
        return ProfileManager.Instance.currentPlayer.inputSettings;
    }

    // ---- Encher os controlos com os valores guardados ----
    public void LoadFromProfile()
    {
        InputSettings s = GetSettings();
        if (s == null) return;

        isLoading = true;

        if (modeDropdown != null) modeDropdown.value = s.inputMode;

        if (rockSensitivity != null) rockSensitivity.value = s.rockSensitivity;
        if (rockTurnStrength != null) rockTurnStrength.value = s.rockTurnStrength;
        if (rockDeadzone != null) rockDeadzone.value = s.rockDeadzone;

        if (comandoSensitivity != null) comandoSensitivity.value = s.comandoSensitivity;
        if (comandoTurnStrength != null) comandoTurnStrength.value = s.comandoTurnStrength;
        if (comandoDeadzone != null) comandoDeadzone.value = s.comandoDeadzone;

        isLoading = false;

        ShowGroupForMode(s.inputMode);

        UpdateValueLabels();
    }

    // Mostra so o grupo de sliders do modo ativo (definicoes por modo)
    private void ShowGroupForMode(int mode)
    {
        if (rockGroup != null) rockGroup.SetActive(mode == 1);
        if (comandoGroup != null) comandoGroup.SetActive(mode == 2);
        // modo 0 (Teclado) nao tem sliders: ambos escondidos
    }

    // ---- Ligado ao OnValueChanged do dropdown ----
    public void OnModeChanged(int newMode)
    {
        if (isLoading) return;
        InputSettings s = GetSettings();
        if (s == null) return;

        s.inputMode = newMode;
        ShowGroupForMode(newMode);
        WriteSlidersTo(s);

        // aplicar logo ao MovementPC, sem esperar pelo Guardar
        MovementPC mover = FindObjectOfType<MovementPC>();
        if (mover != null) mover.ApplyInputSettings();

        FreeLookCamera cam = FindObjectOfType<FreeLookCamera>();
        if (cam != null) cam.AplicarModoCamera();
    }

    // ---- Ligado ao OnValueChanged de TODOS os sliders ----
    public void OnSliderChanged()
    {
        if (isLoading) return;
        InputSettings s = GetSettings();
        if (s == null) return;
        WriteSlidersTo(s);
        UpdateValueLabels();
        SaveAndApply();      // <-- é esta linha que aplica ao carro
    }

    private void WriteSlidersTo(InputSettings s)
    {
        if (rockSensitivity != null) s.rockSensitivity = rockSensitivity.value;
        if (rockTurnStrength != null) s.rockTurnStrength = rockTurnStrength.value;
        if (rockDeadzone != null) s.rockDeadzone = rockDeadzone.value;

        if (comandoSensitivity != null) s.comandoSensitivity = comandoSensitivity.value;
        if (comandoTurnStrength != null) s.comandoTurnStrength = comandoTurnStrength.value;
        if (comandoDeadzone != null) s.comandoDeadzone = comandoDeadzone.value;
    }


    // ---- Botao VOLTAR (regressa ao menu de pausa) ----
    public void Button_Back()
    {
        if (calibrationPanel != null) calibrationPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Button_Reset()
    {
        InputSettings s = GetSettings();
        if (s == null) return;

        // Repoe so o modo ativo, nos valores de fabrica
        if (s.inputMode == 1) // Rato/Rock
        {
            s.rockSensitivity = 4f;
            s.rockTurnStrength = 0.7f;
            s.rockDeadzone = 0.05f;
        }
        else if (s.inputMode == 2) // Comando
        {
            s.comandoSensitivity = 1f;
            s.comandoTurnStrength = 0.7f;
            s.comandoDeadzone = 0.15f;
        }
        // modo Teclado nao tem calibracao

        LoadFromProfile();   // reenche os sliders com os novos valores (e os numeros)
        SaveAndApply();      // grava e aplica na hora
    }

    private void SaveAndApply()
    {
        if (ProfileManager.Instance == null || ProfileManager.Instance.currentPlayer == null) return;

        SaveManager.SaveProfile(ProfileManager.Instance.currentPlayer);

        MovementPC mover = FindObjectOfType<MovementPC>();
        if (mover != null) mover.ApplyInputSettings();

        FreeLookCamera cam = FindObjectOfType<FreeLookCamera>();
        if (cam != null) cam.AplicarModoCamera();
    }


    private void UpdateValueLabels()
    {
        if (rockSensitivityValue != null) rockSensitivityValue.text = rockSensitivity.value.ToString("0.0");
        if (rockTurnStrengthValue != null) rockTurnStrengthValue.text = rockTurnStrength.value.ToString("0.00");
        if (rockDeadzoneValue != null) rockDeadzoneValue.text = rockDeadzone.value.ToString("0.00");
        if (comandoSensitivityValue != null) comandoSensitivityValue.text = comandoSensitivity.value.ToString("0.0");
        if (comandoTurnStrengthValue != null) comandoTurnStrengthValue.text = comandoTurnStrength.value.ToString("0.00");
        if (comandoDeadzoneValue != null) comandoDeadzoneValue.text = comandoDeadzone.value.ToString("0.00");
    }
}