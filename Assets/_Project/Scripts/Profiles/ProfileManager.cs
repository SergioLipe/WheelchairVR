using UnityEngine;

public class ProfileManager : MonoBehaviour
{
    // Makes this script globally accessible from anywhere
    public static ProfileManager Instance;

    // The currently active patient playing the game
    public PlayerData currentPlayer;

    // The ID of our default profile (kept in English for the file system)
    private const string DefaultProfileId = "Guest";

    void Awake()
    {
        // Ensures only one instance of this manager exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps it alive between scenes
            Debug.Log("Profile system initialized!");
            
            // Check and create the default profile as soon as the game starts
            InitializeDefaultProfile();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDefaultProfile()
    {
        // Ask the SaveManager to try loading the "Guest" profile
        PlayerData defaultProfile = SaveManager.LoadProfile(DefaultProfileId);

        // If it returns null, the file doesn't exist yet
        if (defaultProfile == null)
        {
            // Create a new PlayerData instance
            defaultProfile = new PlayerData();
            defaultProfile.profileID = DefaultProfileId;
            
            // The name in PT-PT for the UI to display
            defaultProfile.profileName = "Visitante";

            // Save it to disk using your SaveManager
            SaveManager.SaveProfile(defaultProfile);
            Debug.Log("Guest profile created successfully!");
        }
        else
        {
            Debug.Log("Guest profile already exists. Skipping creation.");
        }
        
        // Notice: The line SetActiveProfile(defaultProfile) was removed!
        // The game will no longer auto-login to this profile.
    }

    // Call this when logging in or creating a new profile via the UI menus
    public void SetActiveProfile(PlayerData data)
    {
        currentPlayer = data;
        Debug.Log("The active patient is now: " + currentPlayer.profileID);
    }
}