using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Troca o idioma do jogo em runtime (via botão) e guarda a escolha do jogador.
/// Suporta VÁRIOS botões/labels — todos mostram o idioma alternativo e
/// mantêm-se sincronizados quando o idioma muda.
///
/// LIGAR AOS BOTÕES:
/// - Cada botão de idioma -> OnClick = ToggleLanguage()
/// - Arrasta o Text (TMP) de cada botão para a lista "Button Labels"
/// </summary>
public class LanguageSwitcher : MonoBehaviour
{
    [Header("--- Códigos dos locales ---")]
    public string portugueseCode = "pt-PT";
    public string englishCode = "en";

    [Header("--- Visual dos botões (opcional) ---")]
    [Tooltip("Arrasta para aqui o Text (TMP) de CADA botão de idioma. Todos mostram o idioma alternativo.")]
    public TMP_Text[] buttonLabels;

    [Tooltip("Texto a mostrar quando o jogo está em inglês (clica para ir p/ PT)")]
    public string labelWhenEnglish = "PT";

    [Tooltip("Texto a mostrar quando o jogo está em português (clica para ir p/ EN)")]
    public string labelWhenPortuguese = "EN";

    private const string PrefKey = "PlayerLanguageChoice";

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        if (PlayerPrefs.HasKey(PrefKey))
        {
            string savedCode = PlayerPrefs.GetString(PrefKey);
            ApplyLocale(savedCode);
        }

        UpdateButtonLabels();
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        UpdateButtonLabels();
    }

    /// <summary>Alterna PT<->EN. Liga a CADA botão de idioma.</summary>
    public void ToggleLanguage()
    {
        Locale current = LocalizationSettings.SelectedLocale;
        if (current == null) return;

        string currentCode = current.Identifier.Code;

        if (currentCode.StartsWith("pt"))
            ApplyLocale(englishCode);
        else
            ApplyLocale(portugueseCode);
    }

    public void SetPortuguese() => ApplyLocale(portugueseCode);
    public void SetEnglish() => ApplyLocale(englishCode);

    private void ApplyLocale(string code)
    {
        Locale target = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (target == null)
        {
            Debug.LogWarning($"[LanguageSwitcher] Locale '{code}' não encontrado.");
            return;
        }

        LocalizationSettings.SelectedLocale = target;
        PlayerPrefs.SetString(PrefKey, code);
        PlayerPrefs.Save();

        Debug.Log($"[LanguageSwitcher] Idioma definido para: {code}");
    }

    // Atualiza TODOS os labels dos botões para mostrar o idioma alternativo
    private void UpdateButtonLabels()
    {
        if (buttonLabels == null || buttonLabels.Length == 0) return;

        Locale current = LocalizationSettings.SelectedLocale;
        if (current == null) return;

        bool isPortuguese = current.Identifier.Code.StartsWith("pt");
        string text = isPortuguese ? labelWhenPortuguese : labelWhenEnglish;

        foreach (TMP_Text label in buttonLabels)
        {
            if (label != null) label.text = text;
        }
    }

    public void ResetToSystemLanguage()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        Debug.Log("[LanguageSwitcher] Escolha apagada.");
    }
}