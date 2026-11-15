using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseMenuSceneLayout : MonoBehaviour
{
    [System.Serializable]
    public sealed class PanelLayout
    {
        public Vector2 size = new Vector2(500f, 397.1725f);
        public Vector2 position = new Vector2(0f, -0.2792f);
        public RectOffsetSerializable padding = new RectOffsetSerializable(32, 32, 26, 24);
        public float spacing = -18f;
        public bool childControlWidth = false;
        public bool childForceExpandWidth = false;
    }

    [System.Serializable]
    public sealed class ElementLayout
    {
        public Vector2 size;
        public Vector2 position;

        public ElementLayout(float width, float height, float posX, float posY)
        {
            size = new Vector2(width, height);
            position = new Vector2(posX, posY);
        }
    }

    [System.Serializable]
    public sealed class ButtonLayout
    {
        public float slotHeight = 92f;
        public ElementLayout visual;
        public ElementLayout hitArea;

        public ButtonLayout(ElementLayout visualLayout, ElementLayout hitAreaLayout)
        {
            visual = visualLayout;
            hitArea = hitAreaLayout;
        }
    }

    [System.Serializable]
    public sealed class RectOffsetSerializable
    {
        public int left;
        public int right;
        public int top;
        public int bottom;

        public RectOffsetSerializable(int leftValue, int rightValue, int topValue, int bottomValue)
        {
            left = leftValue;
            right = rightValue;
            top = topValue;
            bottom = bottomValue;
        }

        public RectOffset ToRectOffset()
        {
            return new RectOffset(left, right, top, bottom);
        }
    }

    [Header("Panel")]
    public PanelLayout panel = new PanelLayout();

    [Header("Title")]
    public ElementLayout title = new ElementLayout(425f, 38.0023f, 0f, 0f);

    [Header("Buttons")]
    public ButtonLayout continueButton = new ButtonLayout(
        new ElementLayout(430f, 242f, 5.800003f, -19.7f),
        new ElementLayout(240.0782f, 47.7533f, 1.8004f, 46.93992f));

    public ButtonLayout settingsButton = new ButtonLayout(
        new ElementLayout(430f, 242f, 2.5198f, 101.6f),
        new ElementLayout(219.9139f, 42.769f, 2.5198f, 121.986f));

    public ButtonLayout menuButton = new ButtonLayout(
        new ElementLayout(430f, 242f, 1.8612f, 203.4f),
        new ElementLayout(223.5972f, 41.5636f, 1.8612f, 133.9892f));
}
