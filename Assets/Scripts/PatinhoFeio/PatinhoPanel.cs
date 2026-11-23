using System;
using Lassu.Museum;
using PatinhoFeio;
using UnityEngine;

namespace MuseuVirtual
{
    [DisallowMultipleComponent, RequireComponent(typeof(MuseumExhibit), typeof(AudioSource))]
    public sealed class PatinhoPanel : MonoBehaviour
    {
        // Order follows the physical controls. P(0) is the most significant switch.
        static readonly string[] Buttons = { "Normal", "Ciclo_unico", "Instrucao_unica", "Enderecamento",
            "Armazenamento", "Exposicao", "Espera", "Interrupcao", "Partida", "Preparacao", "Liga", "Desliga" };
        static readonly string[] Labels = { "Normal", "Ciclo único", "Instrução única", "Endereçamento",
            "Armazenamento", "Exposição", "Espera", "Interrupção", "Partida", "Preparação", "Liga", "Desliga",
            "Endereçamento · fixo / sequencial", "Memória · liberada / protegida" };
        static readonly PanelMode[] Modes = { PanelMode.Normal, PanelMode.Cs, PanelMode.Is,
            PanelMode.Ce, PanelMode.Cp, PanelMode.Mp };
        const int StartButton = 8, PowerOnButton = 10, PowerOffButton = 11;
        const int Increment = 12, Protection = 13, FirstSwitch = 14;
        // Keep the render cost bounded on a slow frame (including the WebGL main thread).
        const int ClocksPerSecond = 24000, MaxClocksPerFrame = 600;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly Color Ruby = new Color(4f, 0.12f, 0.025f);
        static readonly Color Opal = new Color(2.6f, 1.7f, 0.65f);
        readonly Transform[] controls = new Transform[FirstSwitch + 12];
        readonly string[] labels = new string[FirstSwitch + 12];
        readonly Quaternion[] restRotation = new Quaternion[FirstSwitch + 12];
        readonly Vector3[] restPosition = new Vector3[FirstSwitch + 12];
        readonly Renderer[] lenses = new Renderer[PanelSession.External + 1];
        readonly Color[] colors = new Color[PanelSession.External + 1];
        readonly float[] intensities = new float[PanelSession.External + 1];
        public AudioClip switchSound, knobSound, startButtonSound, commonButtonSound, powerButtonSound;
        AudioSource speaker;
        MaterialPropertyBlock properties;
        PanelSession machine;
        Camera cameraAtPanel;
        Canvas galleryHud;
        bool hudWasEnabled;
        Vector3 originalPosition;
        Quaternion originalRotation;
        Rect originalRect;
        Bounds face;
        float clockRemainder;
        int pressed = -1;
        float releaseTime;
        bool dirty;
        public PanelSession Machine => machine;
        public bool Operating => cameraAtPanel != null;
        public const float GuideWidth = 414;
        public static float UiScale => Mathf.Min(Screen.width / 1280f, Screen.height / 760f);

        void Awake()
        {
            speaker = GetComponent<AudioSource>();
            machine = new PanelSession();
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < Buttons.Length; i++) BindControl(i, "PTF_Botao_" + Buttons[i], Labels[i]);
            BindControl(Increment, "PTF_Knob_Enderecamento", Labels[Increment]);
            BindControl(Protection, "PTF_Knob_Memoria", Labels[Protection]);
            for (int i = 0; i < 12; i++)
                BindControl(FirstSwitch + i, $"PTF_Switch_dados_{i:00}", $"P({i})  ·  /{1 << (11 - i):X3}");

            BindRow("Acumulador", PanelSession.Acc, 8);
            BindRow("Codigo_instrucao", PanelSession.Ri, 8);
            BindRow("Dados_memoria", PanelSession.Rd, 8);
            BindRow("Endereco_instrucao", PanelSession.Ci, 12);
            BindRow("Endereco_memoria", PanelSession.Re, 12);
            BindRow("Dados_painel", PanelSession.Rc, 12);
            BindLamp("Vai_um", PanelSession.Carry);
            BindLamp("Transbordo", PanelSession.Overflow);
            BindLamp("Parado", PanelSession.Halt);
            BindLamp("Externo", PanelSession.External);
            for (int i = 0; i < 7; i++) BindLamp("Fase_" + (i + 1), PanelSession.Phase1 + i);
            Paint();
        }

        void BindControl(int index, string path, string label)
        {
            var part = transform.Find(path);
            if (part == null) throw new InvalidOperationException(path);
            controls[index] = part;
            labels[index] = label;
            restRotation[index] = part.localRotation;
            restPosition[index] = part.localPosition;
            if (index == 0) face = new Bounds(part.localPosition, Vector3.zero);
            else face.Encapsulate(part.localPosition);
        }

        void BindRow(string row, int offset, int width)
        {
            for (int i = 0; i < width; i++) BindLamp($"{row}_{i:00}", offset + width - 1 - i);
        }

        void BindLamp(string suffix, int bit)
        {
            var part = transform.Find("PTF_LED_" + suffix);
            var lens = part.GetComponentInChildren<Renderer>();
            lenses[bit] = lens;
            colors[bit] = lens.sharedMaterial.name.Contains("rubi") ? Ruby : Opal;
            intensities[bit] = -1;
            face.Encapsulate(part.localPosition);
        }

