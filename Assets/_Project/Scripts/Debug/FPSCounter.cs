using UnityEngine;
using TMPro;

/// <summary>
/// Simple FPS display for VR testing.
/// Can be toggled on/off via the Inspector to save performance.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [Header("=== Settings ===")]
    [Tooltip("Uncheck this to completely hide the FPS and save performance")]
    public bool showFPS = true;

    [Header("=== UI Reference ===")]
    public TMP_Text fpsText;

    private float deltaTime = 0.0f;

    void Update()
    {
        // 1. Se estiver desligado, esconde o texto e pára as contas.
        if (!showFPS)
        {
            if (fpsText != null && fpsText.gameObject.activeSelf)
            {
                fpsText.gameObject.SetActive(false); 
            }
            return; 
        }

        // 2. Garante que o texto está visível se estiver ligado
        if (fpsText != null && !fpsText.gameObject.activeSelf)
        {
            fpsText.gameObject.SetActive(true); 
        }

        // 3. Matemática para calcular os FPS reais
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float currentFPS = 1.0f / deltaTime;

        // 4. Atualizar o ecrã com as cores de aviso
        if (fpsText != null)
        {
            fpsText.text = $"FPS: {Mathf.Ceil(currentFPS)}";

            if (currentFPS >= 72f)
            {
                fpsText.color = Color.green; // Perfeito para VR
            }
            else if (currentFPS >= 60f)
            {
                fpsText.color = Color.yellow; // Aceitável, mas com quebras
            }
            else
            {
                fpsText.color = Color.red; // Perigo de motion sickness
            }
        }
    }
}