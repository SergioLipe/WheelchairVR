using UnityEngine;

public class ProfileManager : MonoBehaviour
{
    // Makes this script globally accessible from anywhere
    public static ProfileManager Instance;

    // The currently active patient playing the game
    public PlayerData currentPlayer;

    void Awake()
    {
        // Ensures only one instance of this manager exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps it alive between scenes
            Debug.Log("Sistema de perfis iniciado!");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Call this when logging in or creating a new profile
    public void SetActiveProfile(PlayerData data)
    {
        currentPlayer = data;
        Debug.Log("O doente ativo agora é: " + currentPlayer.profileID);
    }
}