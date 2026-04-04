using UnityEngine;
using UnityEngine.UI;              
using TMPro;                       
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem;     // Required for VR controller input

/// <summary>
/// Manages the game state, timer, scoring, and UI exclusively for the VR Wheelchair.
/// Spawns menus directly in front of the player's VR headset.
/// </summary>
public class LevelManagerVR : MonoBehaviour
{
    public static LevelManagerVR Instance { get; private set; }

    [Header("--- VR Configuration ---")]
    [Tooltip("Main Camera inside the VR Rig (Used to place menus in front of the player)")]
    public Transform vrCamera;
    
    [Tooltip("The VR Controller button to pause the game (e.g., Y or B button)")]
    public InputActionReference vrPauseAction;
    
    [Tooltip("How far away from the player's face the menu will appear (meters)")]
    public float menuSpawnDistance = 1.5f;

    [Header("--- Level Configuration ---")]
    public int levelID = 1;
    public float timeFor3Stars = 60f;
    public float timeFor2Stars = 90f;
    public int maxCollisionsFor3Stars = 0;
    public int maxCollisionsFor2Stars = 2;
    public int maxSlidesFor3Stars = 2;
    public int maxSlidesFor2Stars = 5;

    [Header("--- Current State (Read Only) ---")]
    public float elapsedTime = 0f;
    public int collisionCount = 0;
    public int slideCount = 0;
    public bool isLevelActive = true;
    private bool isPaused = false;

    [Header("--- UI References (In-Game HUD) ---")]
    public GameObject gameHUDPanel;
    public TMP_Text timeText;
    public TMP_Text collisionText;
    public TMP_Text slideText;

    [Header("--- UI References (Pause & End Game) ---")]
    public GameObject pauseMenuPanel;
    public GameObject endGamePanel;

    [Header("--- End Game Panel Elements ---")]
    public TMP_Text finalTimeText;
    public TMP_Text finalCollisionText;
    public TMP_Text finalSlideText;
    public Image star1;
    public Image star2;
    public Image star3;

    [Header("--- Special Buttons ---")]
    public GameObject nextLevelButton; 

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

    private void OnEnable()
    {
        if (vrPauseAction != null && vrPauseAction.action != null)
            vrPauseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (vrPauseAction != null && vrPauseAction.action != null)
            vrPauseAction.action.Disable();
    }

    private void Start()
    {
        isLevelActive = true;
        elapsedTime = 0f;

        if (gameHUDPanel != null) gameHUDPanel.SetActive(true);
        if (endGamePanel != null) endGamePanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        ResumeGame();
    }

    private void Update()
    {
        // Check for VR Pause Input
        if (vrPauseAction != null && vrPauseAction.action != null && vrPauseAction.action.WasPressedThisFrame() && isLevelActive)
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
        isPaused = true;
        Time.timeScale = 0f; 

        if (pauseMenuPanel != null && vrCamera != null)
        {
            PositionMenuInFrontOfPlayer(pauseMenuPanel);
            pauseMenuPanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; 

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Teleports the UI Canvas to exactly where the player is looking
    /// </summary>
    private void PositionMenuInFrontOfPlayer(GameObject menu)
    {
        if (vrCamera == null) return;

        Vector3 spawnPos = vrCamera.position + (vrCamera.forward * menuSpawnDistance);
        
        // Keep it at eye level so the player doesn't have to look up or down too much
        spawnPos.y = vrCamera.position.y; 

        menu.transform.position = spawnPos;
        menu.transform.LookAt(vrCamera);
        
        // UI canvases look backwards when using LookAt, so we flip it 180 degrees
        menu.transform.Rotate(0, 180, 0); 
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
        if (collisionText != null) collisionText.text = $"Colisões: {collisionCount}";
    }

    public void RegisterSlide()
    {
        if (!isLevelActive || isPaused) return;

        slideCount++;
        if (slideText != null) slideText.text = $"Deslizes: {slideCount}";
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

        if (elapsedTime <= timeFor3Stars && collisionCount <= maxCollisionsFor3Stars && slideCount <= maxSlidesFor3Stars)
        {
            stars = 3;
        }
        else if (elapsedTime <= timeFor2Stars && collisionCount <= maxCollisionsFor2Stars && slideCount <= maxSlidesFor2Stars)
        {
            stars = 2;
        }

        string saveKey = "Level_" + levelID + "_Stars";
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
        }

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        if (gameHUDPanel != null) gameHUDPanel.SetActive(false);

        if (endGamePanel != null && vrCamera != null)
        {
            PositionMenuInFrontOfPlayer(endGamePanel);
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
                nextLevelButton.SetActive(nextSceneIndex < SceneManager.sceneCountInBuildSettings);
            }
        }

        Time.timeScale = 0f;
    }

    // =========================================================
    // BUTTON FUNCTIONS (Connect these in the Inspector OnClick)
    // =========================================================

    public void Button_NextLevel()
    {
        Time.timeScale = 1f; 
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(nextSceneIndex);
        else SceneManager.LoadScene("MainMenu");
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
    
    // Adicionado para ser mais fácil ligar o botão de resume no Inspector
    public void Button_ResumeGame()
    {
        ResumeGame();
    }
}