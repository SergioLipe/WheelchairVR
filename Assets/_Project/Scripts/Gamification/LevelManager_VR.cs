using UnityEngine;
using UnityEngine.UI;              
using TMPro;                       
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem;     

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
        isLevelActive = true;
        elapsedTime = 0f;

        if (endGamePanel != null) endGamePanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // Avisa o gestor de mãos que estamos a jogar o nível
        if (handVisibilityManager != null)
        {
            handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PlayingLevel;
        }
        
        Time.timeScale = 1f; 
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
            
            // Avisa o gestor de mãos que estamos na Pausa
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

        // Avisa o gestor de mãos para voltar ao modo de jogo normal
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

        PlayerPrefs.SetInt("Level_" + levelID + "_Stars", stars);
        PlayerPrefs.Save();

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        Time.timeScale = 0f;

        if (endGamePanel != null && vrCamera != null)
        {
            PositionMenuInFrontOfPlayer(endGamePanel);
            endGamePanel.SetActive(true);

            // Avisa o gestor de mãos que estamos no ecrã final (Lasers ON)
            if (handVisibilityManager != null)
            {
                handVisibilityManager.currentMode = HandVisibilityManager.GameMode.PauseMenu;
            }

            if (finalTimeText != null) finalTimeText.text = FormatTime(elapsedTime);
            if (finalCollisionText != null) finalCollisionText.text = collisionCount.ToString();
            if (finalSlideText != null) finalSlideText.text = slideCount.ToString();

            star1.color = (starCount >= 1) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            star2.color = (starCount >= 2) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            star3.color = (starCount >= 3) ? Color.white : new Color(0.3f, 0.3f, 0.3f);
        }
    }

    // Funções para ligar no evento "On Click ()" dos botões
    public void Button_NextLevel() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); }
    public void Button_RetryLevel() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void Button_MainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public void Button_ResumeGame() { ResumeGame(); }
}