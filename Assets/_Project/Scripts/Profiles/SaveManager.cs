using UnityEngine;
using System.IO;

public static class SaveManager
{
    // Saves the player data to a JSON file
    public static void SaveProfile(PlayerData data)
    {
        // Convert the class into a formatted JSON string
        string jsonText = JsonUtility.ToJson(data, true);

        // Define the file name and path dynamically
        string fileName = data.profileID + "_profile.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        // Write the text to the local disk
        File.WriteAllText(filePath, jsonText);
        
        // Console message in PT-PT for the user/developer
        Debug.Log("Perfil guardado com sucesso no caminho: " + filePath);
    }

    // Loads the player data from a JSON file
    public static PlayerData LoadProfile(string profileID)
    {
        string fileName = profileID + "_profile.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        // Check if the file actually exists before reading
        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            PlayerData loadedData = JsonUtility.FromJson<PlayerData>(jsonText);
            
            // Console message in PT-PT
            Debug.Log("Perfil carregado com sucesso!");
            return loadedData;
        }
        else
        {
            // Warning message in PT-PT
            Debug.LogWarning("Atenção: Ficheiro de perfil não encontrado em: " + filePath);
            return null;
        }
    }

    // Searches the folder and returns a list of all existing profile IDs
    public static string[] GetAllProfileIDs()
    {
        // Get all files ending with "_profile.json" in the folder
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*_profile.json");
        string[] profileIDs = new string[files.Length];

        for (int i = 0; i < files.Length; i++)
        {
            // Extract just the file name and remove the "_profile.json" extension to get the clean ID
            string fileName = Path.GetFileName(files[i]);
            profileIDs[i] = fileName.Replace("_profile.json", "");
        }
        
        return profileIDs;
    }
}