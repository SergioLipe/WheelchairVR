using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// VR version of the Main Menu Manager.
/// Each profile row has:
/// - Login (click on the name)
/// - Edit button (rename inline, uses Quest virtual keyboard)
/// - Delete button (with confirmation popup)
/// 
/// Level selection panel has a steering type toggle (Frontal / Traseira)
/// which is stored in SteeringPreference and applied when entering each level.
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
    [Tooltip("Drag the Content GameObject of the ProfilesList ScrollView (where rows will be cloned)")]
    public Transform profilesListContent;

    [Tooltip("Drag the BtnProfileTemplate GameObject. Must contain children: Btn_LoginProfile, InputEditName, Btn_EditProfile, Btn_DeleteProfile.")]
    public GameObject profileButtonTemplate;

    [Tooltip("Optional: text shown when there are no saved profiles yet")]
    public GameObject noProfilesPlaceholder;

    [Header("--- VR: Create New Profile ---")]
    public TMP_InputField inputFieldNewProfileID;
    public Button btnCreateProfile;

    [Header("--- VR: Confirm Delete Popup ---")]
    public GameObject confirmDeletePanel;
    public TMP_Text txtConfirmMessage;
    public Button btnConfirmYes;
    public Button btnConfirmNo;

    [Header("--- VR: View History ---")]
    public Button btnViewHistory;

    [Tooltip("VR: Panel shown when user clicks 'View history' to choose which profile to inspect")]
    public GameObject historyProfileSelectorPanel;

    [Tooltip("VR: Where history profile buttons will be cloned")]
    public Transform historyProfilesListContent;

    [Tooltip("VR: Template button for history profile selector")]
    public GameObject historyProfileButtonTemplate;

    [Tooltip("VR: Button to close the history profile selector and go back to login")]
    public Button btnCloseHistoryProfileSelector;

    [Header("--- Steering Type Selection (LevelSelectionPanel) ---")]
    [Tooltip("Button to select Front Steering (Frontal)")]
    public Button btnSteeringFront;

    [Tooltip("Button to select Rear Steering (Traseira)")]
    public Button btnSteeringRear;

    [Header("--- Steering Button Visual States ---")]
    [Tooltip("Image color when the button IS the selected steering type")]
    public Color steeringSelectedColor = new Color(0.376f, 0.647f, 0.980f, 0.314f); // azul subtil 80a

    [Tooltip("Outline color when the button IS the selected steering type")]
    public Color steeringSelectedOutlineColor = new Color(0.376f, 0.647f, 0.980f, 0.863f); // azul 220a

    [Tooltip("Text color when the button IS the selected steering type")]
    public Color steeringSelectedTextColor = Color.white;

    [Tooltip("Image color when the button is NOT the selected steering type")]
    public Color steeringUnselectedColor = new Color(1f, 1f, 1f, 0.078f); // branco 20a

    [Tooltip("Outline color when the button is NOT the selected steering type")]
    public Color steeringUnselectedOutlineColor = new Color(1f, 1f, 1f, 0.314f); // branco 80a

    [Tooltip("Text color when the button is NOT the selected steering type")]
    public Color steeringUnselectedTextColor = new Color(1f, 1f, 1f, 0.706f); // branco 180a

    [Header("--- History: Levels Panel ---")]
    public GameObject historyLevelsPanel;
    public Button btnCloseHistory;
    public Button[] historyLevelButtons;
    public TMP_Text txtHistorySubtitle;
    public Button btnHistFreestyle;
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

    private List<GameObject> spawnedProfileButtons = new List<GameObject>();
    private List<GameObject> spawnedHistoryProfileButtons = new List<GameObject>();
    private string profileToDelete = null;

    private void Start()
    {
        // 1. Setup Initial Panels Visibility
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);

        // 2. Hide button templates
        if (profileButtonTemplate != null) profileButtonTemplate.SetActive(false);
        if (historyProfileButtonTemplate != null) historyProfileButtonTemplate.SetActive(false);

        // 3. Populate the profiles list
        PopulateProfilesList();

        // 4. Setup "Create new profile" button
        if (btnCreateProfile != null) btnCreateProfile.onClick.AddListener(OnCreateProfileClicked);

        // 5. Setup history navigation buttons
        if (btnViewHistory != null) btnViewHistory.onClick.AddListener(OpenHistoryProfileSelector);
        if (btnCloseHistoryProfileSelector != null) btnCloseHistoryProfileSelector.onClick.AddListener(CloseHistoryProfileSelector);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(CloseHistory);
        if (btnBackToHistLevels != null) btnBackToHistLevels.onClick.AddListener(BackToHistoryLevels);

        // 6. Confirm delete popup buttons
        if (btnConfirmYes != null) btnConfirmYes.onClick.AddListener(OnConfirmDeleteYes);
        if (btnConfirmNo != null) btnConfirmNo.onClick.AddListener(OnConfirmDeleteNo);

        // 7. Setup steering type selection buttons
        if (btnSteeringFront != null)
        {
            btnSteeringFront.onClick.RemoveAllListeners();
            btnSteeringFront.onClick.AddListener(() => OnSteeringSelected(WheelController.SteeringType.FrontSteering));
        }
        if (btnSteeringRear != null)
        {
            btnSteeringRear.onClick.RemoveAllListeners();
            btnSteeringRear.onClick.AddListener(() => OnSteeringSelected(WheelController.SteeringType.RearSteering));
        }
        // Reflect current value visually (default: Front)
        RefreshSteeringButtonsVisual();

        // 8. Setup main game levels
        InitializeAllLevels();

        // 9. Force recenter the VR canvas (with delay so the HMD pose is stable)
        VRCanvasPositioner positioner = FindObjectOfType<VRCanvasPositioner>();
        if (positioner != null)
        {
            positioner.RecenterCanvas();
        }
    }

    // ==========================================
    // STEERING TYPE SELECTION
    // ==========================================

    /// <summary>
    /// Called when the user clicks one of the steering buttons in the LevelSelectionPanel.
    /// Updates the SteeringPreference (used by the level on scene load) and refreshes visuals.
    /// </summary>
    public void OnSteeringSelected(WheelController.SteeringType steeringType)
    {
        SteeringPreference.SetSteering(steeringType);
        RefreshSteeringButtonsVisual();
    }

    /// <summary>
    /// Visually updates the two buttons to reflect which is currently selected.
    /// </summary>
    private void RefreshSteeringButtonsVisual()
    {
        bool frontSelected = SteeringPreference.CurrentSteering == WheelController.SteeringType.FrontSteering;
        bool rearSelected = SteeringPreference.CurrentSteering == WheelController.SteeringType.RearSteering;

        SetSteeringButtonVisual(btnSteeringFront, frontSelected);
        SetSteeringButtonVisual(btnSteeringRear, rearSelected);
    }

    /// <summary>
    /// Applies the "selected" or "unselected" visual style to a steering button.
    /// </summary>
    private void SetSteeringButtonVisual(Button button, bool isSelected)
    {
        if (button == null) return;

        // Background image color
        Image bgImage = button.GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = isSelected ? steeringSelectedColor : steeringUnselectedColor;
        }

        // Outline color
        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = isSelected ? steeringSelectedOutlineColor : steeringUnselectedOutlineColor;
        }

        // Text color
        TMP_Text txt = button.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.color = isSelected ? steeringSelectedTextColor : steeringUnselectedTextColor;
        }
    }

    // ==========================================
    // VR PROFILE LIST WITH LOGIN / EDIT / DELETE
    // ==========================================

    private void PopulateProfilesList()
    {
        foreach (GameObject oldBtn in spawnedProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedProfileButtons.Clear();

        if (profileButtonTemplate == null || profilesListContent == null)
        {
            Debug.LogWarning("[MainMenuManager_VR] Profile template or content reference missing.");
            return;
        }

        if (profileButtonTemplate.activeSelf)
            profileButtonTemplate.SetActive(false);

        string[] savedIDs = SaveManager.GetAllProfileIDs();

        if (noProfilesPlaceholder != null)
        {
            noProfilesPlaceholder.SetActive(savedIDs == null || savedIDs.Length == 0);
        }

        if (savedIDs == null || savedIDs.Length == 0) return;

        foreach (string profileID in savedIDs)
        {
            GameObject newRow = Instantiate(profileButtonTemplate, profilesListContent);
            newRow.SetActive(true);
            newRow.name = "Row_Profile_" + profileID;

            string capturedID = profileID;

            Transform loginBtnT = newRow.transform.Find("Btn_LoginProfile");
            Transform editBtnT = newRow.transform.Find("Btn_EditProfile");
            Transform deleteBtnT = newRow.transform.Find("Btn_DeleteProfile");
            Transform inputEditT = newRow.transform.Find("InputEditName");

            Button loginBtn = null;
            TMP_Text loginText = null;
            if (loginBtnT != null)
            {
                loginBtn = loginBtnT.GetComponent<Button>();
                loginText = loginBtnT.GetComponentInChildren<TMP_Text>();

                if (loginText != null) loginText.text = profileID;

                if (loginBtn != null)
                {
                    loginBtn.onClick.RemoveAllListeners();
                    loginBtn.onClick.AddListener(() => LoginWithProfile(capturedID));
                }
            }
            else
            {
                Debug.LogWarning($"[MainMenuManager_VR] Btn_LoginProfile not found in row '{newRow.name}'.");
            }

            TMP_InputField editInput = null;
            if (inputEditT != null)
            {
                editInput = inputEditT.GetComponent<TMP_InputField>();
                inputEditT.gameObject.SetActive(false);
            }

            if (editBtnT != null)
            {
                Button editBtn = editBtnT.GetComponent<Button>();
                if (editBtn != null && editInput != null && loginBtn != null)
                {
                    Button capturedLogin = loginBtn;
                    TMP_InputField capturedInput = editInput;
                    TMP_Text capturedLoginText = loginText;

                    editBtn.onClick.RemoveAllListeners();
                    editBtn.onClick.AddListener(() => StartRenameProfile(capturedID, capturedLogin, capturedInput, capturedLoginText));
                }
            }

            if (deleteBtnT != null)
            {
                Button deleteBtn = deleteBtnT.GetComponent<Button>();
                if (deleteBtn != null)
                {
                    deleteBtn.onClick.RemoveAllListeners();
                    deleteBtn.onClick.AddListener(() => RequestDeleteProfile(capturedID));
                }
            }

            spawnedProfileButtons.Add(newRow);
        }
    }

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
            PlayerData newData = new PlayerData();
            newData.profileID = profileID;
            SaveManager.SaveProfile(newData);
            ProfileManager.Instance.SetActiveProfile(newData);
        }

        if (txtCurrentProfile != null)
            txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);

        // Make sure the steering buttons visual is up to date when this panel opens
        RefreshSteeringButtonsVisual();
    }

    public void OnCreateProfileClicked()
    {
        if (inputFieldNewProfileID == null) return;

        string typedID = inputFieldNewProfileID.text.Trim();

        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        PlayerData loadedData = SaveManager.LoadProfile(typedID);
        bool isNewProfile = (loadedData == null);

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

        if (isNewProfile) PopulateProfilesList();

        if (inputFieldNewProfileID != null) inputFieldNewProfileID.text = "";

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);

        RefreshSteeringButtonsVisual();
    }

    // ==========================================
    // EDIT (RENAME) PROFILE
    // ==========================================

    private void StartRenameProfile(string oldID, Button loginBtn, TMP_InputField editInput, TMP_Text loginText)
    {
        if (loginBtn == null || editInput == null) return;

        loginBtn.gameObject.SetActive(false);
        editInput.gameObject.SetActive(true);
        editInput.text = oldID;

        Transform row = loginBtn.transform.parent;
        if (row != null)
        {
            Transform editBtnT = row.Find("Btn_EditProfile");
            Transform deleteBtnT = row.Find("Btn_DeleteProfile");
            if (editBtnT != null) editBtnT.gameObject.SetActive(false);
            if (deleteBtnT != null) deleteBtnT.gameObject.SetActive(false);
        }

        editInput.Select();
        editInput.ActivateInputField();

        editInput.onEndEdit.RemoveAllListeners();
        editInput.onEndEdit.AddListener((newName) =>
        {
            FinishRenameProfile(oldID, newName, loginBtn, editInput, loginText);
        });
    }

    private void FinishRenameProfile(string oldID, string newName, Button loginBtn, TMP_InputField editInput, TMP_Text loginText)
    {
        if (editInput != null) editInput.gameObject.SetActive(false);
        if (loginBtn != null) loginBtn.gameObject.SetActive(true);

        if (loginBtn != null)
        {
            Transform row = loginBtn.transform.parent;
            if (row != null)
            {
                Transform editBtnT = row.Find("Btn_EditProfile");
                Transform deleteBtnT = row.Find("Btn_DeleteProfile");
                if (editBtnT != null) editBtnT.gameObject.SetActive(true);
                if (deleteBtnT != null) deleteBtnT.gameObject.SetActive(true);
            }
        }

        string trimmed = newName != null ? newName.Trim() : "";

        if (string.IsNullOrEmpty(trimmed) || trimmed == oldID) return;

        bool success = SaveManager.RenameProfile(oldID, trimmed);

        if (success)
        {
            PopulateProfilesList();
        }
        else
        {
            if (loginText != null) loginText.text = oldID;
            Debug.LogWarning($"[MainMenuManager_VR] Failed to rename '{oldID}' to '{trimmed}'.");
        }
    }

    // ==========================================
    // DELETE PROFILE WITH CONFIRMATION
    // ==========================================

    public void RequestDeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        profileToDelete = profileID;

        if (confirmDeletePanel != null)
        {
            if (txtConfirmMessage != null)
                txtConfirmMessage.text = $"Tem a certeza que quer apagar o perfil <b><color=#F87171>{profileID}</color></b>?\n\nEsta ação não pode ser desfeita.";
            confirmDeletePanel.SetActive(true);
        }
        else
        {
            DeleteProfile(profileID);
            profileToDelete = null;
        }
    }

    public void OnConfirmDeleteYes()
    {
        if (!string.IsNullOrEmpty(profileToDelete))
        {
            DeleteProfile(profileToDelete);
            profileToDelete = null;
        }
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);
    }

    public void OnConfirmDeleteNo()
    {
        profileToDelete = null;
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);
    }

    private void DeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        bool deleted = SaveManager.DeleteProfile(profileID);

        if (deleted)
        {
            PopulateProfilesList();
        }
        else
        {
            Debug.LogWarning($"[MainMenuManager_VR] Failed to delete profile '{profileID}'.");
        }
    }

    // ==========================================
    // HISTORY: PROFILE SELECTOR
    // ==========================================

    public void OpenHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel == null)
        {
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
        foreach (GameObject oldBtn in spawnedHistoryProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedHistoryProfileButtons.Clear();

        if (historyProfileButtonTemplate == null || historyProfilesListContent == null) return;

        if (historyProfileButtonTemplate.activeSelf)
            historyProfileButtonTemplate.SetActive(false);

        string[] savedIDs = SaveManager.GetAllProfileIDs();
        if (savedIDs == null || savedIDs.Length == 0) return;

        foreach (string profileID in savedIDs)
        {
            GameObject newRow = Instantiate(historyProfileButtonTemplate, historyProfilesListContent);
            newRow.SetActive(true);
            newRow.name = "Row_HistProfile_" + profileID;

            string capturedID = profileID;

            Transform selectBtnT = newRow.transform.Find("Btn_SelectProfile");

            Button selectBtn = null;
            TMP_Text selectText = null;

            if (selectBtnT != null)
            {
                selectBtn = selectBtnT.GetComponent<Button>();
                selectText = selectBtnT.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                selectBtn = newRow.GetComponent<Button>();
                selectText = newRow.GetComponentInChildren<TMP_Text>();
            }

            if (selectText != null) selectText.text = profileID;

            if (selectBtn != null)
            {
                selectBtn.onClick.RemoveAllListeners();
                selectBtn.onClick.AddListener(() => OpenHistoryLevelsForProfile(capturedID));
            }

            spawnedHistoryProfileButtons.Add(newRow);
        }
    }

    public void CloseHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
        if (mainTitle != null) mainTitle.SetActive(true);
    }

    // ==========================================
    // HISTORY SYSTEM
    // ==========================================

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
                        string label = attemptsCount == 1 ? "Tentativa" : "Tentativas";
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
                txtHistFreestyleCount.text = $"MODO LIVRE — {freestyleCount} {label}";
            }
            else
            {
                txtHistFreestyleCount.text = "MODO LIVRE — sem sessões registadas";
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
            string label = totalAttempts == 1 ? "Tentativa registada" : "Tentativas registadas";
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

        PopulateProfilesList();

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
        if (levelButtons == null || levelButtons.Length == 0) return;

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
        Debug.Log($"A carregar a cena: {sceneName} (Steering: {SteeringPreference.CurrentSteering})");
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