using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Main Menu dynamically.
/// Automatically finds and colors the stars and backgrounds for any number of levels.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
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
        // This function runs once when the menu opens
        InitializeAllLevels();
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

            // 2. Determine if UNLOCKED (Level 1 is always true, others need 1+ star in prev level) (also checks if UnlockAll was used)
            bool isUnlocked = (levelID == 1) || (prevStars >= 1) || (PlayerPrefs.GetInt("UnlockAll", 0) == 1);

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
        Debug.Log($"Loading Scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        InitializeAllLevels(); // Refresh visuals immediately
        Debug.Log("Progress Reset!");
    }

    public void UnlockAllLevels()
    {
        // Saves a VIP pass in the system
        PlayerPrefs.SetInt("UnlockAll", 1);

        // Updates the menu instantly
        InitializeAllLevels();
        Debug.Log("All levels have been unlocked!");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}