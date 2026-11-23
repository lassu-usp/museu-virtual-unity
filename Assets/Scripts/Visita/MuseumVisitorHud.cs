using Lassu.Museum;
using UnityEngine;

namespace MuseuVirtual
{
    [RequireComponent(typeof(MuseumVisitor))]
    public sealed class MuseumVisitorHud : MonoBehaviour
    {
        MuseumVisitor visitor;
        MuseumGateInteractor gates;
        readonly PatinhoPanelGuide panelGuide = new PatinhoPanelGuide();
        GUIStyle heading, body, caption, centered, menuButton;
        float controlsWidth;
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
            caption = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            centered = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            menuButton = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            heading.normal.textColor = body.normal.textColor = caption.normal.textColor = centered.normal.textColor = Color.white;
            controlsWidth = 40;
            foreach (var hint in MuseumPrompts.Walking)
            {
                foreach (var key in hint.Keys) controlsWidth += MuseumPrompts.Width(key, 28) + 3;
                controlsWidth += 5 + MuseumPrompts.TextWidth(hint.Label, caption) + 22;
            }
            controlsWidth -= 22;
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

        bool MenuButton(Rect rect, string label, bool primary = false, Sprite icon = null, Sprite alternative = null)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            var border = primary ? accent : new Color(0.38f, 0.49f, 0.54f);
            var face = primary ? new Color(0.18f, 0.47f, 0.46f) : new Color(0.09f, 0.15f, 0.19f);
            Fill(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, 0.25f));
            Fill(rect, hover ? accent : border);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), hover ? Color.Lerp(face, accent, 0.15f) : face);
            menuButton.normal.textColor = Color.white;
            if (icon == null) return GUI.Button(rect, label, menuButton);
            bool clicked = GUI.Button(rect, GUIContent.none, menuButton);
            MuseumPrompts.DrawAction(rect, icon, label, menuButton, true, alternative);
            return clicked;
        }

        void DrawControls()
        {
            var panel = new Rect((1600 - controlsWidth) / 2, 823, controlsWidth, 50);
            Fill(panel, new Color(ink.r, ink.g, ink.b, 0.9f));
            float x = panel.x + 20;
            foreach (var hint in MuseumPrompts.Walking)
            {
                foreach (var key in hint.Keys) x += MuseumPrompts.DrawIcon(x, panel.center.y, 28, key) + 3;
                x += 5;
                float width = MuseumPrompts.TextWidth(hint.Label, caption);
                GUI.Label(new Rect(x, panel.y, width, panel.height), hint.Label, caption);
                x += width + 22;
            }
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
                if (MenuButton(new Rect(left, top + 154, 528, 54), "Continuar a visita", true, MuseumPrompts.Click)) visitor.Resume();
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
                if (item.OperatorPanel != null)
                {
                    panelGuide.Draw(item.OperatorPanel, visitor);
                    GUI.matrix = Matrix4x4.identity;
                    return;
                }
                Panel(new Rect(1110,220,450,520));
                GUI.Label(new Rect(1138,246,394,80), item.displayName, heading);
                GUI.Label(new Rect(1138,343,394,180), item.description, body);
                if (item.CanPickUp)
                {
                    MuseumPrompts.DrawAction(new Rect(1138, 525, 394, 36), MuseumPrompts.Click, "Arrastar para girar", caption);
                    MuseumPrompts.DrawAction(new Rect(1138, 562, 394, 36), MuseumPrompts.Wheel, "Aproximar ou afastar", caption);
                    MuseumPrompts.DrawAction(new Rect(1138, 599, 394, 36), MuseumPrompts.R, "Alinhar a peça", caption);
                }
                else GUI.Label(new Rect(1138, 539, 394, 45), "Peça fixa na exposição.", caption);
                if (MenuButton(new Rect(1138,659,394,52), item.CanPickUp ? "Devolver à exposição" : "Continuar a visita",
                    false, MuseumPrompts.E, MuseumPrompts.Esc)) visitor.EndInspection();
            }
            else
            {
                bool targeted = visitor.FocusedExhibit != null || (gates != null && gates.FocusedGate != null);
                GUI.color = targeted ? accent : Color.white;
                GUI.DrawTexture(new Rect(797,447,6,6),Texture2D.whiteTexture); GUI.color = Color.white;
                if (visitor.FocusedExhibit != null)
                {
                    Panel(new Rect(570,686,460,90));
                    var item = visitor.FocusedExhibit;
                    string action = item.OperatorPanel != null ? "Operar o painel" :
                        item.CanPickUp ? "Pegar e examinar" : "Conhecer a peça";
                    GUI.Label(new Rect(582,692,436,34), item.displayName, centered);
                    MuseumPrompts.DrawAction(new Rect(582, 729, 436, 36), MuseumPrompts.E, action, caption, true);
                }
            }
            if (!gallery && visitor.InspectedExhibit == null && !visitor.IsFocusMode) DrawControls();
            if (visitor.IsFocusMode)
            {
                Panel(new Rect(583,120,434,80));
                MuseumPrompts.DrawAction(new Rect(595, 127, 410, 36), MuseumPrompts.Z, "Sair do foco", caption, true);
                GUI.Label(new Rect(595, 167, 410, 25), "Mover ou usar outro comando encerra o foco.", menuButton);
            }
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
