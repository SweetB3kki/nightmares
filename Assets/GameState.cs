using System.Collections.Generic;
using UnityEngine;

public static class GameState
{
    public static GameObject mainScenePlayer = null;
    public static bool isPaused = false;
    public static bool isInLabirint = false;
    public static bool isSceneTransitioning = false;
    public static bool hasLabyrinthReturnPosition = false;
    public static Vector3 labyrinthReturnPosition = Vector3.zero;

    public static bool bossCompleted = false;
    public static bool bossFightStarted = false;
    public static bool hasGear1 = false;
    public static bool hasGear2 = false;
    public static bool allGearsInserted = false;
    public static bool gear1Inserted = false;
    public static bool gear2Inserted = false;

    // True after the clocks puzzle is fully completed once.
    public static bool clocksPuzzleCompleted = false;

    // True after the picture puzzle is fully completed once.
    public static bool picturePuzzleCompleted = false;

    // Cursor-state flags unlock the stronger interactive cursor after the first click.
    public static bool clocksCursorUnlocked = false;
    public static bool pictureCursorUnlocked = false;

    private static readonly HashSet<string> labyrinthCollectedSparkIds = new HashSet<string>();

    public static void MarkLabyrinthSparkCollected(string sparkId)
    {
        if (!string.IsNullOrWhiteSpace(sparkId))
            labyrinthCollectedSparkIds.Add(sparkId);
    }

    public static bool IsLabyrinthSparkCollected(string sparkId)
    {
        return !string.IsNullOrWhiteSpace(sparkId) && labyrinthCollectedSparkIds.Contains(sparkId);
    }

    public static void ClearLabyrinthSparkState()
    {
        labyrinthCollectedSparkIds.Clear();
    }
}
