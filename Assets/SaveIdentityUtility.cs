using System.Collections.Generic;
using UnityEngine;

public static class SaveIdentityUtility
{
    public static string GetObjectId(Component component)
    {
        return component == null ? string.Empty : GetObjectId(component.transform);
    }

    public static string GetObjectId(GameObject gameObject)
    {
        return gameObject == null ? string.Empty : GetObjectId(gameObject.transform);
    }

    public static string GetObjectId(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        Stack<string> segments = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            segments.Push(current.name + "#" + current.GetSiblingIndex());
            current = current.parent;
        }

        string sceneName = transform.gameObject.scene.IsValid()
            ? transform.gameObject.scene.name
            : "NoScene";

        return sceneName + ":" + string.Join("/", segments.ToArray());
    }
}
