using UnityEngine;

namespace MuseuVirtual
{
    [RequireComponent(typeof(MuseumVisitor))]
    public sealed class MuseumVisitorHud : MonoBehaviour
    {
        MuseumVisitor visitor;
        GUIStyle heading, body, caption, button, centered;
        readonly Color ink = new Color(0.035f, 0.065f, 0.105f, 0.96f);
        readonly Color accent = new Color(0.34f, 0.83f, 0.78f);
        void Awake() => visitor = GetComponent<MuseumVisitor>();
        void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true };
            caption = new GUIStyle(body) { fontSize = 16 };
            centered = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 21, padding = new RectOffset(18,18,12,12) };
            heading.normal.textColor = body.normal.textColor = caption.normal.textColor = centered.normal.textColor = Color.white;
        }
        void Panel(Rect rect)
        {
            GUI.color = ink; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.color = accent; GUI.DrawTexture(new Rect(rect.x,rect.y,4,rect.height),Texture2D.whiteTexture); GUI.color = Color.white;
        }
        void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600*scale)*0.5f, (Screen.height-900*scale)*0.5f), Quaternion.identity, Vector3.one*scale);
            Panel(new Rect(32,28,430,76));
            GUI.Label(new Rect(52,39,390,26), "LASSU · USP  /  MUSEU VIRTUAL", caption);
            GUI.Label(new Rect(52,65,390,30), "Tecnologia em suas mãos", body);
            if (visitor.IsPaused)
            {
                Panel(new Rect(490,215,620,475));
                GUI.Label(new Rect(528,246,540,40), "Explore o museu", heading);
                GUI.Label(new Rect(528,310,540,80), "Explore as salas e aproxime-se das peças para conhecer o acervo.", body);
                GUI.Label(new Rect(528,407,540,115), "W A S D   mover     ·     Mouse   olhar\nShift   correr     ·     E   interagir\nEsc   liberar o cursor e pausar", body);
                if (GUI.Button(new Rect(528,557,540,55), "Entrar / continuar a visita", button)) visitor.Resume();
                if (GUI.Button(new Rect(528,625,540,38), "Voltar ao ponto inicial", caption)) visitor.ResetVisitor();
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
                GUI.color = visitor.FocusedExhibit == null ? Color.white : accent;
                GUI.DrawTexture(new Rect(797,447,6,6),Texture2D.whiteTexture); GUI.color = Color.white;
                if (visitor.FocusedExhibit != null)
                {
                    Panel(new Rect(500,680,600,82));
                    GUI.Label(new Rect(520,691,560,58), visitor.FocusedExhibit.displayName + "\n" + (visitor.FocusedExhibit.CanPickUp ? "Pegar e examinar" : "Conhecer a peça"), centered);
                }
                Panel(new Rect(340,815,920,55));
                GUI.Label(new Rect(357,826,886,34), "WASD  mover     ·     Mouse  olhar     ·     Shift  correr     ·     E  interagir     ·     Esc  pausar", centered);
            }
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
