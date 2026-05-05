using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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

    [Tooltip("Subtitle text under 'HISTÓRICO DE SESSÕES' that shows the patient name dynamically")]
    public TMP_Text txtHistorySubtitle;

    [Tooltip("Optional: Freestyle history button (shown separately under the level grid)")]
    public Button btnHistFreestyle;

    [Tooltip("Optional: Text inside the freestyle history button that shows the number of sessions")]
    public TMP_Text txtHistFreestyleCount;

    [Header("--- History: Attempts Panel ---")]
    public GameObject historyAttemptsPanel;
    public TMP_Text txtHistoryDetails;
    public Button btnBackToHistLevels;

    [Header("--- History: Attempts Panel Header ---")]
    [Tooltip("Patient name shown in the badge at the top of the attempts panel")]
    public TMP_Text txtPatientName;

    [Tooltip("Level title (e.g. 'Level 1') at the top of the attempts panel")]
    public TMP_Text txtLevelTitle;

    [Tooltip("Number of attempts (e.g. '5 tentativas registadas')")]
    public TMP_Text txtAttemptsCount;

    [Header("--- Main Game Level Buttons ---")]
    public Button[] levelButtons;

    [Header("--- UI Colors ---")]
    public Color unlockedBGColor = new Color(0f, 0.78f, 0.32f, 1f);
    public Color freeStyleColor = new Color(0f, 0.6f, 1f, 1f);
    public Color lockedBGColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color starEarnedColor = new Color(1f, 0.84f, 0f, 1f);
    public Color starEmptyColor = new Color(0f, 0f, 0f, 0.4f);

    [Header("--- History Button Colors ---")]
    [Tooltip("Color used for level buttons that have session records")]
    public Color historyHasRecordsColor = new Color(0.086f, 0.639f, 0.290f, 1f); // #16A34A green

    [Tooltip("Color used for level buttons with no session records (faded/grayed out)")]
    public Color historyNoRecordsColor = new Color(0.32f, 0.32f, 0.32f, 0.5f); // gray faded

    private void Start()
    {
        // Force cursor visible immediately
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Start safety coroutine that runs at the end of every frame
        // This guarantees the cursor stays visible no matter what other scripts do
        StartCoroutine(KeepCursorVisible());

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

    /// <summary>
    /// Coroutine that runs at the END of every frame (after all Update and LateUpdate calls).
    /// This forces the cursor to stay visible even if XR or other scripts try to hide it.
    /// </summary>
    private IEnumerator KeepCursorVisible()
    {
        WaitForEndOfFrame wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
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
    /// Opens the History Levels panel and enables buttons only for played levels.
    /// Each button shows the number of attempts and changes color (green if has records, gray if not).
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

        // Update the dynamic subtitle with the patient name
        if (txtHistorySubtitle != null)
        {
            txtHistorySubtitle.text = $"Escolhe um nível para ver as tentativas de <color=#FCD34D>{selectedID}</color>";
        }

        // Configure each level button (number, color, attempts text, click handler)
        for (int i = 0; i < historyLevelButtons.Length; i++)
        {
            if (historyLevelButtons[i] == null) continue;

            int levelID = i + 1;
            string targetLevelName = "Level" + levelID;

            // Count attempts for this level
            int attemptsCount = 0;
            foreach (SessionRecord record in data.sessionHistory)
            {
                if (record.levelName == targetLevelName)
                {
                    attemptsCount++;
                }
            }

            bool hasPlayedThisLevel = attemptsCount > 0;

            Button btn = historyLevelButtons[i];
            btn.interactable = hasPlayedThisLevel;

            // Update background color (green if has records, gray faded if not)
            Image bgImage = btn.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = hasPlayedThisLevel ? historyHasRecordsColor : historyNoRecordsColor;
            }

            // Update the "X tentativas" text inside the button (looks for child named "AttemptsText")
            Transform attemptsTransform = btn.transform.Find("AttemptsText");
            if (attemptsTransform != null)
            {
                TMP_Text attemptsText = attemptsTransform.GetComponent<TMP_Text>();
                if (attemptsText != null)
                {
                    if (hasPlayedThisLevel)
                    {
                        string label = attemptsCount == 1 ? "tentativa" : "tentativas";
                        attemptsText.text = $"{attemptsCount} {label}";
                        attemptsText.color = new Color(1f, 1f, 1f, 0.85f);
                    }
                    else
                    {
                        attemptsText.text = "sem registos";
                        attemptsText.color = new Color(1f, 1f, 1f, 0.4f);
                    }
                }
            }

            // Also fade the Number text when there are no records (looks for child named "Number")
            Transform numberTransform = btn.transform.Find("Number");
            if (numberTransform != null)
            {
                TMP_Text numberText = numberTransform.GetComponent<TMP_Text>();
                if (numberText != null)
                {
                    numberText.color = hasPlayedThisLevel
                        ? new Color(1f, 1f, 1f, 1f)
                        : new Color(1f, 1f, 1f, 0.4f);
                }
            }

            // Clear old clicks and add new one
            btn.onClick.RemoveAllListeners();
            if (hasPlayedThisLevel)
            {
                PlayerData capturedData = data;
                string capturedLevelName = targetLevelName;
                btn.onClick.AddListener(() => ShowAttemptsForLevel(capturedData, capturedLevelName));
            }
        }

        // Configure the freestyle history button (counts Freestyle/Level11 sessions)
        ConfigureFreestyleHistoryButton(data);
    }

    /// <summary>
    /// Configures the freestyle button in the history panel:
    /// counts how many freestyle sessions exist and updates the count label.
    /// </summary>
    private void ConfigureFreestyleHistoryButton(PlayerData data)
    {
        if (btnHistFreestyle == null) return;

        // Count freestyle sessions. Adjust the level name here if your freestyle scene has a different name.
        int freestyleCount = 0;
        foreach (SessionRecord record in data.sessionHistory)
        {
            if (record.levelName == "Freestyle" || record.levelName == "Level11" || record.levelName == "FreestyleLevel")
            {
                freestyleCount++;
            }
        }

        bool hasFreestyleRecords = freestyleCount > 0;
        btnHistFreestyle.interactable = hasFreestyleRecords;

        // Update count text if the reference is set
        if (txtHistFreestyleCount != null)
        {
            if (hasFreestyleRecords)
            {
                string label = freestyleCount == 1 ? "sessão registada" : "sessões registadas";
                txtHistFreestyleCount.text = $"{freestyleCount} {label}";
            }
            else
            {
                txtHistFreestyleCount.text = "sem sessões registadas";
            }
        }

        // Setup click handler
        btnHistFreestyle.onClick.RemoveAllListeners();
        if (hasFreestyleRecords)
        {
            PlayerData capturedData = data;
            btnHistFreestyle.onClick.AddListener(() => ShowAttemptsForLevel(capturedData, "Freestyle"));
        }
    }

    /// <summary>
    /// Displays the list of attempts for a specific level with Date and Time.
    /// The header (patient name, level title, attempt count) is now updated separately
    /// from the scrollable list, so it stays fixed at the top.
    /// </summary>
    public void ShowAttemptsForLevel(PlayerData data, string levelName)
    {
        historyLevelsPanel.SetActive(false);
        historyAttemptsPanel.SetActive(true);

        // Count attempts for the header
        int totalAttempts = 0;
        foreach (SessionRecord record in data.sessionHistory)
        {
            if (record.levelName == levelName) totalAttempts++;
        }

        // Update fixed header texts
        if (txtPatientName != null)
            txtPatientName.text = data.profileID;

        if (txtLevelTitle != null)
            txtLevelTitle.text = levelName;

        if (txtAttemptsCount != null)
        {
            string label = totalAttempts == 1 ? "tentativa registada" : "tentativas registadas";
            txtAttemptsCount.text = $"{totalAttempts} {label}";
        }

        // Build only the attempts list (no header inside the scroll content anymore)
        string historyText = "";
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
                historyText += $"Tempo: <b>{record.completionTime:F1}s</b>   |   Colisões: <color=#EF4444><b>{record.totalCollisions}</b></color>   |   Deslizes: <color=#FCD34D><b>{record.totalSlides}</b></color>\n";
                historyText += "<color=#333333>──────────────────────────────────────────</color>\n\n";

                attemptCount++;
            }
        }

        if (txtHistoryDetails != null)
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