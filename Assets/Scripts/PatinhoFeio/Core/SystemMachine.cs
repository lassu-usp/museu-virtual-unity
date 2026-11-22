using System;
using System.Collections.Generic;
using System.Linq;

namespace PatinhoFeio
{
    public sealed class SystemInputs
    {
        public LogicWord Prepare { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Select { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Start { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Wait { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Interrupt { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Increment { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Protect { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Rc { get; set; } = LogicWord.Filled(12, LogicBit.U);
        public PanelMode Mode { get; set; } = PanelMode.Ce;
        public LogicWord Present { get; set; } = LogicWord.Binary(15, 0);
        public LogicWord Ok { get; set; } = LogicWord.Filled(15, LogicBit.U);
        public LogicWord Loaded { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Online { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Stalled { get; set; } = LogicWord.Binary(1, 0);
        public DeviceEvent[] Events { get; set; } = Array.Empty<DeviceEvent>();
        public SystemInputs Copy() { var copy = (SystemInputs)MemberwiseClone(); copy.Events = (DeviceEvent[])Events.Clone(); return copy; }
        internal void Validate()
        {
            if (new[] { Prepare, Select, Start, Wait, Interrupt, Increment, Protect, Loaded, Online, Stalled }.Any(w => w.Width != 1) ||
                Rc.Width != 12 || Present.Width != 15 || Ok.Width != 15) throw new ArgumentException("System input widths");
            int previous = -1;
            foreach (var e in Events)
            {
                int key = e.Channel * 2 + e.Slot;
                if (key <= previous) throw new ArgumentException("Device events must have unique ordered channel/slot");
                previous = key;
            }
        }
    }

    /// <summary>
    /// Composes the independent logical CPU, panel, 15 interfaces and optional CE-12 reader.
    /// Each edge first computes projections from committed state, samples the CPU response,
    /// decides acceptance using all projected sources, then commits peripherals. CE-03 never
    /// depends on sequential callback order. See docs/io.md, interrupts.md and vectors.md.
    /// </summary>
    public sealed class SystemMachine
    {
        public CoreMachine Core { get; }
        private readonly IoInterface[] interfaces;
        private readonly FrontPanel panel = new FrontPanel();
        private SystemInputs inputs = new SystemInputs();
        private TapeReader reader;
        private Projection edge;
        private bool disconnected;
        private LogicWord panelPending = LogicWord.Binary(1, 0);
        private readonly List<LogicWord> inputLog = new List<LogicWord>(), ackLog = new List<LogicWord>();
        private int e8Commands, frames;
        public SystemInputs Inputs => inputs.Copy();
        public PanelMode Mode => panel.Mode;
        public bool Manual => panel.Manual;
        public bool Stopped => !Core.Active;
        public LogicWord PanelPending => panelPending;
        public bool ReaderEnabled => reader != null;
        public bool ReaderActive => reader != null && reader.Active;
        public bool ReaderSuppress => reader != null && reader.Suppress;
        public int ReaderPosition => reader?.Position ?? 0;
        public int Accepts => ackLog.Count;
        public int Reads => inputLog.Count;
        public int E8Commands => e8Commands;
        public int Frames => frames;
        public IReadOnlyList<LogicWord> InputLog => inputLog.AsReadOnly();
        public IReadOnlyList<LogicWord> AckLog => ackLog.AsReadOnly();
        public LogicWord InterruptSources => Project(false).Sources;
        public InterfaceState Interface(int channel) => interfaces[Channel(channel)].State;
        public InterfaceState DirectInterface(int channel) => Project(false).Direct[Channel(channel)].Snapshot();
        private static int Channel(int channel) => channel >= 1 && channel <= 15 ? channel : throw new ArgumentOutOfRangeException(nameof(channel));
        private static bool High(LogicWord value) => value == LogicWord.Binary(1, 1);

        public SystemMachine(MachineState initial, IEnumerable<KeyValuePair<int, LogicWord>> image,
            IEnumerable<InterfaceState> initialInterfaces, TapeReader reader = null)
        {
            var states = initialInterfaces.ToArray();
            if (states.Length != 15) throw new ArgumentException("Declare all 15 interfaces");
            interfaces = new IoInterface[16];
            for (int i = 1; i <= 15; i++) interfaces[i] = new IoInterface(states[i - 1]);
            this.reader = reader?.Snapshot();
            Core = new CoreMachine(initial, image) { BeforeClock = BeginEdge, AfterClock = EndEdge };
        }

        public void ApplyInputs(long atNs, SystemInputs values)
        {
            values.Validate();
            if (atNs % 50 == 25) throw new ArgumentException("Stimulus on rising edge is outside v1");
            Core.AdvanceTo(atNs); inputs = values.Copy();
            if (High(inputs.Prepare))
            {
                Prepare();
                var drive = Core.Inputs; drive.Prepare = inputs.Prepare; Core.Drive(drive);
            }
            else { var drive = Core.Inputs; drive.Prepare = inputs.Prepare; Core.Drive(drive); }
        }
        public void AdvanceTo(long atNs) => Core.AdvanceTo(atNs);
        // A visitor can fetch an unwritten cell. PREPARAÇÃO lets the operator recover
        // from the emulator diagnostic while keeping the ferrite and committed registers.
        public void PrepareAtPanel(SystemInputs controls)
        {
            if (!High(controls.Prepare)) throw new ArgumentException("Expected PREPARAÇÃO");
            Core.ReleaseDiagnostic();
            long time = Core.NowNs;
            ApplyInputs(time % 50 == 25 ? time + 25 : time, controls);
        }
        private void Prepare()
        {
            panel.Prepare(); panelPending = LogicWord.Binary(1, 0); reader?.Prepare();
            for (int ch = 1; ch <= 15; ch++) interfaces[ch].Prepare();
            disconnected = !ReaderEnabled && inputs.Present == LogicWord.Binary(15, 0) && inputs.Events.Length == 0;
        }
        private LogicWord Presence(int ch) => ch == 0 ? LogicWord.Binary(1, 0) :
            ch == 14 && ReaderEnabled ? LogicWord.Binary(1, 1) : inputs.Present.Slice(ch - 1, 1);

        private sealed class Projection
        {
            public readonly InterfaceState[] Direct = new InterfaceState[16], After = new InterfaceState[16];
            public LogicWord Sources, Panel;
            public TapeReader Reader;
            public IoRequest Request;
        }
        private Projection Project(bool diagnose)
        {
            var p = new Projection { Request = Core.Request, Reader = reader, Panel = panelPending.Or(inputs.Interrupt) };
            if (High(inputs.Prepare)) p.Panel = LogicWord.Binary(1, 0);
            DeviceEvent? arrival = null;
            if (ReaderEnabled && !High(inputs.Prepare))
            {
                if (diagnose && inputs.Present.Slice(13, 1) != LogicWord.Binary(1, 0))
                    throw new MachineDiagnostic(Core.NowNs, "PF_READER: channel E externally populated");
                p.Reader = reader.Project(p.Request.Channel == 14 ? p.Request.Operation : IoOperation.None,
                    inputs.Loaded, inputs.Online, inputs.Stalled, Core.NowNs, diagnose, out arrival);
            }
            var sourceBits = new LogicWord[16]; sourceBits[0] = p.Panel;
            for (int ch = 1; ch <= 15; ch++)
            {
                p.Direct[ch] = interfaces[ch].Direct(p.Request.Channel == ch && High(Presence(ch)) ? p.Request : default);
                var events = inputs.Events.Where(e => e.Channel == ch).ToList();
                if (diagnose && !High(inputs.Prepare) && events.Count != 0)
                {
                    if (ch == 14 && ReaderEnabled) throw new MachineDiagnostic(Core.NowNs, "PF_READER: external event on reader channel");
                    if (!High(Presence(ch))) throw new MachineDiagnostic(Core.NowNs, "PF_INTERFACE: event on unavailable channel");
                }
                if (ch == 14 && arrival.HasValue) events.Add(arrival.Value);
                p.After[ch] = High(inputs.Prepare) ? interfaces[ch].State : IoInterface.AfterEvents(p.Direct[ch], events);
                sourceBits[ch] = High(inputs.Prepare) ? LogicWord.Binary(1, 0) :
                    Presence(ch).And(p.After[ch].Permitted).And(p.After[ch].Pending);
            }
            p.Sources = sourceBits[15];
            for (int ch = 14; ch >= 0; ch--) p.Sources = LogicWord.Concat(p.Sources, sourceBits[ch]);
            return p;
        }
        private void BeginEdge()
        {
            if (High(inputs.Prepare)) Prepare();
            var drive = panel.BeginEdge(Core, inputs);
            Core.Drive(drive); // Mode gates must be established before projecting an IO strobe.
            // PREPARAÇÃO cleared all interface latches. While no equipment is attached
            // none can change, and the only possible interrupt source is the panel.
            if (disconnected && (inputs.Present != LogicWord.Binary(15, 0) || inputs.Events.Length != 0))
                disconnected = false;
            if (disconnected)
            {
                if (!High(inputs.Prepare)) panelPending = panelPending.Or(inputs.Interrupt);
                drive.InterruptPending = panelPending;
                drive.IoPresent = LogicWord.Binary(1, 0);
                return;
            }
            edge = Project(true);
            var request = edge.Request;
            drive.InterruptPending = LogicWord.Binary(1, 0);
            for (int ch = 0; ch <= 15; ch++) drive.InterruptPending = drive.InterruptPending.Or(edge.Sources.Slice(ch, 1));
            drive.IoPresent = Presence(request.Channel);
            if (request.Channel != 0)
            {
                var prior = interfaces[request.Channel].State;
                drive.IoData = prior.Data; drive.IoReady = prior.Ready; drive.IoPending = prior.Pending;
                drive.IoOk = request.Channel == 14 && ReaderEnabled ? inputs.Loaded.And(inputs.Online) : inputs.Ok.Slice(request.Channel - 1, 1);
            }
            Core.Drive(drive);
        }
        private void EndEdge()
        {
            if (High(inputs.Prepare)) return;
            if (disconnected)
            {
                if (Core.AcceptedInterrupt)
                {
                    ackLog.Add(LogicWord.Concat(LogicWord.Binary(15, 0), panelPending));
                    panelPending = LogicWord.Binary(1, 0);
                }
                panel.EndEdge(Core);
                return;
            }
            for (int ch = 1; ch <= 15; ch++) interfaces[ch].Commit(edge.After[ch]);
            if (reader != null) { frames += edge.Reader.Position - reader.Position; reader = edge.Reader; }
            if (edge.Request.Operation == IoOperation.Input) inputLog.Add(Core.State.Acc);
            if (edge.Request.Operation == IoOperation.SkipNulls) e8Commands++;
            panelPending = edge.Panel;
            if (Core.AcceptedInterrupt)
            {
                ackLog.Add(edge.Sources);
                if (High(edge.Sources.Slice(0, 1))) panelPending = LogicWord.Binary(1, 0);
            }
            panel.EndEdge(Core);
        }
    }
}
