using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System; // Required for DateTime

/// <summary>
/// Manages the game state for VR levels: timer, scoring, save system, pause, and end-game UI.
/// Includes patient session saving to integrate with the History panel in the Main Menu.
/// </summary>
public class LevelManagerVR : MonoBehaviour
{
    public static LevelManagerVR Instance { get; private set; }

    [Header("--- VR Configuration ---")]
    public Transform vrCamera;
    public InputActionReference vrPauseAction;
    public float menuSpawnDistance = 1.5f;

    [Header("--- VR Hand Manager ---")]
    [Tooltip("Drag the Camera Offset (which has the HandVisibilityManager) here")]
    public HandVisibilityManager handVisibilityManager;

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

    /// <summary>
    /// Helper: returns true if we are currently in the Main Menu scene.
    /// In that case, this manager should not run any gameplay logic.
    /// </summary>
    private bool IsInMainMenu()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "MainMenu" || sceneName.Contains("Menu");
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
        // SAFETY: If somehow this manager ends up in the Main Menu, disable itself.
        if (IsInMainMenu())
        {
            Debug.LogWarning("[LevelManagerVR] Detected in MainMenu scene. Disabling itself.");
            this.enabled = false;
            return;
        }

        isLevelActive = true;
        elapsedTime = 0f;

        if (endGamePanel != null) endGamePanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // Tell the hand manager we are playing the level
        if (handVisibilityManager != null)
        {
            handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PlayingLevel;
        }

        Time.timeScale = 1f;

        string currentLevelName = SceneManager.GetActiveScene().name;
        Debug.Log($"Sessão VR iniciada no nível: {currentLevelName}");
    }

    private void Update()
    {
        if (vrPauseAction != null && vrPauseAction.action != null && vrPauseAction.action.WasPressedThisFrame() && isLevelActive)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (isLevelActive && !isPaused)
        {
            elapsedTime += Time.deltaTime;
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

            // Tell the hand manager we are in the Pause Menu
            if (handVisibilityManager != null)
            {
                handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PauseMenu;
            }
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

        // Tell the hand manager we are back in normal gameplay mode
        if (handVisibilityManager != null)
        {
            handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PlayingLevel;
        }
    }

    private void PositionMenuInFrontOfPlayer(GameObject menu)
    {
        if (vrCamera == null) return;

        Vector3 spawnPos = vrCamera.position + (vrCamera.forward * menuSpawnDistance);
        spawnPos.y = vrCamera.position.y;

        menu.transform.position = spawnPos;
        menu.transform.LookAt(vrCamera);
        menu.transform.Rotate(0, 180, 0);
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
    }

    public void RegisterSlide()
    {
        if (!isLevelActive || isPaused) return;
        slideCount++;
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
        if (elapsedTime <= timeFor3Stars && collisionCount <= maxCollisionsFor3Stars && slideCount <= maxSlidesFor3Stars) stars = 3;
        else if (elapsedTime <= timeFor2Stars && collisionCount <= maxCollisionsFor2Stars && slideCount <= maxSlidesFor2Stars) stars = 2;

        // --- GAME PROGRESS SAVE SYSTEM (PlayerPrefs) ---
        // Only update if the new score is better than the previous best
        string saveKey = "Level_" + levelID + "_Stars";
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
            Debug.Log($"Progresso do jogo gravado! Nível {levelID} completo com {stars} estrelas (VR).");
        }

        // --- PATIENT DATA SAVE SYSTEM (JSON) ---
        // Save session record so it appears in the Main Menu history panel
        if (ProfileManager.Instance != null && ProfileManager.Instance.currentPlayer != null)
        {
            SessionRecord newRecord = new SessionRecord();
            newRecord.sessionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            newRecord.levelName = SceneManager.GetActiveScene().name;
            newRecord.completionTime = elapsedTime;
            newRecord.totalCollisions = collisionCount;
            newRecord.totalSlides = slideCount;

            ProfileManager.Instance.currentPlayer.sessionHistory.Add(newRecord);
            SaveManager.SaveProfile(ProfileManager.Instance.currentPlayer);

            Debug.Log("Dados clínicos da sessão VR gravados com sucesso no perfil do paciente!");
        }
        else
        {
            Debug.LogWarning("Aviso (VR): Nenhum perfil de paciente ativo detetado. Os dados clínicos não foram guardados.");
        }

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        Time.timeScale = 0f;

        if (endGamePanel != null && vrCamera != null)
        {
            PositionMenuInFrontOfPlayer(endGamePanel);
            endGamePanel.SetActive(true);

            // Tell the hand manager we are on the end screen (lasers ON)
            if (handVisibilityManager != null)
            {
                handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PauseMenu;
            }

            if (finalTimeText != null) finalTimeText.text = FormatTime(elapsedTime);
            if (finalCollisionText != null) finalCollisionText.text = collisionCount.ToString();
            if (finalSlideText != null) finalSlideText.text = slideCount.ToString();

            if (star1 != null) star1.color = (starCount >= 1) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            if (star2 != null) star2.color = (starCount >= 2) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            if (star3 != null) star3.color = (starCount >= 3) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
        }
    }

    // =========================================================
    // BUTTON FUNCTIONS (Connect these in the Inspector OnClick)
    // =========================================================

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
            Debug.Log("Não há mais níveis! A carregar o Menu Principal.");
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

    public void Button_ResumeGame()
    {
        ResumeGame();
    }
}