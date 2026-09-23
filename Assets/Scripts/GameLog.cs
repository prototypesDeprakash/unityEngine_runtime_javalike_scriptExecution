using System;
using UnityEngine;

/// <summary>
/// One place for messages the PLAYER should see (not just the Unity Console).
/// MessageFeedUI listens to Posted and shows them on screen.
/// </summary>
public static class GameLog
{
    // message, isWarning
    public static event Action<string, bool> Posted;

    public static void Info(string message)
    {
        Debug.Log(message);
        Posted?.Invoke(message, false);
    }

    public static void Warn(string message)
    {
        Debug.LogWarning(message);
        Posted?.Invoke(message, true);
    }
}
