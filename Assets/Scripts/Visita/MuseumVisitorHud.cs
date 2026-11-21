using Lassu.Museum;
using UnityEngine;

namespace MuseuVirtual
{
    [RequireComponent(typeof(MuseumVisitor))]
    public sealed class MuseumVisitorHud : MonoBehaviour
    {
        MuseumVisitor visitor;
        MuseumGateInteractor gates;
        GUIStyle heading, body, caption, button, centered, menuButton, keycap;
        readonly Color ink = new Color(0.035f, 0.065f, 0.105f, 0.96f);
        readonly Color accent = new Color(0.34f, 0.83f, 0.78f);
        void Awake()
        {
            visitor = GetComponent<MuseumVisitor>();
            gates = GetComponent<MuseumGateInteractor>();
        }
        void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true };
            caption = new GUIStyle(body) { fontSize = 16 };
            centered = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 21, padding = new RectOffset(18,18,12,12) };
            menuButton = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            keycap = new GUIStyle(menuButton) { fontSize = 15, fontStyle = FontStyle.Bold };
            heading.normal.textColor = body.normal.textColor = caption.normal.textColor = centered.normal.textColor = Color.white;
        }
        void Panel(Rect rect)
        {
            GUI.color = ink; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.color = accent; GUI.DrawTexture(new Rect(rect.x,rect.y,4,rect.height),Texture2D.whiteTexture); GUI.color = Color.white;
        }
        static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        bool MenuButton(Rect rect, string label, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            var border = primary ? accent : new Color(0.38f, 0.49f, 0.54f);
            var face = primary ? new Color(0.18f, 0.47f, 0.46f) : new Color(0.09f, 0.15f, 0.19f);
            Fill(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, 0.25f));
            Fill(rect, hover ? accent : border);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), hover ? Color.Lerp(face, accent, 0.15f) : face);
            menuButton.normal.textColor = Color.white;
            return GUI.Button(rect, label, menuButton);
        }

        void DrawControls()
        {
            var panel = new Rect(340, 820, 920, 50);
            Fill(panel, new Color(ink.r, ink.g, ink.b, 0.78f));
            float x = panel.x + 18;
            DrawKey(ref x, "W", 27); DrawKey(ref x, "A", 27); DrawKey(ref x, "S", 27); DrawKey(ref x, "D", 27);
            DrawHint(ref x, "mover", 86);
            DrawKey(ref x, "Mouse", 64); DrawHint(ref x, "olhar", 78);
            DrawKey(ref x, "Shift", 52); DrawHint(ref x, "correr", 86);
            DrawKey(ref x, "E", 27); DrawHint(ref x, "interagir", 110);
            DrawKey(ref x, "Z", 27); DrawHint(ref x, "foco", 75);
            DrawKey(ref x, "Esc", 42); DrawHint(ref x, "pausar", 80);
        }

        void DrawKey(ref float x, string label, float width)
        {
            Fill(new Rect(x, 830, width, 30), new Color(0.42f, 0.55f, 0.57f, 0.8f));
            Fill(new Rect(x + 1, 831, width - 2, 26), new Color(0.1f, 0.16f, 0.2f));
            keycap.normal.textColor = new Color(0.88f, 0.95f, 0.94f);
            GUI.Label(new Rect(x, 829, width, 30), label, keycap);
            x += width + 5;
        }

        void DrawHint(ref float x, string label, float width)
        {
            GUI.Label(new Rect(x + 4, 835, width, 26), label, caption);
            x += width;
        }

        void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600*scale)*0.5f, (Screen.height-900*scale)*0.5f), Quaternion.identity, Vector3.one*scale);
            // In the gallery, the Canvas shows the room banner and the controls hint.
            bool gallery = MuseumHUD.Instance != null;
            if (visitor.IsPaused)
            {
                const float menuWidth = 600;
#if UNITY_STANDALONE
                const float menuHeight = 400;
#else
                const float menuHeight = 330;
#endif
                var menu = new Rect((1600 - menuWidth) / 2, (900 - menuHeight) / 2, menuWidth, menuHeight);
                Panel(menu);
                float left = menu.x + 36;
                float top = menu.y + 30;
                GUI.Label(new Rect(left, top, 528, 48), "Explore o museu", heading);
                GUI.Label(new Rect(left, top + 66, 528, 65), "Explore as salas e aproxime-se das peças para conhecer o acervo.", body);
                if (MenuButton(new Rect(left, top + 154, 528, 54), "Entrar / continuar a visita", true)) visitor.Resume();
                if (MenuButton(new Rect(left, top + 220, 528, 50), "Voltar ao ponto inicial")) visitor.ResetVisitor();
#if UNITY_STANDALONE
                if (MenuButton(new Rect(left, top + 282, 528, 50), "Sair do museu"))
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
#endif
                // Buttons consume their own clicks; a click anywhere else recaptures the cursor.
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0) { visitor.Resume(); Event.current.Use(); }
            }
            else if (visitor.InspectedExhibit != null)
            {
                var item = visitor.InspectedExhibit;
                Panel(new Rect(1110,220,450,520));
                GUI.Label(new Rect(1138,246,394,80), item.displayName, heading);
                GUI.Label(new Rect(1138,343,394,180), item.description, body);
                GUI.Label(new Rect(1138,539,394,92), item.CanPickUp ? "Arraste para girar · R para alinhar\nRoda do mouse para aproximar\nE ou Esc para devolver" : "Peça fixa na exposição.\nE ou Esc para continuar.", caption);
                if (GUI.Button(new Rect(1138,659,394,52), item.CanPickUp ? "Devolver à exposição" : "Continuar a visita", button)) visitor.EndInspection();
            }
            else
            {
                bool targeted = visitor.FocusedExhibit != null || (gates != null && gates.FocusedGate != null);
                GUI.color = targeted ? accent : Color.white;
                GUI.DrawTexture(new Rect(797,447,6,6),Texture2D.whiteTexture); GUI.color = Color.white;
                if (visitor.FocusedExhibit != null)
                {
                    Panel(new Rect(500,680,600,82));
                    GUI.Label(new Rect(520,691,560,58), visitor.FocusedExhibit.displayName + "\n" + (visitor.FocusedExhibit.CanPickUp ? "Pegar e examinar" : "Conhecer a peça"), centered);
                }
            }
            if (!gallery && visitor.InspectedExhibit == null) DrawControls();
            if (visitor.IsFocusMode)
            {
                Panel(new Rect(500,120,600,70));
                GUI.Label(new Rect(518,130,564,52), "FOCO  ·  Z para sair\nMover ou realizar outra ação encerra o foco", centered);
            }
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
