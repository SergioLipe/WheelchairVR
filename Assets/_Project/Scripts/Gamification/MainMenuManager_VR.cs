using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System; // Required to format the date

/// <summary>
/// VR version of the Main Menu Manager.
/// Differences from the PC version:
/// - No cursor coroutine (controllers use raycast, not mouse cursor)
/// - Profiles shown as dynamic list of clickable buttons (instead of dropdown)
/// - Clicking a profile button logs in directly (no separate login button)
/// - Separate "Create new profile" flow with input field + virtual keyboard
/// 
/// Same as PC version:
/// - History system (levels grid + attempts panel)
/// - Save/Load via SaveManager and ProfileManager
/// - Level initialization with stars
/// </summary>
public class MainMenuManager_VR : MonoBehaviour
{
    [Header("--- Main UI Elements ---")]
    [Tooltip("Drag the main game title text here so it hides during history")]
    public GameObject mainTitle;

    [Header("--- Profile Selection Panel ---")]
    public GameObject profileSelectionPanel;
    public GameObject levelSelectionPanel;
    public TMP_Text txtCurrentProfile;

    [Header("--- VR: Existing Profiles List ---")]
    [Tooltip("Drag the Content GameObject of the ProfilesList ScrollView (where buttons will be cloned)")]
    public Transform profilesListContent;

    [Tooltip("Drag the template button (will be cloned for each profile). It's hidden at start.")]
    public Button profileButtonTemplate;

    [Tooltip("Optional: text shown when there are no saved profiles yet")]
    public GameObject noProfilesPlaceholder;

    [Header("--- VR: Create New Profile ---")]
    public TMP_InputField inputFieldNewProfileID;
    public Button btnCreateProfile;

    [Header("--- VR: View History ---")]
    public Button btnViewHistory;

    [Tooltip("VR: Panel shown when user clicks 'View history' to choose which profile to inspect")]
    public GameObject historyProfileSelectorPanel;

    [Tooltip("VR: Where history profile buttons will be cloned (similar to profile list)")]
    public Transform historyProfilesListContent;

    [Tooltip("VR: Template button for history profile selector")]
    public Button historyProfileButtonTemplate;

    [Tooltip("VR: Button to close the history profile selector and go back to login")]
    public Button btnCloseHistoryProfileSelector;

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
    public TMP_Text txtPatientName;
    public TMP_Text txtLevelTitle;
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
    public Color historyHasRecordsColor = new Color(0.086f, 0.639f, 0.290f, 1f);
    public Color historyNoRecordsColor = new Color(0.32f, 0.32f, 0.32f, 0.5f);

    // List of profile buttons currently spawned (so we can destroy them on refresh)
    private List<GameObject> spawnedProfileButtons = new List<GameObject>();
    private List<GameObject> spawnedHistoryProfileButtons = new List<GameObject>();

    private void Start()
    {
        // 1. Setup Initial Panels Visibility
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);

        // 2. Hide the profile button template (we only clone it, never show the original)
        if (profileButtonTemplate != null) profileButtonTemplate.gameObject.SetActive(false);
        if (historyProfileButtonTemplate != null) historyProfileButtonTemplate.gameObject.SetActive(false);

        // 3. Populate the profiles list with existing patients
        PopulateProfilesList();

        // 4. Setup "Create new profile" button
        if (btnCreateProfile != null) btnCreateProfile.onClick.AddListener(OnCreateProfileClicked);

        // 5. Setup history navigation buttons
        if (btnViewHistory != null) btnViewHistory.onClick.AddListener(OpenHistoryProfileSelector);
        if (btnCloseHistoryProfileSelector != null) btnCloseHistoryProfileSelector.onClick.AddListener(CloseHistoryProfileSelector);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(CloseHistory);
        if (btnBackToHistLevels != null) btnBackToHistLevels.onClick.AddListener(BackToHistoryLevels);

