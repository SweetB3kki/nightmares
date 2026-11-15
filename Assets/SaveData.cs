using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int version = 1;
    public string currentSceneName = string.Empty;
    public string currentSavePointId = string.Empty;
    public int currentSavePointOrder = -1;
    public SerializableVector3 spawnPosition = SerializableVector3.Zero;

    public int sparkCount;
    public bool hasKey;
    public bool hasGear1;
    public bool hasGear2;
    public bool gear1Inserted;
    public bool gear2Inserted;
    public bool allGearsInserted;
    public bool clocksPuzzleCompleted;
    public bool picturePuzzleCompleted;
    public bool clocksCursorUnlocked;
    public bool pictureCursorUnlocked;

    public SampleSceneSaveData sampleScene = new SampleSceneSaveData();
}

[Serializable]
public sealed class SampleSceneSaveData
{
    public bool chestOpened;
    public bool underCollapseTriggered;
    public List<string> collectedSparkIds = new List<string>();
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public static SerializableVector3 Zero => new SerializableVector3 { x = 0f, y = 0f, z = 0f };

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }

    public static SerializableVector3 FromVector3(Vector3 value)
    {
        return new SerializableVector3
        {
            x = value.x,
            y = value.y,
            z = value.z
        };
    }
}
