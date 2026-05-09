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

    // ==========================================
    // NEW: DELETE PROFILE
    // ==========================================

    /// <summary>
    /// Deletes a profile file from disk by its ID.
    /// Returns true if deleted successfully, false if file didn't exist or deletion failed.
    /// </summary>
    public static bool DeleteProfile(string profileID)
    {
        if (string.IsNullOrEmpty(profileID)) return false;

        string fileName = profileID + "_profile.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log("Perfil apagado com sucesso: " + filePath);
                return true;
            }
            else
            {
                Debug.LogWarning("Atenção: Ficheiro de perfil não encontrado para apagar: " + filePath);
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Erro ao apagar perfil '" + profileID + "': " + e.Message);
            return false;
        }
    }

    // ==========================================
    // NEW: RENAME PROFILE
    // ==========================================

    /// <summary>
    /// Renames a profile by copying its data to a new ID and deleting the old one.
    /// Returns true if successful, false if the old profile doesn't exist or the new ID is already taken.
    /// </summary>
    public static bool RenameProfile(string oldID, string newID)
    {
        if (string.IsNullOrEmpty(oldID) || string.IsNullOrEmpty(newID)) return false;
        if (oldID == newID) return true; // nothing to do

        // Check if newID already exists - we don't want to overwrite another profile
        PlayerData existing = LoadProfile(newID);
        if (existing != null)
        {
            Debug.LogWarning("Atenção: Já existe um perfil com o ID '" + newID + "'. Não é possível renomear.");
            return false;
        }

        // Load old profile
        PlayerData data = LoadProfile(oldID);
        if (data == null)
        {
            Debug.LogWarning("Atenção: Perfil '" + oldID + "' não encontrado para renomear.");
            return false;
        }

        // Update the ID inside the data
        data.profileID = newID;

        // Save under new name
        SaveProfile(data);

        // Delete old file
        DeleteProfile(oldID);

        Debug.Log("Perfil renomeado de '" + oldID + "' para '" + newID + "'.");
        return true;
    }
}