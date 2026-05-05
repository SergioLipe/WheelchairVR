using UnityEngine;
using UnityEngine.UI;              // Required for Image manipulation
using TMPro;                       // Required for TextMeshPro elements
using UnityEngine.SceneManagement; // Required for reloading scenes or loading the Menu

/// <summary>
/// Manages the game state, timer, scoring system, saving progress, pause system, and UI updates.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // Singleton instance to allow easy access from other scripts
    public static LevelManager Instance { get; private set; }

    [Header("--- Level Configuration ---")]
    [Tooltip("Unique ID for this level (1, 2, 3...). Crucial for the Save System.")]
    public int levelID = 1;

    [Header("--- Star Criteria (Time) ---")]
    [Tooltip("Maximum time in seconds to achieve 3 stars")]
    public float timeFor3Stars = 60f;

    [Tooltip("Maximum time in seconds to achieve 2 stars")]
    public float timeFor2Stars = 90f;

    [Header("--- Star Criteria (Collisions) ---")]
    [Tooltip("Maximum number of collisions allowed to achieve 3 stars")]
    public int maxCollisionsFor3Stars = 0;

    [Tooltip("Maximum number of collisions allowed to achieve 2 stars")]
    public int maxCollisionsFor2Stars = 2;

    [Header("--- Star Criteria (Slides) ---")]
    [Tooltip("Maximum number of slides allowed to achieve 3 stars")]
    public int maxSlidesFor3Stars = 2;

    [Tooltip("Maximum number of slides allowed to achieve 2 stars")]
    public int maxSlidesFor2Stars = 5;

    [Header("--- Current State (Read Only) ---")]
    public float elapsedTime = 0f;
    public int collisionCount = 0;
    public int slideCount = 0;
    public bool isLevelActive = true;
    private bool isPaused = false;

    [Header("--- UI References (In-Game HUD) ---")]
    [Tooltip("The main HUD panel containing the speedometer and timers")]
    public GameObject gameHUDPanel;

    [Tooltip("Reference to the Timer Text (In-Game)")]
    public TMP_Text timeText;

    [Tooltip("Reference to the Collision Counter Text (In-Game)")]
    public TMP_Text collisionText;

    [Tooltip("Reference to the Slide Counter Text (In-Game)")]
    public TMP_Text slideText;

    [Header("--- UI References (Pause & End Game) ---")]
    [Tooltip("Reference to the Pause Menu Panel (Must be disabled at start)")]
    public GameObject pauseMenuPanel;

    [Tooltip("Reference to the End Game Panel (Must be disabled at start)")]
    public GameObject endGamePanel;

    [Header("--- End Game Panel Elements ---")]
    [Tooltip("Text to display final time in the report card")]
    public TMP_Text finalTimeText;

    [Tooltip("Text to display final collision count in the report card")]
    public TMP_Text finalCollisionText;

    [Tooltip("Text to display final slide count in the report card")]
    public TMP_Text finalSlideText;

    [Tooltip("Image for the 1st Star")]
    public Image star1;

    [Tooltip("Image for the 2nd Star")]
    public Image star2;

    [Tooltip("Image for the 3rd Star")]
    public Image star3;

    [Header("--- Special Buttons ---")]
    [Tooltip("The Next Level Button (Drag here to hide it automatically on last level)")]
    public GameObject nextLevelButton;

    /// <summary>
    /// Helper: returns true if we are currently in the Main Menu scene.
    /// In that case, this manager should not touch the cursor at all.
    /// </summary>
    private bool IsInMainMenu()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "MainMenu" || sceneName.Contains("Menu");
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // SAFETY: If somehow this manager is alive in the Main Menu, do nothing.
        if (IsInMainMenu())
        {
            Debug.LogWarning("[LevelManager] Detected in MainMenu scene. Disabling itself to avoid cursor conflicts.");
            this.enabled = false;
            return;
        }

        isLevelActive = true;
        elapsedTime = 0f;

        if (gameHUDPanel != null) gameHUDPanel.SetActive(true);
        if (endGamePanel != null) endGamePanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        ResumeGame();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && isLevelActive)
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        if (isLevelActive && !isPaused)
        {
            elapsedTime += Time.deltaTime;
            UpdateUI();
        }
    }

    public void PauseGame()
    {
        if (IsInMainMenu()) return;

        isPaused = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        // CRITICAL FIX: Never lock/hide cursor if we're in the Main Menu
        if (IsInMainMenu())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        isPaused = false;
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    private void UpdateUI()
    {
        if (timeText != null)
        {
            timeText.text = FormatTime(elapsedTime);
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        string minutes = Mathf.Floor(timeInSeconds / 60).ToString("00");
        string seconds = (timeInSeconds % 60).ToString("00");
        return $"{minutes}:{seconds}";
    }

    public void RegisterStrongCollision(string objectHit)
    {
        if (!isLevelActive || isPaused) return;

        collisionCount++;

        if (collisionText != null)
        {
            collisionText.text = $"Colisões: {collisionCount}";
        }
    }

    public void RegisterSlide()
    {
        if (!isLevelActive || isPaused) return;

        slideCount++;

        if (slideText != null)
        {
            slideText.text = $"Deslizes: {slideCount}";
        }
    }

    public void FinishLevel()
    {
        if (!isLevelActive) return;

        isLevelActive = false;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        CalculateResults();
    }

    private void CalculateResults()
    {
        int stars = 1;

        if (elapsedTime <= timeFor3Stars &&
            collisionCount <= maxCollisionsFor3Stars &&
            slideCount <= maxSlidesFor3Stars)
        {
            stars = 3;
        }
        else if (elapsedTime <= timeFor2Stars &&
                 collisionCount <= maxCollisionsFor2Stars &&
                 slideCount <= maxSlidesFor2Stars)
        {
            stars = 2;
        }

        string saveKey = "Level_" + levelID + "_Stars";
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
            Debug.Log($"Game Saved! Level {levelID} completed with {stars} stars.");
        }

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        if (gameHUDPanel != null)
            gameHUDPanel.SetActive(false);

        Movement wheelchairMovement = FindObjectOfType<Movement>();
        if (wheelchairMovement != null)
        {
            wheelchairMovement.showInterface = false;
        }

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);

            if (finalTimeText != null) finalTimeText.text = FormatTime(elapsedTime);
            if (finalCollisionText != null) finalCollisionText.text = collisionCount.ToString();
            if (finalSlideText != null) finalSlideText.text = slideCount.ToString();

            Color activeColor = Color.white;
            Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);

            if (star1 != null) star1.color = (starCount >= 1) ? activeColor : inactiveColor;
            if (star2 != null) star2.color = (starCount >= 2) ? activeColor : inactiveColor;
            if (star3 != null) star3.color = (starCount >= 3) ? activeColor : inactiveColor;

            if (nextLevelButton != null)
            {
                int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

                if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
                {
                    nextLevelButton.SetActive(true);
                }
                else
                {
                    nextLevelButton.SetActive(false);
                }
            }
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Button_NextLevel()
    {
        Time.timeScale = 1f;

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No more levels! Loading Main Menu.");
            SceneManager.LoadScene("MainMenu");
        }
    }

    public void Button_RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Button_MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}