using UnityEngine;

public class ProfileManager : MonoBehaviour
{
    // Makes this script globally accessible from anywhere
    public static ProfileManager Instance;

    // The currently active patient playing the game
    public PlayerData currentPlayer;

    // The name of our default profile
    private const string DefaultProfileName = "Default";

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
        // Ask the SaveManager to try loading the "Default" profile
        PlayerData defaultProfile = SaveManager.LoadProfile(DefaultProfileName);

        // If it returns null, the file doesn't exist yet
        if (defaultProfile == null)
        {
            // Create a new PlayerData instance
            defaultProfile = new PlayerData();
            defaultProfile.profileID = DefaultProfileName;
            defaultProfile.profileName = "Default User";

            // Save it to disk using your SaveManager
            SaveManager.SaveProfile(defaultProfile);
            Debug.Log("Default profile created successfully!");
        }

        // Automatically set "Default" as the active profile on startup
        SetActiveProfile(defaultProfile);
    }

    // Call this when logging in or creating a new profile
    public void SetActiveProfile(PlayerData data)
    {
        currentPlayer = data;
        Debug.Log("The active patient is now: " + currentPlayer.profileID);
    }
}