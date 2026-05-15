using UnityEngine;

/// <summary>
/// Attached to each star (GoalIcon) placed in the Freestyle scene.
/// Detects the player entering its trigger zone, plays a sound,
/// notifies the FreestyleManager, and disappears.
/// 
/// Setup:
/// - GameObject must have a Sphere Collider with "Is Trigger" enabled.
/// - GameObject must have the tag/layer that the player's CharacterController can trigger.
/// - The player GameObject (Wheelchair_PC or Wheelchair_VR) must have the "Player" tag.
/// </summary>
[RequireComponent(typeof(Collider))]
public class StarCollectible : MonoBehaviour
{
    [Header("--- Detection ---")]
    [Tooltip("Tag of the player GameObject (usually 'Player'). Both Wheelchair_PC and Wheelchair_VR should have this tag.")]
    public string playerTag = "Player";

    [Header("--- Audio ---")]
    [Tooltip("Sound played when this star is collected. Drag your existing star pickup sound here.")]
    public AudioClip collectSound;

    [Tooltip("Volume of the collect sound (0-1)")]
    [Range(0f, 1f)]
    public float collectVolume = 1f;

    [Header("--- Visual ---")]
    [Tooltip("If true, destroys the star GameObject after collection. If false, just deactivates it.")]
    public bool destroyAfterCollect = false;

    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        // Already collected? ignore
        if (collected) return;

        // Check if it's the player (by tag or its CharacterController root)
        bool isPlayer = other.CompareTag(playerTag);

        // Fallback: check parents in case the collider is on a child of the player
        if (!isPlayer && other.transform.root != null)
        {
            isPlayer = other.transform.root.CompareTag(playerTag);
        }

        if (!isPlayer) return;

        Collect();
    }

    /// <summary>
    /// Marks this star as collected, plays the sound, and notifies the manager.
    /// </summary>
    private void Collect()
    {
        collected = true;

        // Play the sound at the star's position (so it spatializes correctly)
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
        }

        // Notify the FreestyleManager
        if (FreestyleManager.Instance != null)
        {
            FreestyleManager.Instance.OnStarCollected(this);
        }
        else
        {
            Debug.LogWarning("[StarCollectible] FreestyleManager.Instance is null. Star collected but not registered.");
        }

        // Disable or destroy the star
        if (destroyAfterCollect)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Public method to force-collect (useful for testing or cheats).
    /// </summary>
    public void ForceCollect()
    {
        Collect();
    }

    /// <summary>
    /// Returns whether this star has been collected.
    /// </summary>
    public bool IsCollected => collected;
}