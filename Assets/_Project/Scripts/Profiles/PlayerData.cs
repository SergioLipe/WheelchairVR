using System.Collections.Generic;

[System.Serializable]
public class SessionRecord
{
    // Date of the clinical session
    public string sessionDate;

    // The name of the level played (e.g., "Level1")
    public string levelName;

    // Time taken to complete the exercise
    public float completionTime;

    // Number of direct hits with obstacles
    public int totalCollisions;

    // Number of times the wheelchair scraped the walls
    public int totalSlides;

    // For Freestyle (Level11) runs only: stars collected in this run
    public int starsCollected;

    // For Freestyle (Level11) runs only: total stars available in the scene
    public int starsTotal;
}

[System.Serializable]
public class PlayerData
{
    // Unique identifier for the patient (e.g., "Patient_01")
    public string profileID;

    // Optional real name of the patient
    public string profileName;

    // List containing all past sessions for this patient
    public List<SessionRecord> sessionHistory = new List<SessionRecord>();
}