using UnityEngine;
using UnityEngine.UI;              // Required for Image manipulation
using TMPro;                       // Required for TextMeshPro elements
using UnityEngine.SceneManagement; // Required for reloading scenes or loading the Menu
using System;                      // Required for DateTime

/// <summary>
/// Manages the game state, timer, scoring system, saving progress, pause system, and UI updates.
/// Now includes patient data saving for clinical history.
/// </summary>
public class LevelManager_PC : MonoBehaviour
{
    // Singleton instance to allow easy access from other scripts
    public static LevelManager_PC Instance { get; private set; }

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
    public GameObject nextLevelButton; // <--- NEW: Reference for the Next Level Button

    private void Awake()
    {
        // Singleton Pattern
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
        // Ensure the game starts in a playable state
        isLevelActive = true;
        elapsedTime = 0f;

        // Ensure correct panels are visible/hidden
        if (gameHUDPanel != null) gameHUDPanel.SetActive(true);
        if (endGamePanel != null) endGamePanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        ResumeGame();
        
        string currentLevelName = SceneManager.GetActiveScene().name;
        Debug.Log($"Sessão iniciada no nível: {currentLevelName}");
    }

    private void Update()
    {
        // Check for Pause Input (ESC)
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

        // Only update the timer if the level is active and NOT paused
        if (isLevelActive && !isPaused)
        {
            elapsedTime += Time.deltaTime;
            UpdateUI();
        }
    }

    // Ensure cursor is visible and unlocked when game is paused or ended
    private void LateUpdate()
    {
        if (Time.timeScale == 0f)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Pauses the game, shows the cursor, and opens the pause menu
    /// </summary>
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freezes physics and movement

        // Unlock and show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Resumes the game, hides the cursor, and closes the pause menu
    /// </summary>
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Unfreezes the game

        // Lock and hide cursor for gameplay (Assuming First Person / Wheelchair view)
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
            // Update the live timer
            timeText.text = FormatTime(elapsedTime);
        }
    }

    /// <summary>
    /// Helper to format seconds into MM:SS string
    /// </summary>
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
            // Update Debug UI if needed
            collisionText.text = $"Colisões: {collisionCount}";
        }
    }

    public void RegisterSlide()
    {
        if (!isLevelActive || isPaused) return;

        slideCount++;

        if (slideText != null)
        {
            // Update Debug UI if needed
            slideText.text = $"Deslizes: {slideCount}";
        }
    }

    public void FinishLevel()
    {
        if (!isLevelActive) return;

        isLevelActive = false;

        // Ensure pause menu is closed
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        CalculateResults();
    }

    private void CalculateResults()
    {
        int stars = 1; // Minimum 1 star

        // 3 STARS CRITERIA (GOLD)
        if (elapsedTime <= timeFor3Stars &&
            collisionCount <= maxCollisionsFor3Stars &&
            slideCount <= maxSlidesFor3Stars)
        {
            stars = 3;
        }
        // 2 STARS CRITERIA (SILVER)
        else if (elapsedTime <= timeFor2Stars &&
                 collisionCount <= maxCollisionsFor2Stars &&
                 slideCount <= maxSlidesFor2Stars)
        {
            stars = 2;
        }

        // --- GAME PROGRESS SAVE SYSTEM (PlayerPrefs) ---
        string saveKey = "Level_" + levelID + "_Stars";
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
            Debug.Log($"Progresso do jogo gravado! Nível {levelID} completo com {stars} estrelas.");
        }

        // --- PATIENT DATA SAVE SYSTEM (JSON) ---
        if (ProfileManager.Instance != null && ProfileManager.Instance.currentPlayer != null)
        {
            SessionRecord newRecord = new SessionRecord();
            newRecord.sessionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            newRecord.levelName = SceneManager.GetActiveScene().name;
            newRecord.completionTime = elapsedTime;
            newRecord.totalCollisions = collisionCount;
            newRecord.totalSlides = slideCount;

            // Add the record to the patient and save to disk
            ProfileManager.Instance.currentPlayer.sessionHistory.Add(newRecord);
            SaveManager.SaveProfile(ProfileManager.Instance.currentPlayer);
            
            Debug.Log("Dados clínicos da sessão gravados com sucesso no perfil do paciente!");
        }
        else
        {
            Debug.LogWarning("Aviso: Nenhum perfil de paciente ativo detetado. Os dados clínicos não foram guardados.");
        }

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        // 1. Hide the Game HUD (The speedometer, instructions, etc.)
        if (gameHUDPanel != null)
            gameHUDPanel.SetActive(false);

        // 2. Hide interface (from Movement script)
        Movement wheelchairMovement = FindObjectOfType<Movement>();
        if (wheelchairMovement != null)
        {
            wheelchairMovement.showInterface = false;
        }

        // 3. Show the End Game Panel
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);

            // Update the Report Card Stats
            if (finalTimeText != null) finalTimeText.text = FormatTime(elapsedTime);
            if (finalCollisionText != null) finalCollisionText.text = collisionCount.ToString();
            if (finalSlideText != null) finalSlideText.text = slideCount.ToString();

            // Update Star Images (Turn them Gold or Dark based on score)
            Color activeColor = Color.white;  // Or your Gold Color
            Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark Grey

            if (star1 != null) star1.color = (starCount >= 1) ? activeColor : inactiveColor;
            if (star2 != null) star2.color = (starCount >= 2) ? activeColor : inactiveColor;
            if (star3 != null) star3.color = (starCount >= 3) ? activeColor : inactiveColor;

            // === NEXT LEVEL BUTTON LOGIC ===
            if (nextLevelButton != null)
            {
                // Check if there is a next scene in Build Settings
                int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

                if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
                {
                    nextLevelButton.SetActive(true); // There is a next level
                }
                else
                {
                    nextLevelButton.SetActive(false); // Last level, hide button
                }
            }
        }

        // 4. Freeze game and show cursor
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // =========================================================
    // BUTTON FUNCTIONS (Connect these in the Inspector OnClick)
    // =========================================================

    /// <summary>
    /// Loads the Next Level if available
    /// </summary>
    public void Button_NextLevel()
    {
        Time.timeScale = 1f; // Always unfreeze before loading

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // Safety check to ensure scene exists
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // Fallback to menu if no next level
            Debug.Log("Não há mais níveis! A carregar o Menu Principal.");
            SceneManager.LoadScene("MainMenu");
        }
    }

    /// <summary>
    /// Reloads the current scene
    /// </summary>
    public void Button_RetryLevel()
    {
        Time.timeScale = 1f; // Always unfreeze before loading
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Loads the Main Menu scene
    /// </summary>
    public void Button_MainMenu()
    {
        Time.timeScale = 1f; // Always unfreeze before loading
        SceneManager.LoadScene("MainMenu");
    }
}