using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System; // Required to format the date

/// <summary>
/// Manages the Main Menu dynamically.
/// Each profile row has:
/// - Login (click on the name)
/// - Edit button (rename inline)
/// - Delete button (with confirmation popup)
/// 
/// Generic "Ver Histórico" opens an intermediate panel where the user picks which patient to consult.
/// 
/// When returning from a level (active profile already set), automatically skips the login panel
/// and opens the level selection panel.
/// </summary>
public class MainMenuManager_PC : MonoBehaviour
{
    [Header("--- Main UI Elements ---")]
    [Tooltip("Drag the main game title text here so it hides during history")]
    public GameObject mainTitle;

    [Header("--- Profile UI Elements ---")]
    public GameObject profileSelectionPanel;
    public GameObject levelSelectionPanel;
    public TMP_InputField inputFieldProfileID;
    public Button btnLogin;
    public TMP_Text txtCurrentProfile;

    [Header("--- Existing Profiles List ---")]
    [Tooltip("Drag the Content GameObject of the ProfilesList ScrollView (where rows will be cloned)")]
    public Transform profilesListContent;

    [Tooltip("Drag the BtnProfileTemplate GameObject. Must contain children: Btn_LoginProfile, InputEditName, Btn_EditProfile, Btn_DeleteProfile.")]
    public GameObject profileButtonTemplate;

    [Header("--- Confirm Delete Popup ---")]
    [Tooltip("The overlay panel shown when user clicks the trash icon")]
    public GameObject confirmDeletePanel;

    [Tooltip("Text inside the popup that shows which profile is being deleted")]
    public TMP_Text txtConfirmMessage;

    [Tooltip("Yes/Apagar button in the popup")]
    public Button btnConfirmYes;

    [Tooltip("No/Cancelar button in the popup")]
    public Button btnConfirmNo;

    [Header("--- History UI Elements ---")]
    public TMP_Dropdown dropdownProfiles;
    public Button btnViewHistory;

    [Header("--- History: Profile Selector Panel (intermediate) ---")]
    [Tooltip("Panel shown when user clicks 'Ver Histórico' to choose which patient to consult")]
    public GameObject historyProfileSelectorPanel;

    [Tooltip("Content of the ScrollView inside HistoryProfileSelectorPanel")]
    public Transform historyProfilesListContent;

    [Tooltip("Template button inside HistoryProfileSelectorPanel (BtnHistProfileTemplate)")]
    public GameObject historyProfileButtonTemplate;

    [Tooltip("Back button inside HistoryProfileSelectorPanel")]
    public Button btnBackFromHistorySelector;

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

    // List of profile rows spawned (so we can destroy them on refresh)
    private List<GameObject> spawnedProfileButtons = new List<GameObject>();
    private List<GameObject> spawnedHistoryProfileButtons = new List<GameObject>();

    // Tracks the last profile the user interacted with
    private string lastSelectedProfileID = null;

    // Profile ID currently waiting for delete confirmation
    private string profileToDelete = null;

