using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MuseuVirtual
{
    internal static class MuseumPrompts
    {
        // Keyboard and mouse artwork from Mr. Breakfast's Free Prompts (CC0).
        static readonly System.Collections.Generic.Dictionary<string, Sprite> Icons =
            Resources.LoadAll<Sprite>("Museum/InputPrompts").ToDictionary(sprite => sprite.name);
        internal static readonly Sprite E = Icons["E"], Esc = Icons["Esc"], Z = Icons["Z"], R = Icons["R"];
        internal static readonly Sprite Click = Icons["Click"], Wheel = Icons["Wheel"];

        internal sealed class Hint
        {
            internal readonly string Label;
            internal readonly Sprite[] Keys;
            internal Hint(string label, params Sprite[] keys) { Label = label; Keys = keys; }
        }

        internal static readonly Hint[] Walking = {
            new Hint("Mover", Icons["W"], Icons["A"], Icons["S"], Icons["D"]),
            new Hint("Olhar", Icons["Mouse"]), new Hint("Correr", Icons["Shift"]),
            new Hint("Interagir", E), new Hint("Foco", Z), new Hint("Pausar", Esc)
        };

        internal static float Width(Sprite icon, float height) => height * icon.rect.width / icon.rect.height;

        internal static Image AddIcon(Transform parent, Sprite icon, float height)
        {
            var image = new GameObject(icon.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(Width(icon, height), height);
            return image;
        }

        internal static float DrawIcon(float x, float centerY, float height, Sprite icon)
        {
            var pixels = icon.rect;
            var texture = icon.texture;
            var uv = new Rect(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            float width = Width(icon, height);
            GUI.DrawTextureWithTexCoords(new Rect(x, centerY - height / 2, width, height), texture, uv);
            return width;
        }

        static readonly GUIContent Measure = new GUIContent();

        internal static float TextWidth(string text, GUIStyle style)
        {
            Measure.text = text;
            return Mathf.Ceil(style.CalcSize(Measure).x);
        }

        internal static void DrawAction(Rect row, Sprite key, string label, GUIStyle style,
            bool centered = false, Sprite alternative = null, float height = 28)
        {
            float textWidth = TextWidth(label, style);
            float joinWidth = alternative == null ? 0 : TextWidth("ou", style);
            float total = Width(key, height) + 8 + textWidth;
            if (alternative != null) total += 8 + joinWidth + Width(alternative, height);
            float x = centered ? row.center.x - total / 2 : row.x;
            x += DrawIcon(x, row.center.y, height, key);
            if (alternative != null)
            {
                GUI.Label(new Rect(x + 4, row.y, joinWidth, row.height), "ou", style);
                x += 8 + joinWidth;
                x += DrawIcon(x, row.center.y, height, alternative);
            }
            GUI.Label(new Rect(x + 8, row.y, centered ? textWidth : row.xMax - x - 8, row.height), label, style);
        }
    }
}
