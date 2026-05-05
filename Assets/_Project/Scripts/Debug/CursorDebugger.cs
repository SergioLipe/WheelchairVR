using UnityEngine;

public class CursorDebugger : MonoBehaviour
{
    void Update()
    {
        // Loga TODOS os frames, não só quando muda
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[CLICK] Antes: visible={Cursor.visible}, lock={Cursor.lockState}");
        }
    }

    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[CLICK] LateUpdate: visible={Cursor.visible}, lock={Cursor.lockState}");
        }
    }
}