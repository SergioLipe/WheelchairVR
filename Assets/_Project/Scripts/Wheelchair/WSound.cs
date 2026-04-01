using UnityEngine;
using System.Collections;

/// <summary>
/// Alternative sound system with fade out effects
/// Uses separate AudioSources for startup and movement loop
/// Compatible with both MovementPC and MovementVR
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WSound : MonoBehaviour
{
    [Header("Audio Source References")]
    [Tooltip("AudioSource with STARTUP sound (plays once)")]
    public AudioSource startupAudioSource;

    [Tooltip("AudioSource with MOVEMENT sound (loops)")]
    public AudioSource movementAudioSource;

    [Header("Fade Configuration")]
    [Tooltip("Time (in seconds) for movement sound to fade out")]
    public float fadeOutTime = 0.2f;

    // Component references
    private MovementPC movementPC;
    private MovementVR movementVR;

    // State
    private bool startupSoundPlayed = false;
    private bool wasAcceleratingCache = false;
    
    // Fade control
    private Coroutine fadeOutCoroutine;
    private float originalMovementVolume;

    void Start()
    {
        InitializeComponents();
    }

    void Update()
    {
        CheckAccelerationState();
        UpdateStartupToLoopTransition();
    }

    private void InitializeComponents()
    {
        movementPC = GetComponent<MovementPC>();
        movementVR = GetComponent<MovementVR>();

        if (startupAudioSource == null || movementAudioSource == null)
        {
            return;
        }

        // Set 2D audio for minimal latency
        startupAudioSource.spatialBlend = 0f;
        movementAudioSource.spatialBlend = 0f;
        
        originalMovementVolume = movementAudioSource.volume;
    }

    private bool IsPlayerAccelerating()
    {
        if (movementPC != null) return movementPC.playerIsAccelerating;
        if (movementVR != null) return movementVR.playerIsAccelerating;
        return false;
    }

    private void CheckAccelerationState()
    {
        bool acceleratingNow = IsPlayerAccelerating();

        // Player started accelerating
        if (acceleratingNow && !wasAcceleratingCache)
        {
            PlayStartupSounds();
        }
        // Player stopped accelerating
        else if (!acceleratingNow && wasAcceleratingCache)
        {
            StopSounds();
        }

        wasAcceleratingCache = acceleratingNow;
    }

    private void UpdateStartupToLoopTransition()
    {
        if (startupSoundPlayed && !startupAudioSource.isPlaying)
        {
            bool acceleratingNow = IsPlayerAccelerating();
            
            if (acceleratingNow && !movementAudioSource.isPlaying)
            {
                movementAudioSource.volume = originalMovementVolume;
                movementAudioSource.Play();
            }
            startupSoundPlayed = false;
        }
    }

    private void PlayStartupSounds()
    {
        // Cancel fade out if active
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        // Stop loop sound immediately
        movementAudioSource.Stop();

        // Restore movement sound volume
        movementAudioSource.volume = originalMovementVolume;

        // Play startup sound
        startupAudioSource.Stop();
        startupAudioSource.Play();
        startupSoundPlayed = true;
    }

    private void StopSounds()
    {
        // Stop startup immediately
        startupAudioSource.Stop();
        startupSoundPlayed = false;

        // Fade out movement sound if playing
        if (movementAudioSource.isPlaying)
        {
            fadeOutCoroutine = StartCoroutine(FadeOut(movementAudioSource, fadeOutTime));
        }
    }

    private IEnumerator FadeOut(AudioSource audioSource, float fadeTime)
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeTime);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        
        // Restore original volume for next time
        audioSource.volume = originalMovementVolume;
        fadeOutCoroutine = null;
    }
}