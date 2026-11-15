#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateSpriteDissolveArticleGraph
{
    [MenuItem("Tools/HGHTMS/Create Sprite Dissolve Article Graph", false, 1000)]
    private static void ShowDeprecatedNotice()
    {
        Debug.LogWarning("Sprite Dissolve Article Graph has been reverted. Use the baseline radial dissolve instead.");
    }
}
#endif
