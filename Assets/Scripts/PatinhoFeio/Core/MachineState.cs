using System;

namespace PatinhoFeio
{
    public enum Phase { F1, F2, F3, F4, F5, I6, I7 }
    public enum ExecutionMode { Unknown, Running, Waiting, Stopped }

    /// <summary>Scenario state, not a reset image (registers.md CE-09).</summary>
    public sealed class MachineState
    {
        public LogicWord Acc { get; internal set; }
        public LogicWord Ci { get; internal set; }
        public LogicWord Re { get; internal set; }
        public LogicWord Rd { get; internal set; }
        public LogicWord Ri { get; internal set; }
        public LogicWord V { get; internal set; }
        public LogicWord T { get; internal set; }
        public LogicWord Bei { get; internal set; }
        public LogicWord Enabled { get; internal set; }
        public LogicWord Servicing { get; internal set; }
        public ExecutionMode Mode { get; internal set; }

        public MachineState(LogicWord acc, LogicWord ci, LogicWord re, LogicWord rd, LogicWord ri,
            LogicWord v, LogicWord t, LogicWord bei, LogicWord enabled, LogicWord servicing, ExecutionMode mode)
        {
            Acc = Sized(acc, 8); Ci = Sized(ci, 12); Re = Sized(re, 12); Rd = Sized(rd, 8); Ri = Sized(ri, 8);
            V = Sized(v, 1); T = Sized(t, 1); Bei = Sized(bei, 1); Enabled = Sized(enabled, 1);
            Servicing = Sized(servicing, 1); Mode = mode;
        }
        private static LogicWord Sized(LogicWord value, int width) => value.Width == width ? value :
            throw new ArgumentException("Invalid state word width");
        public static MachineState Unknown() => new MachineState(LogicWord.Filled(8, LogicBit.U),
            LogicWord.Filled(12, LogicBit.U), LogicWord.Filled(12, LogicBit.U), LogicWord.Filled(8, LogicBit.U),
            LogicWord.Filled(8, LogicBit.U), LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U),
            LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U), ExecutionMode.Unknown);
        public MachineState Snapshot() => new MachineState(Acc, Ci, Re, Rd, Ri, V, T, Bei, Enabled, Servicing, Mode);
    }

    public sealed class CoreInputs
    {
        // Connected only by the scene-free system composition. Core vectors use disconnected defaults.
        internal PanelMode ManualMode { get; set; } = PanelMode.Normal;
        internal bool Manual { get; set; }
        internal bool StopBeforeFetch { get; set; }
        internal bool InterruptsAllowed { get; set; } = true;
        internal LogicWord InterruptPending { get; set; } = LogicWord.Binary(1, 0);
        internal LogicWord IoPresent { get; set; } = LogicWord.Binary(1, 0);
        internal LogicWord IoData { get; set; } = LogicWord.Filled(8, LogicBit.U);
        internal LogicWord IoReady { get; set; } = LogicWord.Bit(LogicBit.U);
        internal LogicWord IoPending { get; set; } = LogicWord.Bit(LogicBit.U);
        internal LogicWord IoOk { get; set; } = LogicWord.Bit(LogicBit.U);
        internal LogicWord Increment { get; set; } = LogicWord.Binary(1, 0);
        public LogicWord Start { get; set; } = LogicWord.Bit(LogicBit.U);
        public LogicWord CycleStep { get; set; } = LogicWord.Bit(LogicBit.U);
        public LogicWord InstructionStep { get; set; } = LogicWord.Bit(LogicBit.U);
        public LogicWord Prepare { get; set; } = LogicWord.Bit(LogicBit.U);
        public LogicWord Protect { get; set; } = LogicWord.Bit(LogicBit.U);
        public LogicWord Rc { get; set; } = LogicWord.Filled(12, LogicBit.U);
        internal CoreInputs Copy() => (CoreInputs)MemberwiseClone();
        internal void Validate()
        {
            if (Start.Width != 1 || CycleStep.Width != 1 || InstructionStep.Width != 1 ||
                Prepare.Width != 1 || Protect.Width != 1 || Rc.Width != 12) throw new ArgumentException("Input width");
        }
    }

    /// <summary>Tool-domain refusal, never an architectural trap.</summary>
    public sealed class MachineDiagnostic : Exception
    {
        public long AtNs { get; }
        public string Severity => "failure";
        public MachineDiagnostic(long atNs, string message) : base(message) { AtNs = atNs; }
    }
}
