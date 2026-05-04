using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Main Menu dynamically.
/// Automatically finds and colors the stars and backgrounds for any number of levels.
/// Now includes Profile Selection logic and current user display.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("--- Profile UI Elements ---")]
    public GameObject profileSelectionPanel;
    public GameObject levelSelectionPanel;
    public TMP_InputField inputFieldProfileID;
    public Button btnLogin;
    
    [Tooltip("Text to show who is currently logged in")]
    public TMP_Text txtCurrentProfile; // <--- NOVA VARIÁVEL AQUI

    [Header("--- Level Buttons ---")]
    [Tooltip("Drag ALL your level buttons here in order (Level 1, Level 2... and Free Style last)")]
    public Button[] levelButtons;

    [Header("--- UI Colors ---")]
    [Tooltip("Color for normal levels you can play")]
    public Color unlockedBGColor = new Color(0f, 0.78f, 0.32f, 1f); // Vibrant Green

    [Tooltip("Color for the Free Style level when unlocked")]
    public Color freeStyleColor = new Color(0f, 0.6f, 1f, 1f); // Cool Blue

    [Tooltip("Color for levels you cannot play yet")]
    public Color lockedBGColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark Gray

    [Tooltip("Color for earned stars")]
    public Color starEarnedColor = new Color(1f, 0.84f, 0f, 1f); // Gold/Yellow

    [Tooltip("Color for missing stars")]
    public Color starEmptyColor = new Color(0f, 0f, 0f, 0.4f); // Semi-transparent Black

    private void Start()
    {
        // Force the mouse to be visible when the menu opens
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // --- Profile Logic ---
        // 1. Show profiles panel and hide levels panel on start
        if (profileSelectionPanel != null && levelSelectionPanel != null)
        {
            profileSelectionPanel.SetActive(true);
            levelSelectionPanel.SetActive(false);
        }

        // 2. Link the login button to the function
        if (btnLogin != null)
        {
            btnLogin.onClick.AddListener(OnLoginButtonClicked);
        }

        // --- Level Logic ---
        InitializeAllLevels();
    }

    private void LateUpdate()
    {
        // Runs AFTER all other scripts. 
        // If any spy script tries to lock the mouse on click, this instantly overrides it!
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    /// <summary>
    /// Handles the login process when the user clicks the login button.
    /// </summary>
    public void OnLoginButtonClicked()
    {
        // Reads what the user typed
        string typedID = inputFieldProfileID.text.Trim();

        // If it's empty, do nothing
        if (string.IsNullOrEmpty(typedID))
        {
            Debug.LogWarning("Atenção: O ID do paciente não pode estar vazio!");
            return;
        }

        // Try to load the profile
        PlayerData loadedData = SaveManager.LoadProfile(typedID);

        if (loadedData != null)
        {
            // Profile exists! Set it as active
            ProfileManager.Instance.SetActiveProfile(loadedData);
        }
        else
        {
            // Profile does not exist, create a new one
            PlayerData newData = new PlayerData();
            newData.profileID = typedID;
            
            // Save to disk
            SaveManager.SaveProfile(newData);
            
            // Set as active
            ProfileManager.Instance.SetActiveProfile(newData);
        }

        // --- ATUALIZA O TEXTO NO ECRÃ COM O ID DO PACIENTE ---
        if (txtCurrentProfile != null)
        {
            txtCurrentProfile.text = ProfileManager.Instance.currentPlayer.profileID;
        }

        // Hide profiles panel and show levels panel
        profileSelectionPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// Loops through all buttons, checks progress, and applies visuals automatically.
    /// </summary>
    private void InitializeAllLevels()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;

            int levelID = i + 1; // Array index starts at 0, Levels start at 1

            // 1. Get saved data for THIS level and the PREVIOUS level
            string saveKey = "Level_" + levelID + "_Stars";
            int currentStars = PlayerPrefs.GetInt(saveKey, 0);

            int prevStars = 0;
            if (levelID > 1)
            {
                prevStars = PlayerPrefs.GetInt("Level_" + (levelID - 1) + "_Stars", 0);
            }

            // 2. Determine if UNLOCKED 
            //  Level 1 AND the Last Level (Free Style) are always unlocked
            bool isFreeStyleLevel = (i == levelButtons.Length - 1);
            bool isUnlocked = (levelID == 1) || isFreeStyleLevel || (prevStars >= 1) || (PlayerPrefs.GetInt("UnlockAll", 0) == 1);
            
            // 3. Get the visual components inside this specific button
            Button btn = levelButtons[i];
            Image bgImage = btn.GetComponent<Image>();
            TMP_Text levelText = btn.GetComponentInChildren<TMP_Text>();

            // IMPORTANT: The object holding the stars MUST be named exactly "StarContainer"
            Transform starContainer = btn.transform.Find("StarContainer");

            // 4. Apply Logic and Visuals
            btn.interactable = isUnlocked;

            if (isUnlocked)
            {
                // -- UNLOCKED VISUALS --
                if (bgImage != null)
                {
                    // If it is the LAST button in the array, apply the Free Style color
                    if (i == levelButtons.Length - 1)
                    {
                        bgImage.color = freeStyleColor;
                    }
                    else // If it is a normal level, apply the green color
                    {
                        bgImage.color = unlockedBGColor;
                    }
                }

                if (levelText != null) levelText.color = Color.white;

                if (starContainer != null)
                {
                    starContainer.gameObject.SetActive(true);

                    // Automatically get the 3 star images inside the container
                    Image[] stars = starContainer.GetComponentsInChildren<Image>();

                    for (int s = 0; s < stars.Length; s++)
                    {
                        // If 's' is less than earned stars, color it Gold. Otherwise, Black.
                        if (s < currentStars)
                            stars[s].color = starEarnedColor;
                        else
                            stars[s].color = starEmptyColor;
                    }
                }

                // Link the button click event
                int captureID = levelID;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => LoadGameLevel(captureID));
            }
            else
            {
                // -- LOCKED VISUALS --
                if (bgImage != null) bgImage.color = lockedBGColor;
                if (levelText != null) levelText.color = Color.gray;

                // Hide the stars entirely if the level is locked
                if (starContainer != null)
                {
                    starContainer.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Loads the scene. Ensure your scenes in Build Settings are named "Level1", "Level2", etc.
    /// </summary>
    public void LoadGameLevel(int levelNumber)
    {
        string sceneName = "Level" + levelNumber;
        Debug.Log($"A carregar a cena: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        InitializeAllLevels(); // Refresh visuals immediately
        Debug.Log("Progresso apagado!");
    }

    public void UnlockAllLevels()
    {
        // Saves a VIP pass in the system
        PlayerPrefs.SetInt("UnlockAll", 1);

        // Updates the menu instantly
        InitializeAllLevels();
        Debug.Log("Todos os níveis foram desbloqueados!");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}