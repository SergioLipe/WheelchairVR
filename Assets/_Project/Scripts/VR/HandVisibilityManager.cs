using UnityEngine;

public class HandVisibilityManager : MonoBehaviour
{
    public enum GameMode 
    { 
        PauseMenu,      // Menu de Pausa
        OtherMenus,     // Resto dos Menus (ex: Principal)
        PlayingLevel,   // A jogar o nível
        Countdown       // ---> NOVO: Durante a contagem inicial
    }

    [Header("Current State")]
    public GameMode currentMode = GameMode.OtherMenus;

    [Header("References")]
    public Transform mainCameraTransform;

    [Header("Left Hand")]
    public GameObject leftControllerUI;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor leftLaser;

    [Header("Right Hand")]
    public GameObject rightControllerUI;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightLaser;

    [Header("Settings")]
    public float lookDownAngle = 60f; 

    void Update()
    {
        // Verifica se o jogador está a olhar para baixo (ângulo com o chão)
        float angleToFloor = Vector3.Angle(mainCameraTransform.forward, Vector3.down);
        bool isLookingDown = angleToFloor < lookDownAngle;

        bool shouldShowCanvas = false;
        bool shouldShowLaser = false;

        // AS TUAS REGRAS EXATAS:
        if (currentMode == GameMode.PauseMenu)
        {
            // 1. Menu de Pausa: Canvas aparece SEMPRE (Lasers ligados)
            shouldShowCanvas = true;
            shouldShowLaser = true;
        }
        else if (currentMode == GameMode.OtherMenus)
        {
            // 2. Resto dos Menus: Canvas SÓ aparece a olhar para baixo (Lasers ligados)
            shouldShowCanvas = isLookingDown;
            shouldShowLaser = true;
        }
        else if (currentMode == GameMode.PlayingLevel)
        {
            // 3. A jogar o nível: Canvas SÓ aparece a olhar para baixo (Lasers desligados)
            shouldShowCanvas = isLookingDown;
            shouldShowLaser = false;
        }
        else if (currentMode == GameMode.Countdown)
        {
            // 4. ---> NOVO: Durante a contagem, Canvas aparece SEMPRE (Lasers desligados)
            shouldShowCanvas = true;
            shouldShowLaser = false;
        }

        // Aplicar as regras
        ApplyVisibility(shouldShowCanvas, shouldShowLaser);
    }

    void ApplyVisibility(bool showCanvas, bool showLaser)
    {
        // Controlar Canvas
        if (leftControllerUI != null) leftControllerUI.SetActive(showCanvas);
        if (rightControllerUI != null) rightControllerUI.SetActive(showCanvas);

        // Controlar Lasers
        if (leftLaser != null)
        {
            leftLaser.enabled = showLaser;
            
            var lineL = leftLaser.GetComponent<LineRenderer>();
            if (lineL != null) lineL.enabled = showLaser;

            // ---> O SEGREDO PARA A BOLINHA (RETICLE) DESAPARECER <---
            var lineVisualL = leftLaser.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
            if (lineVisualL != null) lineVisualL.enabled = showLaser;
        }

        if (rightLaser != null)
        {
            rightLaser.enabled = showLaser;
            
            var lineR = rightLaser.GetComponent<LineRenderer>();
            if (lineR != null) lineR.enabled = showLaser;

            var lineVisualR = rightLaser.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
            if (lineVisualR != null) lineVisualR.enabled = showLaser;
        }
    }
}