using UnityEngine;
using TMPro; // Adicionado para suportar o TextMeshPro!

/// <summary>
/// VR Dashboard UI Controller using TextMeshPro
/// </summary>
public class VRDashboardUI : MonoBehaviour
{
    [Header("=== Core References ===")]
    public MovementVR wheelchairController;

    [Header("=== UI Text References (TextMeshPro) ===")]
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI steeringText;
    
    [Header("=== UI Panels ===")]
    public GameObject emergencyBrakePanel;

    void Update()
    {
        if (wheelchairController == null) return;

        UpdateModeDisplay();
        UpdateSpeedDisplay();
        UpdateSteeringDisplay();
        UpdateBrakeWarning();
    }

    private void UpdateModeDisplay()
    {
        if (modeText == null) return;

        string modeString = "";
        Color modeColor = Color.white;

        switch (wheelchairController.currentMode)
        {
            case MovementVR.SpeedMode.Slow:
                modeString = "Interior";
                modeColor = new Color(1f, 0.9f, 0.5f, 1f); 
                break;
            case MovementVR.SpeedMode.Normal:
                modeString = "Exterior";
                modeColor = new Color(0.6f, 1f, 0.7f, 1f); 
                break;
            case MovementVR.SpeedMode.Off:
                modeString = "Desligado";
                modeColor = new Color(1f, 0.6f, 0.6f, 1f); 
                break;
        }

        modeText.text = $"Modo: {modeString}";
        modeText.color = modeColor;
    }

    private void UpdateSpeedDisplay()
    {
        if (speedText == null) return;

        float currentSpeedKmH = wheelchairController.GetCurrentSpeed() * 3.6f;
        float maxSpeedLimit = wheelchairController.currentMode == MovementVR.SpeedMode.Slow ? 3f : 8f;

        speedText.text = $"Veloc: {currentSpeedKmH:F1} / {maxSpeedLimit:F0} km/h";
    }

    private void UpdateSteeringDisplay()
    {
        if (steeringText == null) return;

        string steerType = wheelchairController.GetCurrentSteeringType();
        bool isRear = steerType.Contains("Rear");

        string steerString = isRear ? "Traseira" : "Frontal";
        Color steerColor = isRear ? new Color(1f, 0.75f, 1f, 1f) : new Color(0.65f, 0.95f, 1f, 1f);

        steeringText.text = $"Direção: {steerString}";
        steeringText.color = steerColor;
    }

    private void UpdateBrakeWarning()
    {
        if (emergencyBrakePanel == null) return;

        bool isBraking = wheelchairController.IsEmergencyBraking();
        if (emergencyBrakePanel.activeSelf != isBraking)
        {
            emergencyBrakePanel.SetActive(isBraking);
        }
    }
}