        void Paint()
        {
            for (int i = 0; i < lenses.Length; i++)
            {
                float light = machine.Brightness(i);
                if (light == intensities[i]) continue;
                intensities[i] = light;
                properties.SetColor(Emission, colors[i] * light);
                lenses[i].SetPropertyBlock(properties);
            }
        }

        void Update()
        {
            if (pressed >= 0 && Time.unscaledTime >= releaseTime) Release();
            if (!dirty && !machine.Running) return;
            float clocks = Mathf.Min(MaxClocksPerFrame, clockRemainder + Time.unscaledDeltaTime * ClocksPerSecond);
            int count = (int)clocks;
            clockRemainder = clocks - count;
            dirty = machine.Running; // Paint the settled state once on the frame after PARE.
            machine.RunFrame(count);
            Paint();
        }

        void Release()
        {
            if (pressed >= 0) controls[pressed].localPosition = restPosition[pressed];
            pressed = -1;
        }

        public void Operate(int index)
        {
            if (!Operating || index < 0 || index >= controls.Length) return;
            dirty = true;
            AudioClip sound;
            if (index >= FirstSwitch)
            {
                sound = switchSound;
                int bit = index - FirstSwitch;
                machine.ToggleSwitch(bit);
                bool down = (machine.Switches & (1 << (11 - bit))) != 0;
                controls[index].localRotation = restRotation[index] * Quaternion.AngleAxis(down ? 52 : 0, Vector3.right);
            }
            else if (index == Increment || index == Protection)
            {
                sound = knobSound;
                if (index == Increment) machine.ToggleIncrement(); else machine.ToggleProtection();
                bool initial = index == Increment ? machine.Sequential : machine.Protected;
                controls[index].localRotation = restRotation[index] * Quaternion.AngleAxis(initial ? 0 : 52, Vector3.up);
            }
            else
            {
                sound = index switch
                {
                    StartButton => startButtonSound,
                    PowerOnButton or PowerOffButton => powerButtonSound,
                    _ => commonButtonSound
                };
                Release();
                pressed = index;
                releaseTime = Time.unscaledTime + 0.14f;
                float travel = index >= PowerOnButton ? 0.003f : 0.0018f;
                controls[index].localPosition = restPosition[index] + Vector3.up * travel;
                if (index < Modes.Length) machine.Select(Modes[index]);
                else switch (index)
                {
                    case 6: machine.Wait(); break;
                    case 7: machine.Interrupt(); break;
                    case StartButton: machine.Start(); break;
                    case 9: machine.Prepare(); break;
                    case PowerOnButton: machine.PowerOn(); break;
                    case PowerOffButton: machine.PowerOff(); break;
                }
            }
            speaker.PlayOneShot(sound);
            Paint();
        }

        public void Open(Camera view)
        {
            if (Operating) return;
            cameraAtPanel = view;
            originalPosition = view.transform.localPosition;
            originalRotation = view.transform.localRotation;
            originalRect = view.rect;
            galleryHud = MuseumHUD.Instance != null ? MuseumHUD.Instance.GetComponent<Canvas>() : null;
            if (galleryHud != null) { hudWasEnabled = galleryHud.enabled; galleryHud.enabled = false; }
            FramePanel();
        }

        void LateUpdate() { if (Operating) FramePanel(); }

        void FramePanel()
        {
            var view = cameraAtPanel;
            view.rect = new Rect(0, 0, 1f - GuideWidth * UiScale / Screen.width, 1);
            // The imported computer root uses -X across the panel, +Z up and +Y into it.
            Vector3 up = transform.forward, into = transform.up;
            float width = transform.TransformVector(Vector3.right * face.size.x).magnitude;
            float height = transform.TransformVector(Vector3.forward * face.size.z).magnitude;
            float distance = 0.58f * Mathf.Max(height, width / view.aspect) / Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
            view.transform.SetPositionAndRotation(transform.TransformPoint(face.center) - into * distance,
                Quaternion.LookRotation(into, up));
        }

        public void Close()
        {
            if (!Operating) return;
            Release();
            cameraAtPanel.transform.SetLocalPositionAndRotation(originalPosition, originalRotation);
            cameraAtPanel.rect = originalRect;
            cameraAtPanel = null;
            if (galleryHud != null) galleryHud.enabled = hudWasEnabled;
        }

        void OnDisable() => Close();

        // Pick in the framed panel plane. The cabinet keeps its original collision box.
        public int Pick(Vector2 screenPoint)
        {
            if (!Operating || !cameraAtPanel.pixelRect.Contains(screenPoint)) return -1;
            Vector2 ScreenPoint(int i) => cameraAtPanel.WorldToScreenPoint(controls[i].position);
            float radius = Vector2.Distance(ScreenPoint(FirstSwitch), ScreenPoint(FirstSwitch + 1)) * 0.57f;
            float shortest = radius * radius;
            int selected = -1;
            for (int i = 0; i < controls.Length; i++)
            {
                float distance = (screenPoint - ScreenPoint(i)).sqrMagnitude;
                if (distance > shortest) continue;
                selected = i;
                shortest = distance;
            }
            return selected;
        }

        public string Label(int index) => labels[index];
    }
}
