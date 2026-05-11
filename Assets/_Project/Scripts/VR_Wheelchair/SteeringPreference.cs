using UnityEngine;

/// <summary>
/// Holds the player's steering preference for the current session.
/// 
/// Set from the main menu (LevelSelectionPanel) before loading a level.
/// Read by MovementVR (and MovementController for PC) inside each level's Start.
/// 
/// Since this is a static class, the value persists between scene loads
/// (until the application is closed). It does NOT get saved to disk.
/// 
/// Default: FrontSteering (matches the default in MovementVR).
/// </summary>
public static class SteeringPreference
{
    /// <summary>
    /// The steering type that will be applied when the next level loads.
    /// Defaults to FrontSteering at app startup.
    /// </summary>
    public static WheelController.SteeringType CurrentSteering = WheelController.SteeringType.FrontSteering;

    /// <summary>
    /// True if the user has explicitly chosen a steering type this session.
    /// Useful if levels want to respect a per-level default when the user hasn't chosen.
    /// </summary>
    public static bool HasUserChosen = false;

    /// <summary>
    /// Sets the steering preference. Called from the main menu when user clicks a steering button.
    /// </summary>
    public static void SetSteering(WheelController.SteeringType steering)
    {
        CurrentSteering = steering;
        HasUserChosen = true;
        Debug.Log($"[SteeringPreference] Set to: {steering}");
    }

    /// <summary>
    /// Resets to default (FrontSteering, not chosen by user).
    /// Useful for testing or when the app restarts logically.
    /// </summary>
    public static void Reset()
    {
        CurrentSteering = WheelController.SteeringType.FrontSteering;
        HasUserChosen = false;
    }
}