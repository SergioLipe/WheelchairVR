using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;

/// <summary>
/// Manages the Freestyle (Level11) scene.
/// - Auto-detects all StarCollectible instances in the scene.
/// - Updates a "X / Y" counter on screen (PC HUD and/or VR HUD).
/// - When the player leaves the scene (back to menu), saves the run to the active profile's history.
/// 
/// Setup:
/// - Place this script on a "FreestyleManager" GameObject in the Level11 scene.
/// - Assign txtStarCounter (PC HUD) and/or txtStarCounterVR (VR HUD) — both work simultaneously.
/// - Whichever Canvas is inactive simply has its text update silently (no error).
/// </summary>
public class FreestyleManager : MonoBehaviour
{
    public static FreestyleManager Instance { get; private set; }

    [Header("--- UI: PC HUD ---")]
    [Tooltip("Drag the TMP_Text from the PC HUD Canvas (StarCounter in HUD_Canvas). Leave null if PC HUD not present.")]
    public TMP_Text txtStarCounter;

    [Header("--- UI: VR HUD ---")]
    [Tooltip("Drag the TMP_Text from the VR HUD Canvas (StarCounter in Canvas_VR panel). Leave null if VR HUD not present.")]
    public TMP_Text txtStarCounterVR;

    [Header("--- UI: Optional split fields ---")]
    [Tooltip("Optional: separate text for 'X' (collected). If set, use these instead of the combined txtStarCounter.")]
    public TMP_Text txtCollected;
    public TMP_Text txtTotal;

    [Header("--- Audio ---")]
    [Tooltip("Optional sound when all stars are collected (e.g. a fanfare). Leave null for nothing.")]
    public AudioClip allCollectedSound;
    public AudioSource audioSource;

    [Header("--- Scene Settings ---")]
    [Tooltip("The level name used when saving to history. Must match what the menu's history reader expects (default: 'Freestyle').")]
    public string sessionLevelName = "Freestyle";

    // Stats for the current run
    private int starsCollected = 0;
    private int starsTotal = 0;

    // Track whether we already saved this run (avoid double-saving)
    private bool hasSavedRun = false;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Auto-detect all stars in the scene
        StarCollectible[] allStars = FindObjectsOfType<StarCollectible>(true);
        starsTotal = allStars.Length;
        starsCollected = 0;

        Debug.Log($"[FreestyleManager] Detected {starsTotal} stars in the scene.");

        UpdateCounterUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by StarCollectible when the player collects a star.
    /// </summary>
    public void OnStarCollected(StarCollectible star)
    {
        starsCollected++;
        UpdateCounterUI();

        Debug.Log($"[FreestyleManager] Star collected. Total: {starsCollected}/{starsTotal}");

        if (starsCollected >= starsTotal)
        {
            OnAllStarsCollected();
        }
    }

    private void OnAllStarsCollected()
    {
        Debug.Log("[FreestyleManager] All stars collected!");

        if (allCollectedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(allCollectedSound);
        }
    }

    /// <summary>
    /// Updates the UI text(s) with the current X / Y.
    /// Updates both PC and VR counters; whichever is null is skipped silently.
    /// </summary>
    private void UpdateCounterUI()
    {
        string text = $"{starsCollected} / {starsTotal}";

        if (txtStarCounter != null) txtStarCounter.text = text;
        if (txtStarCounterVR != null) txtStarCounterVR.text = text;

        if (txtCollected != null) txtCollected.text = starsCollected.ToString();
        if (txtTotal != null) txtTotal.text = starsTotal.ToString();
    }

    // ==========================================
    // SAVE TO HISTORY
    // ==========================================

    /// <summary>
    /// Saves the current run to the active profile's history.
    /// Called automatically when the scene unloads (player goes back to menu)
    /// or can be called manually by the "Main Menu" button.
    /// </summary>
    public void SaveRunToHistory()
    {
        if (hasSavedRun) return; // avoid double-save
        hasSavedRun = true;

        if (ProfileManager.Instance == null || ProfileManager.Instance.currentPlayer == null)
        {
            Debug.LogWarning("[FreestyleManager] No active profile. Run not saved.");
            return;
        }

        PlayerData data = ProfileManager.Instance.currentPlayer;

        SessionRecord record = new SessionRecord();
        record.levelName = sessionLevelName;
        record.completionTime = 0f; // freestyle doesn't track time
        record.totalCollisions = 0;
        record.totalSlides = 0;
        record.sessionDate = DateTime.Now.ToString("o"); // ISO 8601 for parsing later
        record.starsCollected = starsCollected;
        record.starsTotal = starsTotal;

        data.sessionHistory.Add(record);
        SaveManager.SaveProfile(data);

        Debug.Log($"[FreestyleManager] Run saved to history: {starsCollected}/{starsTotal} stars.");
    }

    /// <summary>
    /// Called automatically by Unity when the scene is unloaded.
    /// This ensures the run is saved even if the player closes the game or
    /// uses some other path to exit besides clicking "Main Menu".
    /// </summary>
    private void OnDisable()
    {
        SaveRunToHistory();
    }

    // ==========================================
    // PUBLIC GETTERS
    // ==========================================

    public int StarsCollected => starsCollected;
    public int StarsTotal => starsTotal;
    public bool IsComplete => starsCollected >= starsTotal && starsTotal > 0;
}