    private void Start()
    {
        // Force cursor visible immediately
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(KeepCursorVisible());

        // 1. Setup Initial Panels Visibility
        // If we already have an active profile (came back from a level), skip login
        // and go straight to the level selection panel.
        bool hasActiveProfile = ProfileManager.Instance != null
                                && ProfileManager.Instance.currentPlayer != null
                                && !string.IsNullOrEmpty(ProfileManager.Instance.currentPlayer.profileID);

        if (hasActiveProfile)
        {
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);

            // Update the "current profile" label
            if (txtCurrentProfile != null)
                txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

            // Track this as the last selected profile (for the history feature)
            lastSelectedProfileID = ProfileManager.Instance.currentPlayer.profileID;
        }
        else
        {
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);
            if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        }

        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(false);
        if (historyAttemptsPanel != null) historyAttemptsPanel.SetActive(false);
        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);
        if (confirmDeletePanel != null) confirmDeletePanel.SetActive(false);

        // 2. Main Login Buttons
        if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginButtonClicked);

        // 3. Setup Dropdown (legacy, kept for backwards compatibility)
        if (dropdownProfiles != null) dropdownProfiles.onValueChanged.AddListener(OnDropdownValueChanged);
        LoadDropdownProfiles();

        // 4. Hide templates and populate the dynamic list
        if (profileButtonTemplate != null) profileButtonTemplate.SetActive(false);
        if (historyProfileButtonTemplate != null) historyProfileButtonTemplate.SetActive(false);
        PopulateProfilesList();

        // 5. Setup History Navigation Buttons
        if (btnViewHistory != null) btnViewHistory.onClick.AddListener(OpenHistoryProfileSelector);
        if (btnBackFromHistorySelector != null) btnBackFromHistorySelector.onClick.AddListener(CloseHistoryProfileSelector);
        if (btnCloseHistory != null) btnCloseHistory.onClick.AddListener(CloseHistory);
        if (btnBackToHistLevels != null) btnBackToHistLevels.onClick.AddListener(BackToHistoryLevels);

        // 6. Confirm delete popup buttons
        if (btnConfirmYes != null) btnConfirmYes.onClick.AddListener(OnConfirmDeleteYes);
        if (btnConfirmNo != null) btnConfirmNo.onClick.AddListener(OnConfirmDeleteNo);

        // 7. Setup Main Game Levels
        InitializeAllLevels();
    }

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
    // PROFILES LIST WITH 3 ACTIONS PER ROW
    // (Login by clicking name, Edit, Delete)
    // ==========================================

    /// <summary>
    /// Clones the profile row template once per saved profile.
    /// Each row has child buttons: Btn_LoginProfile, Btn_EditProfile, Btn_DeleteProfile, and an InputEditName field.
    /// </summary>
    private void PopulateProfilesList()
    {
        foreach (GameObject oldBtn in spawnedProfileButtons)
        {
            if (oldBtn != null) Destroy(oldBtn);
        }
        spawnedProfileButtons.Clear();

        if (profileButtonTemplate == null || profilesListContent == null)
        {
            Debug.LogWarning("[MainMenuManager_PC] Profile template or content reference missing.");
            return;
        }

        if (profileButtonTemplate.activeSelf)
            profileButtonTemplate.SetActive(false);

        string[] savedIDs = SaveManager.GetAllProfileIDs();
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
                    loginBtn.onClick.AddListener(() => LoginWithProfileDirect(capturedID));
                }
            }
            else
            {
                Debug.LogWarning($"[MainMenuManager_PC] Btn_LoginProfile not found in row '{newRow.name}'.");
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

    public void LoginWithProfileDirect(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        lastSelectedProfileID = profileID;

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

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// Public method to go back from the level selection panel to the profile selection panel.
    /// Hook this to a "Trocar perfil" / "← Voltar" button in the LevelSelectionPanel if desired.
    /// </summary>
    public void BackToProfileSelection()
    {
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(true);

        // Refresh the profiles list
        PopulateProfilesList();

        if (inputFieldProfileID != null) inputFieldProfileID.text = "";
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
            if (lastSelectedProfileID == oldID) lastSelectedProfileID = trimmed;

            PopulateProfilesList();
            LoadDropdownProfiles();
        }
        else
        {
            if (loginText != null) loginText.text = oldID;
            Debug.LogWarning($"[MainMenuManager_PC] Failed to rename '{oldID}' to '{trimmed}'.");
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
                txtConfirmMessage.text = $"Tem a certeza que quer apagar o perfil <b><color=#DC2626>{profileID}</color></b>?\n\nEsta ação não pode ser desfeita.";
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
            if (lastSelectedProfileID == profileID) lastSelectedProfileID = null;

            PopulateProfilesList();
            LoadDropdownProfiles();
        }
        else
        {
            Debug.LogWarning($"[MainMenuManager_PC] Failed to delete profile '{profileID}'.");
        }
    }

    // ==========================================
    // LOGIN & PROFILES SYSTEM (legacy dropdown system)
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
        if (inputFieldProfileID == null) return;

        string typedID = inputFieldProfileID.text.Trim();

        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID não pode estar vazio!");
            return;
        }

        lastSelectedProfileID = typedID;

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

        if (txtCurrentProfile != null) txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;

        if (isNewProfile)
        {
            PopulateProfilesList();
            LoadDropdownProfiles();
        }

        if (inputFieldProfileID != null) inputFieldProfileID.text = "";

        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
    }

    // ==========================================
    // HISTORY: PROFILE SELECTOR (intermediate panel)
    // ==========================================

    public void OpenHistoryProfileSelector()
    {
        if (historyProfileSelectorPanel == null)
        {
            OpenHistoryLevels();
            return;
        }

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
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
                selectBtn.onClick.AddListener(() => OpenHistoryForProfile(capturedID));
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

    public void OpenHistoryForProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return;

        PlayerData data = SaveManager.LoadProfile(profileID);
        if (data == null) return;

        if (historyProfileSelectorPanel != null) historyProfileSelectorPanel.SetActive(false);

        OpenHistoryLevelsForData(data);
    }

    // ==========================================
    // HISTORY SYSTEM
    // ==========================================

    public void OpenHistoryLevels()
    {
        string selectedID = null;

        if (!string.IsNullOrEmpty(lastSelectedProfileID))
        {
            selectedID = lastSelectedProfileID;
        }
        else if (dropdownProfiles != null && dropdownProfiles.interactable && dropdownProfiles.options.Count > 0)
        {
            selectedID = dropdownProfiles.options[dropdownProfiles.value].text;
        }
        else if (inputFieldProfileID != null && !string.IsNullOrEmpty(inputFieldProfileID.text.Trim()))
        {
            selectedID = inputFieldProfileID.text.Trim();
        }
        else
        {
            string[] ids = SaveManager.GetAllProfileIDs();
            if (ids != null && ids.Length > 0) selectedID = ids[0];
        }

        if (string.IsNullOrEmpty(selectedID)) return;

        PlayerData data = SaveManager.LoadProfile(selectedID);
        if (data == null) return;

        OpenHistoryLevelsForData(data);
    }

    private void OpenHistoryLevelsForData(PlayerData data)
    {
        if (data == null) return;

        if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
        if (historyLevelsPanel != null) historyLevelsPanel.SetActive(true);

        if (mainTitle != null) mainTitle.SetActive(false);

        if (txtHistorySubtitle != null)
        {
            txtHistorySubtitle.text = $"Escolhe um nível para ver as tentativas de <color=#FCD34D>{data.profileID}</color>";
        }

        for (int i = 0; i < historyLevelButtons.Length; i++)
        {
            if (historyLevelButtons[i] == null) continue;

            int levelID = i + 1;
            string targetLevelName = "Level" + levelID;

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

                // For Freestyle runs, show stars instead of time/collisions
                if (record.levelName == "Freestyle" || record.levelName == "Level11" || record.levelName == "FreestyleLevel")
                {
                    historyText += $"Estrelas: <color=#FCD34D><b>{record.starsCollected} / {record.starsTotal}</b></color>";

                    if (record.starsTotal > 0 && record.starsCollected >= record.starsTotal)
                    {
                        historyText += $"   <color=#16A34A><b>✓ Completo!</b></color>";
                    }

                    historyText += "\n";
                }
                else
                {
                    historyText += $"Tempo: <b>{record.completionTime:F1}s</b>   |   Colisões: <color=#EF4444><b>{record.totalCollisions}</b></color>   |   Deslizes: <color=#FCD34D><b>{record.totalSlides}</b></color>\n";
                }

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

        // Refresh the profiles list in case profiles changed
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