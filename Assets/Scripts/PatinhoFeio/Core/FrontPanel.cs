namespace PatinhoFeio
{
    public enum PanelMode { Unknown, Ce, Normal, Is, Cs, Cp, Mp }

    /// <summary>Logical operator controls; no Unity input, debounce or lamp wiring.</summary>
    internal sealed class FrontPanel
    {
        public PanelMode Mode { get; private set; } = PanelMode.Ce;
        public PanelMode ActionMode { get; private set; } = PanelMode.Ce;
        public bool Manual { get; private set; }
        private bool stopRequested;
        private readonly CoreInputs drive = new CoreInputs();
        internal static bool IsManual(PanelMode mode) => mode == PanelMode.Ce || mode == PanelMode.Cp || mode == PanelMode.Mp;
        internal void Prepare() { Mode = ActionMode = PanelMode.Ce; Manual = stopRequested = false; }
        internal CoreInputs BeginEdge(CoreMachine core, SystemInputs input)
        {
            bool high(LogicWord word) => word == LogicWord.Binary(1, 1);
            if (high(input.Prepare)) Prepare();
            else
            {
                if (high(input.Select))
                {
                    if (input.Mode == PanelMode.Unknown) throw new MachineDiagnostic(core.NowNs, "PF_PANEL: unknown mode");
                    if (core.Active && !Manual && input.Mode != ActionMode && ActionMode != PanelMode.Cs) stopRequested = true;
                    Mode = input.Mode;
                }
                if (high(input.Wait)) stopRequested = true;
                if (!core.Active && high(input.Start))
                {
                    ActionMode = Mode; Manual = IsManual(Mode); stopRequested = false;
                }
                else if (!core.Active && core.Registers.Mode == ExecutionMode.Waiting && !IsManual(Mode))
                    ActionMode = Mode; // ESP after a manual inspection resumes in the selected processing mode.
            }
            drive.Start = input.Start;
            drive.Prepare = input.Prepare;
            drive.Protect = input.Protect;
            drive.Rc = input.Rc;
            drive.CycleStep = LogicWord.Binary(1, ActionMode == PanelMode.Cs ? 1 : 0);
            drive.InstructionStep = LogicWord.Binary(1, ActionMode == PanelMode.Is ? 1 : 0);
            drive.Manual = Manual;
            drive.ManualMode = ActionMode;
            drive.Increment = input.Increment;
            drive.StopBeforeFetch = stopRequested;
            drive.InterruptsAllowed = !IsManual(Mode);
            return drive;
        }
        internal void EndEdge(CoreMachine core) { if (!core.Active) Manual = false; }
    }
}
