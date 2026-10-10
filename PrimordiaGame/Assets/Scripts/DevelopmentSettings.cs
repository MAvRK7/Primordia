using System;
using UnityEngine;

/// <summary>Saved development options shared by the main menu and spawned players.</summary>
public static class DevelopmentSettings
{
    const string InvulnerabilityKey = "Primordia.Dev.Invulnerability";

    public static event Action<bool> InvulnerabilityChanged;

    public static bool InvulnerabilityEnabled
    {
        get => PlayerPrefs.GetInt(InvulnerabilityKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(InvulnerabilityKey, value ? 1 : 0);
            PlayerPrefs.Save();
            InvulnerabilityChanged?.Invoke(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetListeners() => InvulnerabilityChanged = null;
}
