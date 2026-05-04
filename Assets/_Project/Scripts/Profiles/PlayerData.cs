using System.Collections.Generic;

[System.Serializable]
public class SessionRecord
{
    // Date of the clinical session
    public string sessionDate;
    // Time taken to complete the exercise
    public float completionTime;
    // Number of times the wheelchair collided with obstacles
    public int totalCollisions;
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