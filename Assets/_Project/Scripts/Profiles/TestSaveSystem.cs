using UnityEngine;

public class TestSaveSystem : MonoBehaviour
{
    // Unity calls this method automatically when the game starts
    void Start()
    {
        // 1. Create a new test player data
        PlayerData newPatient = new PlayerData();
        newPatient.profileID = "User_001";
        newPatient.profileName = "Paciente de Teste"; // PT-PT for the UI later

        // 2. Add a fake session record
        SessionRecord fakeSession = new SessionRecord();
        fakeSession.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        fakeSession.completionTime = 45.5f;
        fakeSession.totalCollisions = 2;
        
        newPatient.sessionHistory.Add(fakeSession);

        // 3. Command the SaveManager to write the file to the disk
        SaveManager.SaveProfile(newPatient);
    }
}