        // 6. Setup main game levels (stars, locks, click handlers)
        InitializeAllLevels();
    }

    // ==========================================
    // VR PROFILE LIST (replaces dropdown)
    // ==========================================

    /// <summary>
    /// Clones the profile button template once for each saved profile.
    /// Clicking a button logs in directly with that profile.
    /// </summary>
    private void PopulateProfilesList()
    {
        // Clean up previous buttons (in case we refresh)
        foreach (GameObject oldBtn in spawnedProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedProfileButtons.Clear();

        if (profileButtonTemplate == null || profilesListContent == null)
        {
            Debug.LogWarning("[MainMenuManager_VR] Profile button template or content reference missing.");
            return;
        }

        string[] savedIDs = SaveManager.GetAllProfileIDs();

        // Show or hide "no profiles" placeholder
        if (noProfilesPlaceholder != null)
        {
            noProfilesPlaceholder.SetActive(savedIDs == null || savedIDs.Length == 0);
        }

        if (savedIDs == null || savedIDs.Length == 0) return;

        // Create one button per profile
        foreach (string profileID in savedIDs)
        {
            GameObject newBtnObj = Instantiate(profileButtonTemplate.gameObject, profilesListContent);
            newBtnObj.SetActive(true);
            newBtnObj.name = "Btn_Profile_" + profileID;

            // Set the button text to the profile ID
            TMP_Text btnText = newBtnObj.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = profileID;

            // Hook up click to login
            Button btn = newBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                string capturedID = profileID; // capture for closure
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => LoginWithProfile(capturedID));
            }

            spawnedProfileButtons.Add(newBtnObj);
        }
    }

    /// <summary>
    /// Logs in directly with the given profile ID (called when user clicks an existing profile button).
    /// </summary>
    public void LoginWithProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        PlayerData loadedData = SaveManager.LoadProfile(profileID);

        if (loadedData != null)
        {
            ProfileManager.Instance.SetActiveProfile(loadedData);
        }
        else
        {
            // Should not happen since we listed existing profiles, but just in case
            PlayerData newData = new PlayerData();
            newData.profileID = profileID;
            SaveManager.SaveProfile(newData);
            ProfileManager.Instance.SetActiveProfile(newData);
        }

        if (txtCurrentProfile != null)
            txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// Called when the user types a new ID and presses "Create and Enter".
    /// </summary>
    public void OnCreateProfileClicked()
    {
        if (inputFieldNewProfileID == null) return;

        string typedID = inputFieldNewProfileID.text.Trim();

        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        // If profile already exists, just log in. If not, create.
        PlayerData loadedData = SaveManager.LoadProfile(typedID);

        if (loadedData != null)
        {
            ProfileManager.Instance.SetActiveProfile(loadedData);
        }
        else
        {
            PlayerData newData = new PlayerData();
            newData.profileID = typedID;
            SaveManager.SaveProfile(newData);
            ProfileManager.Instance.SetActiveProfile(newData);
        }

        if (txtCurrentProfile != null)
            txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
    }

    // ==========================================
    // HISTORY: PROFILE SELECTOR (VR-specific)
    // ==========================================

    /// <summary>
    /// Opens an intermediate panel where the user picks which patient's history they want to view.
    /// (Replaces the PC's dropdown selection.)
    /// </summary>
    public void OpenHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel == null)
        {
            // If there's no separate selector panel, just open history with the first profile found
            string[] ids = SaveManager.GetAllProfileIDs();
            if (ids != null && ids.Length > 0)
            {
                OpenHistoryLevelsForProfile(ids[0]);
            }
            return;
        }

        profileSelectionPanel.SetActive(false);
        historyProfileSelectorPanel.SetActive(true);

        if (mainTitle != null) mainTitle.SetActive(false);

        PopulateHistoryProfilesList();
    }

    private void PopulateHistoryProfilesList()
    {
        // Clean up previous
        foreach (GameObject oldBtn in spawnedHistoryProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedHistoryProfileButtons.Clear();

        if (historyProfileButtonTemplate == null || historyProfilesListContent == null) return;

        string[] savedIDs = SaveManager.GetAllProfileIDs();
        if (savedIDs == null || savedIDs.Length == 0) return;

        foreach (string profileID in savedIDs)
        {
            GameObject newBtnObj = Instantiate(historyProfileButtonTemplate.gameObject, historyProfilesListContent);
            newBtnObj.SetActive(true);
            newBtnObj.name = "Btn_HistProfile_" + profileID;

            TMP_Text btnText = newBtnObj.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = profileID;

            Button btn = newBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                string capturedID = profileID;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OpenHistoryLevelsForProfile(capturedID));
            }

            spawnedHistoryProfileButtons.Add(newBtnObj);
        }
    }

    public void CloseHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (mainTitle != null) mainTitle.SetActive(true);
    }

    // ==========================================
    // HISTORY SYSTEM (same logic as PC version)
    // ==========================================

    /// <summary>
    /// Opens the History Levels panel for the chosen profile.
    /// </summary>
    public void OpenHistoryLevelsForProfile(string profileID)
    {
        PlayerData data = SaveManager.LoadProfile(profileID);
        if (data == null) return;

        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        profileSelectionPanel.SetActive(false);
        historyLevelsPanel.SetActive(true);

        if (mainTitle != null) mainTitle.SetActive(false);

        if (txtHistorySubtitle != null)
        {
            txtHistorySubtitle.text = $"Escolhe um nível para ver as tentativas de <color=#FCD34D>{profileID}</color>";
        }

        for (int i = 0; i < historyLevelButtons.Length; i++)
        {
            if (historyLevelButtons[i] == null) continue;

            int levelID = i + 1;
            string targetLevelName = "Level" + levelID;

            int attemptsCount = 0;
            foreach (SessionRecord record in data.sessionHistory)
            {
                if (record.levelName == targetLevelName) attemptsCount++;
            }

            bool hasPlayedThisLevel = attemptsCount > 0;

            Button btn = historyLevelButtons[i];
            btn.interactable = hasPlayedThisLevel;

            Image bgImage = btn.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = hasPlayedThisLevel ? historyHasRecordsColor : historyNoRecordsColor;
            }

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

            btn.onClick.RemoveAllListeners();
            if (hasPlayedThisLevel)
            {
                PlayerData capturedData = data;
                string capturedLevelName = targetLevelName;
                btn.onClick.AddListener(() => ShowAttemptsForLevel(capturedData, capturedLevelName));
            }
        }

        ConfigureFreestyleHistoryButton(data);
    }

    private void ConfigureFreestyleHistoryButton(PlayerData data)
    {
        if (btnHistFreestyle == null) return;

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

        btnHistFreestyle.onClick.RemoveAllListeners();
        if (hasFreestyleRecords)
        {
            PlayerData capturedData = data;
            btnHistFreestyle.onClick.AddListener(() => ShowAttemptsForLevel(capturedData, "Freestyle"));
        }
    }

    public void ShowAttemptsForLevel(PlayerData data, string levelName)
    {
        historyLevelsPanel.SetActive(false);
        historyAttemptsPanel.SetActive(true);

        int totalAttempts = 0;
        foreach (SessionRecord record in data.sessionHistory)
        {
            if (record.levelName == levelName) totalAttempts++;
        }

        if (txtPatientName != null) txtPatientName.text = data.profileID;
        if (txtLevelTitle != null) txtLevelTitle.text = levelName;

        if (txtAttemptsCount != null)
        {
            string label = totalAttempts == 1 ? "tentativa registada" : "tentativas registadas";
            txtAttemptsCount.text = $"{totalAttempts} {label}";
        }

        string historyText = "";
        int attemptCount = 1;

        List<SessionRecord> records = new List<SessionRecord>(data.sessionHistory);
        records.Reverse();

        foreach (SessionRecord record in records)
        {
            if (record.levelName == levelName)
            {
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
        if (mainTitle != null) mainTitle.SetActive(true);
    }

    public void BackToHistoryLevels()
    {
        historyAttemptsPanel.SetActive(false);
        historyLevelsPanel.SetActive(true);
    }

    // ==========================================
    // MAIN GAME LEVELS LOGIC (same as PC)
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