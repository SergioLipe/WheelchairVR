using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System; // Required to format the date

/// <summary>
/// Manages the Main Menu dynamically.
/// Includes Profile Selection, Multi-step History, and current user display.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("--- Main UI Elements ---")]
    [Tooltip("Drag the main game title text here so it hides during history")]
    public GameObject mainTitle; // Reference to hide the main title

    [Header("--- Profile UI Elements ---")]
    public GameObject profileSelectionPanel;
    public GameObject levelSelectionPanel;
    public TMP_InputField inputFieldProfileID;
    public Button btnLogin;
    public TMP_Text txtCurrentProfile;

    [Header("--- History UI Elements ---")]
    public TMP_Dropdown dropdownProfiles;
    public Button btnViewHistory;

    [Header("--- History: Levels Panel ---")]
    public GameObject historyLevelsPanel;
    public Button btnCloseHistory;
    [Tooltip("Drag the history level buttons here in order (Level 1, Level 2...)")]
    public Button[] historyLevelButtons;

    [Header("--- History: Attempts Panel ---")]
    public GameObject historyAttemptsPanel;
    public TMP_Text txtHistoryDetails;
    public Button btnBackToHistLevels;

    [Header("--- Main Game Level Buttons ---")]
    public Button[] levelButtons;

    [Header("--- UI Colors ---")]
    public Color unlockedBGColor = new Color(0f, 0.78f, 0.32f, 1f); 
    public Color freeStyleColor = new Color(0f, 0.6f, 1f, 1f); 
    public Color lockedBGColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); 
    public Color starEarnedColor = new Color(1f, 0.84f, 0f, 1f); 
    public Color starEmptyColor = new Color(0f, 0f, 0f, 0.4f); 

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 1. Setup Initial Panels Visibility
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);

        // 2. Main Login Buttons
        if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginButtonClicked);

        // 3. Setup Dropdown
        if (dropdownProfiles != null) dropdownProfiles.onValueChanged.AddListener(OnDropdownValueChanged);
        LoadDropdownProfiles();

        // 4. Setup History Navigation Buttons
        if (btnViewHistory != null) btnViewHistory.onClick.AddListener(OpenHistoryLevels);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(CloseHistory);
        if (btnBackToHistLevels != null) btnBackToHistLevels.onClick.AddListener(BackToHistoryLevels);

        // 5. Setup Main Game Levels
        InitializeAllLevels();
    }

    private void LateUpdate()
    {
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // ==========================================
    // LOGIN & PROFILES SYSTEM
    // ==========================================

    private void LoadDropdownProfiles()
    {
        if (dropdownProfiles == null) return;
        dropdownProfiles.ClearOptions();
        
        string[] savedIDs = SaveManager.GetAllProfileIDs();
        List<string> options = new List<string>();

        if (savedIDs == null || savedIDs.Length == 0)
        {
            options.Add("Nenhum perfil encontrado");
            dropdownProfiles.AddOptions(options);
            dropdownProfiles.interactable = false;
            if (btnViewHistory != null) btnViewHistory.interactable = false;
        }
        else
        {
            options.AddRange(savedIDs);
            dropdownProfiles.AddOptions(options);
            dropdownProfiles.interactable = true;
            if (btnViewHistory != null) btnViewHistory.interactable = true;
            OnDropdownValueChanged(0);
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        if (!dropdownProfiles.interactable || dropdownProfiles.options.Count == 0) return;
        string selectedID = dropdownProfiles.options[index].text;

        if (inputFieldProfileID != null)
        {
            inputFieldProfileID.text = selectedID;
        }
    }

    public void OnLoginButtonClicked()
    {
        string typedID = inputFieldProfileID.text.Trim();

        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        PlayerData loadedData = SaveManager.LoadProfile(typedID);

        if (loadedData != null) ProfileManager.Instance.SetActiveProfile(loadedData);
        else
        {
            PlayerData newData = new PlayerData();
            newData.profileID = typedID;
            SaveManager.SaveProfile(newData);
            ProfileManager.Instance.SetActiveProfile(newData);
        }

        if (txtCurrentProfile != null) txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
    }

    // ==========================================
    // HISTORY SYSTEM
    // ==========================================

    /// <summary>
    /// Opens the History Levels panel and enables buttons only for played levels
    /// </summary>
    public void OpenHistoryLevels()
    {
        if (!dropdownProfiles.interactable || dropdownProfiles.options.Count == 0) return;

        string selectedID = dropdownProfiles.options[dropdownProfiles.value].text;
        PlayerData data = SaveManager.LoadProfile(selectedID);

        if (data == null) return;

        profileSelectionPanel.SetActive(false);
        historyLevelsPanel.SetActive(true);

        // Hide main title
        if (mainTitle != null) mainTitle.SetActive(false);

        // Check which levels the patient has played to enable/disable buttons
        for (int i = 0; i < historyLevelButtons.Length; i++)
        {
            if (historyLevelButtons[i] == null) continue;

            int levelID = i + 1;
            string targetLevelName = "Level" + levelID;

            bool hasPlayedThisLevel = false;
            
            // Search the history to see if this level exists
            foreach (SessionRecord record in data.sessionHistory)
            {
                if (record.levelName == targetLevelName)
                {
                    hasPlayedThisLevel = true;
                    break;
                }
            }

            Button btn = historyLevelButtons[i];
            btn.interactable = hasPlayedThisLevel;

            // Clear old clicks and add new one
            btn.onClick.RemoveAllListeners();
            if (hasPlayedThisLevel)
            {
                PlayerData capturedData = data;
                string capturedLevelName = targetLevelName;
                btn.onClick.AddListener(() => ShowAttemptsForLevel(capturedData, capturedLevelName));
            }
        }
    }

    /// <summary>
    /// Displays the list of attempts for a specific level with Date and Time
    /// </summary>
    public void ShowAttemptsForLevel(PlayerData data, string levelName)
    {
        historyLevelsPanel.SetActive(false);
        historyAttemptsPanel.SetActive(true);

        // CLEAN HEADER (Only Name | Level)
        string historyText = $"<align=center><size=130%><b><color=#FFFFFF>{data.profileID}   |   {levelName}</color></b></size></align>\n";
        historyText += $"<align=center><color=#666666>_________________________________________</color></align>\n\n";

        int attemptCount = 1;
        
        // Reverse to show the most recent first
        List<SessionRecord> records = new List<SessionRecord>(data.sessionHistory);
        records.Reverse();

        foreach (SessionRecord record in records)
        {
            if (record.levelName == levelName)
            {
                // Format the date nicely
                string formattedDate = record.sessionDate;
                if (DateTime.TryParse(record.sessionDate, out DateTime parsedDate))
                {
                    formattedDate = parsedDate.ToString("dd/MM/yyyy 'às' HH:mm");
                }

                historyText += $"<color=#A0E4FF><b>TENTATIVA {attemptCount}</b></color>   <color=#AAAAAA>•   {formattedDate}</color>\n";
                historyText += $"Tempo: <b>{record.completionTime:F1}s</b>   |   Colisões: <color=red><b>{record.totalCollisions}</b></color>   |   Deslizes: <color=yellow><b>{record.totalSlides}</b></color>\n";
                historyText += "<color=#444444>--------------------------------------------------------</color>\n\n";
                
                attemptCount++;
            }
        }

        txtHistoryDetails.text = historyText;
    }

    public void CloseHistory()
    {
        historyLevelsPanel.SetActive(false);
        profileSelectionPanel.SetActive(true);

        // Show main title again
        if (mainTitle != null) mainTitle.SetActive(true);
    }

    public void BackToHistoryLevels()
    {
        historyAttemptsPanel.SetActive(false);
        historyLevelsPanel.SetActive(true);
    }

    // ==========================================
    // MAIN GAME LEVELS LOGIC
    // ==========================================

    private void InitializeAllLevels()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;

            int levelID = i + 1; 

            string saveKey = "Level_" + levelID + "_Stars";
            int currentStars = PlayerPrefs.GetInt(saveKey, 0);

            int prevStars = 0;
            if (levelID > 1)
            {
                prevStars = PlayerPrefs.GetInt("Level_" + (levelID - 1) + "_Stars", 0);
            }

            bool isFreeStyleLevel = (i == levelButtons.Length - 1);
            bool isUnlocked = (levelID == 1) || isFreeStyleLevel || (prevStars >= 1) || (PlayerPrefs.GetInt("UnlockAll", 0) == 1);
            
            Button btn = levelButtons[i];
            Image bgImage = btn.GetComponent<Image>();
            TMP_Text levelText = btn.GetComponentInChildren<TMP_Text>();
            Transform starContainer = btn.transform.Find("StarContainer");

            btn.interactable = isUnlocked;

            if (isUnlocked)
            {
                if (bgImage != null)
                {
                    if (i == levelButtons.Length - 1) bgImage.color = freeStyleColor;
                    else bgImage.color = unlockedBGColor;
                }

                if (levelText != null) levelText.color = Color.white;

                if (starContainer != null)
                {
                    starContainer.gameObject.SetActive(true);
                    Image[] stars = starContainer.GetComponentsInChildren<Image>();

                    for (int s = 0; s < stars.Length; s++)
                    {
                        if (s < currentStars) stars[s].color = starEarnedColor;
                        else stars[s].color = starEmptyColor;
                    }
                }

                int captureID = levelID;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => LoadGameLevel(captureID));
            }
            else
            {
                if (bgImage != null) bgImage.color = lockedBGColor;
                if (levelText != null) levelText.color = Color.gray;
                if (starContainer != null) starContainer.gameObject.SetActive(false);
            }
        }
    }

    public void LoadGameLevel(int levelNumber)
    {
        string sceneName = "Level" + levelNumber;
        Debug.Log($"A carregar a cena: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        InitializeAllLevels(); 
    }

    public void UnlockAllLevels()
    {
        PlayerPrefs.SetInt("UnlockAll", 1);
        InitializeAllLevels();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}