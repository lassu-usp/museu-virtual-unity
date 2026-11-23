using PatinhoFeio;
using UnityEngine;

namespace MuseuVirtual
{
    // Companion card for the 3D panel, drawn in the visitor's existing IMGUI HUD.
    internal sealed class PatinhoPanelGuide
    {
        const string Resident =
            "1. Aperte Preparação. Nas chaves, forme /010 (só P7 para baixo). Aperte Partida e confira CI = /010.\n\n" +
            "2. Forme /005 (P9 e P11). Aperte Normal e Partida. O programa lê as chaves, soma um e para com ACC = /06.\n\n" +
            "3. Aperte Partida outra vez. O contador roda continuamente. Use Espera para parar e Instrução única + Partida para avançar.";
        const string Manual =
            "1. Aperte Preparação. Forme /020 (só P6 para baixo) e aperte Partida. Deixe o seletor em Sequencial.\n\n" +
            "2. Aperte Armazenamento. Grave /88 (P4 e P8) com Partida. Depois grave /9D (P4, P7, P8, P9 e P11) com outra Partida.\n\n" +
            "3. Endereçamento, /020, Partida. Para conferir os bytes, use Exposição e Partida duas vezes e veja RD. Volte a endereçar /020.\n\n" +
            "4. Forme /005. Aperte Normal e Partida. Seu programa lê as chaves e para com ACC = /05.";

        static readonly Color Paper = new Color(0.10f, 0.13f, 0.13f);
        static readonly Color Ink = new Color(1f, 0.97f, 0.88f);
        static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);
        GUIStyle title, body, small, register, button, prompt;
        bool manual;
        Vector2 scroll;
        const string History = "Exemplos criados para o museu. A ferrite podia reter um programa ao desligar, com risco de corrupção. O carregador histórico de /F80 está perdido.";
        static readonly GUIContent ResidentText = new GUIContent(Resident), ManualText = new GUIContent(Manual),
            HistoryText = new GUIContent(History);
        int previousRc = -1;
        LogicWord previousCi, previousRd, previousAcc;
        string readings;

        static GUIStyle Type(int size, Color color, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            style.normal.textColor = color;
            return style;
        }

        static string Hex(LogicWord word) => word.IsBinary ?
            "/" + word.ToInt().ToString(word.Width == 12 ? "X3" : "X2") : "?";

        void ReadRegisters(PanelSession machine)
        {
            var r = machine.Registers;
            if (previousRc == machine.Switches && previousCi == r.Ci && previousRd == r.Rd && previousAcc == r.Acc) return;
            previousRc = machine.Switches;
            previousCi = r.Ci; previousRd = r.Rd; previousAcc = r.Acc;
            readings = $"RC   /{previousRc:X3}       CI   {Hex(r.Ci)}\nRD   {Hex(r.Rd)}         ACC   {Hex(r.Acc)}";
        }

        static string Mode(PanelMode mode)
        {
            switch (mode)
            {
                case PanelMode.Ce: return "Endereçamento";
                case PanelMode.Cp: return "Armazenamento";
                case PanelMode.Mp: return "Exposição";
                case PanelMode.Cs: return "Ciclo único";
                case PanelMode.Is: return "Instrução única";
                default: return "Normal";
            }
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public void Draw(PatinhoPanel panel, MuseumVisitor visitor)
        {
            if (title == null)
            {
                title = Type(30, Ink, true); body = Type(17, Ink); small = Type(15, Ink);
                register = Type(20, Brass, true);
                button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
                prompt = Type(16, Ink);
                prompt.alignment = TextAnchor.MiddleLeft;
            }
            GUI.matrix = Matrix4x4.identity;
            var ev = Event.current;
            int hover = panel.Pick(new Vector2(ev.mousePosition.x, Screen.height - ev.mousePosition.y));
            if (hover >= 0 && ev.type == EventType.MouseDown && ev.button == 0)
            {
                panel.Operate(hover);
                ev.Use();
            }

            float scale = PatinhoPanel.UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            float edge = width - PatinhoPanel.GuideWidth, x = edge + 22, w = PatinhoPanel.GuideWidth - 44;
            Fill(new Rect(edge, 0, PatinhoPanel.GuideWidth, height), Paper);
            Fill(new Rect(edge, 0, 3, height), Brass);
            GUI.Label(new Rect(x, 16, w, 44), "Patinho Feio", title);
            var machine = panel.Machine;
            GUI.Label(new Rect(x, 64, w, 28), !machine.Powered ? "Desligado" :
                Mode(machine.Mode) + (machine.Running ? "   ·   Em execução" : "   ·   Parado"), body);
            ReadRegisters(machine);
            GUI.Label(new Rect(x, 97, w, 60), machine.Powered ? readings : "RC, CI, RD e ACC apagados", register);
            GUI.Label(new Rect(x, 162, w, 26), (machine.Sequential ? "Sequencial" : "Fixo") + "   ·   Memória " +
                (machine.Protected ? "protegida" : "liberada"), small);
            Fill(new Rect(x, 196, w, 1), Brass);
            GUI.backgroundColor = manual ? Color.gray : Brass;
            if (GUI.Button(new Rect(x, 210, w / 2 - 3, 34), "Programa residente", button)) { manual = false; scroll = Vector2.zero; }
            GUI.backgroundColor = manual ? Brass : Color.gray;
            if (GUI.Button(new Rect(x + w / 2 + 3, 210, w / 2 - 3, 34), "Gravar à mão", button)) { manual = true; scroll = Vector2.zero; }
            GUI.backgroundColor = Color.white;
            var instructions = manual ? ManualText : ResidentText;
            float textWidth = w - 19;
            float guideHeight = body.CalcHeight(instructions, textWidth);
            float historyHeight = small.CalcHeight(HistoryText, textWidth);
            scroll = GUI.BeginScrollView(new Rect(x, 257, w, height - 348), scroll,
                new Rect(0, 0, textWidth, guideHeight + 22 + historyHeight));
            GUI.Label(new Rect(0, 0, textWidth, guideHeight), instructions, body);
            GUI.Label(new Rect(0, guideHeight + 22, textWidth, historyHeight), HistoryText, small);
            GUI.EndScrollView();
            bool fault = machine.Diagnostic != null;
            if (fault || !machine.Powered || hover >= 0)
            {
                float y = height - 65;
                Fill(new Rect(15, y, edge - 30, 45), Paper);
                string hint = fault ? "Execução interrompida. Aperte Preparação e confira o endereço e os bytes gravados."
                    : hover >= 0 ? panel.Label(hover) : "Pressione Liga para retomar.";
                var hintRow = new Rect(28, y, edge - 52, 45);
                if (fault) GUI.Label(hintRow, hint, prompt);
                else MuseumPrompts.DrawAction(hintRow, MuseumPrompts.Click, hint, prompt);
            }
            else MuseumPrompts.DrawAction(new Rect(x, height - 86, w, 28), MuseumPrompts.Click,
                "Acionar chaves, seletores e botões", prompt, height: 24);
            var back = new Rect(x, height - 53, w, 37);
            if (GUI.Button(back, GUIContent.none, button)) visitor.EndInspection();
            MuseumPrompts.DrawAction(back, MuseumPrompts.E, "Voltar à sala", prompt, true, MuseumPrompts.Esc, 26);
        }
    }
}
