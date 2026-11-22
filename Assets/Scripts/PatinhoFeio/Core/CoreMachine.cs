using System;
using System.Collections.Generic;

namespace PatinhoFeio
{
    /// <summary>
    /// Scene-free core surface. Call ApplyInputs / AdvanceTo with logical nanoseconds.
    /// ISA and phase actions: docs/instruction-set.md and microarchitecture.md.
    /// Sampling calendar: docs/vectors.md and vhdl.md (CE-02), not wall-clock time.
    /// SystemMachine connects panel/peripherals through explicit edge inputs; no scene dependency.
    /// </summary>
    public sealed class CoreMachine
    {
        private readonly MachineState state;
        private CoreInputs inputs = new CoreInputs();
        private long nextEdge = 25;
        private int tick;
        private Phase nextPhase;
        private bool completes, stopAtEnd, skip;
        private LogicWord pointerHigh = LogicWord.Filled(8, LogicBit.U);
        private Instruction instruction;
        private MachineDiagnostic fault;
        private bool forcedEntry, completionPending, entryEnding;
        internal Action BeforeClock { get; set; }
        internal Action AfterClock { get; set; }
        internal bool AcceptedInterrupt { get; private set; }
        // The system owns this drive for the whole edge. External inputs still use a copy.
        internal void Drive(CoreInputs values) { inputs = values; if (High(inputs.Prepare)) Prepare(); }

        public CoreMachine(MachineState initial, IEnumerable<KeyValuePair<int, LogicWord>> image = null)
        {
            state = (initial ?? throw new ArgumentNullException(nameof(initial))).Snapshot();
            Memory = new FunctionalMemory(image);
        }
        public long NowNs { get; private set; }
        public MachineState State => state.Snapshot();
        // A live view for the lamps. Its setters are internal to the core assembly.
        public MachineState Registers => state;
        internal void ReleaseDiagnostic()
        {
            if (fault == null) return;
            fault = null;
            nextEdge = NowNs + 50;
        }
        public CoreInputs Inputs => inputs.Copy();
        public FunctionalMemory Memory { get; }
        public bool Active { get; private set; }
        public Phase Phase { get; private set; } = Phase.F1;
        public int Segment => tick / 5;
        // Enable for the upcoming edge, not a latched completion result.
        public bool InstructionDone => Active && !inputs.Manual && !forcedEntry && tick == 35 && completes;
        public IoRequest Request => Active && !inputs.Manual && !High(inputs.Prepare) && Phase == Phase.F2 &&
            tick == 20 && instruction.Op == Operation.Io
            ? new IoRequest(instruction.Code & 15, IoRequest.Decode(instruction.Code & 15, state.Rd), state.Acc) : default;
        public bool MemoryWriteEnable => Memory.WriteEnable(nextEdge);
        public LogicWord ReturnGuard { get; private set; } = LogicWord.Bit(LogicBit.U);
        private static LogicWord Byte(int value) => LogicWord.Binary(8, value);
        private static LogicWord Address(int value) => LogicWord.Binary(12, value);
        private static LogicWord Bit(bool value) => LogicWord.Binary(1, value ? 1 : 0);
        private static bool High(LogicWord value) => value == Bit(true);

        public void ApplyInputs(long atNs, CoreInputs values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            values.Validate();
            if (atNs % 50 == 25) throw new ArgumentException("Stimulus on a rising edge is outside vector format v1");
            AdvanceTo(atNs);
            inputs = values.Copy();
            if (High(inputs.Prepare)) Prepare();
        }

        public void AdvanceTo(long atNs)
        {
            if (fault != null) throw fault;
            if (atNs < NowNs) throw new ArgumentOutOfRangeException(nameof(atNs), "Logical time cannot go backwards");
            try
            {
                while (nextEdge <= atNs)
                {
                    NowNs = nextEdge;
                    BeforeClock?.Invoke();
                    Clock();
                    AfterClock?.Invoke();
                    nextEdge = checked(nextEdge + 50);
                }
                NowNs = atNs;
            }
            catch (MachineDiagnostic diagnostic) { fault = diagnostic; throw; }
        }

        private void Prepare()
        {
            Memory.Abort(); Active = false; Phase = Phase.F1; tick = 0;
            completes = false; stopAtEnd = false; skip = false;
            forcedEntry = completionPending = entryEnding = false; AcceptedInterrupt = false;
            state.Bei = Bit(false); state.Enabled = Bit(true); state.Servicing = Bit(false);
            state.Mode = ExecutionMode.Stopped; ReturnGuard = Bit(false);
        }

        private void Clock()
        {
            AcceptedInterrupt = false;
            if (High(inputs.Prepare)) { Prepare(); return; }
            Memory.Tick(NowNs);
            if (!Active)
            {
                if (state.Mode == ExecutionMode.Waiting && TryAcceptInterrupt()) return;
                if (High(inputs.Start)) { Active = true; if (!inputs.Manual) state.Mode = ExecutionMode.Running; }
                return; // T0 action is on the next edge after accepting PARTIDA.
            }

            if (inputs.Manual) { ClockManual(); return; }

            int elapsed = tick * 50;
            if (elapsed == 0) StartCycle();
            if (elapsed == 500) Capture();
            if (elapsed == 750) PlanCycle();
            if (elapsed == 1000 && instruction.Op == Operation.Io && Phase == Phase.F2) SampleIo();
            if (elapsed == 850 && Phase == Phase.F3 && instruction.Op == Operation.Sus)
                Memory.BeginWrite(NowNs, state.Rd, inputs.Protect);
            if (Phase == Phase.F2 && instruction.Op == Operation.Shift &&
                (elapsed == 850 || elapsed == 1100 || elapsed == 1350 || elapsed == 1600))
            {
                int bit = 3 - (elapsed - 850) / 250;
                if ((state.Rd.ToInt() & (1 << bit)) != 0) Shift(state.Rd.ToInt() >> 4);
            }
            if (elapsed == 1250) ExecuteData();
            if (elapsed == 1750) FinishPhase();
            if (tick == 39)
            {
                tick = 0;
                bool accepted = completionPending && TryAcceptInterrupt();
                completionPending = false;
                if (!accepted)
                {
                    if (stopAtEnd || Phase == Phase.F1 && inputs.StopBeforeFetch) Active = false;
                    if (entryEnding) { forcedEntry = false; entryEnding = false; }
                }
            }
            else tick++;
        }

        private void ClockManual()
        {
            // front-panel.md, one CE/CP/MP cycle; paused CPU phase/BEI/temporaries are retained.
            var mode = inputs.ManualMode;
            if (tick == 0 && mode != PanelMode.Ce) Memory.BeginRead(NowNs, state.Re, mode == PanelMode.Cp);
            if (tick == 10 && mode != PanelMode.Ce) state.Rd = mode == PanelMode.Cp ? inputs.Rc.Slice(0, 8) : Memory.ReadData;
            if (tick == 15)
            {
                if (mode == PanelMode.Ce) state.Ci = state.Re = inputs.Rc;
                if (mode == PanelMode.Cp) Memory.BeginWrite(NowNs, state.Rd, inputs.Protect);
            }
            if (tick == 35 && mode != PanelMode.Ce)
            {
                if (!inputs.Increment.IsBinary) Fail("PF_PANEL: undefined P+1");
                if (High(inputs.Increment)) state.Ci = state.Re = state.Ci.AddModulo(1);
            }
            if (++tick == 40) { tick = 0; Active = false; }
        }

        private bool TryAcceptInterrupt()
        {
            // Interrupts CE-01/03/10: known blockers dominate unknown inputs. No tests mid-instruction.
            if (!inputs.InterruptsAllowed || inputs.Manual || forcedEntry || state.Mode == ExecutionMode.Stopped) return false;
            var eligible = inputs.InterruptPending.And(state.Enabled).And(state.Servicing.Not()).And(state.Bei.Not()).And(ReturnGuard.Not());
            if (!eligible.IsBinary) Fail(inputs.InterruptPending.IsBinary ? "PF_UNDEFINED: interrupt state" : "PF_UNDEFINED: interrupt request");
            if (!High(eligible)) return false;
            state.Servicing = Bit(true); state.Mode = ExecutionMode.Running;
            state.Ri = LogicWord.Concat(LogicWord.Binary(4, 15), state.Ri.Slice(0, 4));
            state.Re = Address(2); Phase = Phase.F3; tick = 0; Active = true;
            forcedEntry = true; entryEnding = completes = stopAtEnd = false;
            AcceptedInterrupt = true;
            return true;
        }

        private void SampleIo()
        {
            var request = Request;
            // SAL OK is unconditionally false outside the three documented channels.
            if (request.Operation == IoOperation.TestOk && request.Channel != 5 && request.Channel != 8 && request.Channel != 14)
            { skip = false; return; }
            if (!High(inputs.IoPresent)) Fail("PF_IO: unavailable channel");
            switch (request.Operation)
            {
                case IoOperation.Input: state.Acc = inputs.IoData; break;
                case IoOperation.TestReady: skip = Known(inputs.IoReady, "IO condition") == 1; break;
                case IoOperation.TestOk: skip = Known(inputs.IoOk, "IO condition") == 1; break;
                case IoOperation.TestNoPending: skip = Known(inputs.IoPending, "IO condition") == 0; break;
            }
        }

        private void StartCycle()
        {
            completes = false; stopAtEnd = false; skip = false;
            // RI can retain an unknown byte before F1; it is decoded only after the fetch.
            if (Phase != Phase.F1) instruction = Instruction.Decode(state.Ri);
            LogicWord location = state.Re;
            if (Phase == Phase.F5 || ((Phase == Phase.F3 || Phase == Phase.F4) && instruction.Op == Operation.Tri))
                location = Address(0);
            else if ((Phase == Phase.F3 || Phase == Phase.F4) && instruction.Op == Operation.Tre) location = Address(1);
            bool split = (Phase == Phase.F3 || Phase == Phase.F4) && instruction.Store;
            Memory.BeginRead(NowNs, location, split);
        }

        private void Capture()
        {
            if (Phase == Phase.F1)
            {
                state.Ri = Memory.ReadData; state.Ci = state.Ci.AddModulo(1);
                instruction = Instruction.Decode(state.Ri);
                return; // B1 does not load RD.
            }
            if (Phase == Phase.F2) state.Ci = state.Ci.AddModulo(1);
            if (Phase == Phase.F3 && (instruction.Op == Operation.Arm || instruction.Op == Operation.Armx))
                state.Rd = state.Acc;
            else if ((Phase == Phase.F3 || Phase == Phase.F4) && instruction.Op == Operation.Pug)
                state.Rd = Phase == Phase.F3 ? LogicWord.Concat(LogicWord.Binary(4, 0), state.Ci.Slice(8, 4)) : state.Ci.Slice(0, 8);
            else if (Phase == Phase.F4 && instruction.Swap) state.Rd = state.Acc;
            else state.Rd = Memory.ReadData;
            if (Phase == Phase.I6) pointerHigh = state.Rd;
        }

        private void PlanCycle()
        {
            if (instruction.Op == Operation.Unknown) Fail("PF_UNSUPPORTED: op_unknown");
            nextPhase = Phase.F1;
            switch (Phase)
            {
                case Phase.F1:
                    nextPhase = instruction.Swap ? Phase.F3 : instruction.Short ? Phase.F1 : Phase.F2;
                    if (instruction.Op == Operation.FlagTest)
                    {
                        var flag = (instruction.Code & 4) != 0 ? state.V : state.T;
                        skip = Known(flag, "flag test") == ((instruction.Code >> 1) & 1);
                    }
                    break;
                case Phase.F2:
                    if (instruction.Memory)
                        nextPhase = Known(state.Bei, "BEI") == 1 ? Phase.I6 : ResolvedPhase();
                    else if (instruction.Op == Operation.Shift)
                    {
                        int operand = Known(state.Rd, "SHIFT operand");
                        if (operand >> 4 > 8) Fail("PF_UNSUPPORTED: shift mode");
                    }
                    else if (instruction.Op == Operation.Io)
                    {
                        Known(state.Rd, "IO operand");
                        if (IoRequest.Decode(instruction.Code & 15, state.Rd) == IoOperation.Invalid)
                            Fail("PF_UNSUPPORTED: IO command");
                    }
                    break;
                case Phase.F3:
                    nextPhase = instruction.Op == Operation.Pug || instruction.Swap ? Phase.F4 : Phase.F1;
                    if (instruction.Op == Operation.Sus)
                    {
                        skip = Known(state.Rd, "SUS operand") == 0;
                        if (!skip) state.Rd = state.Rd.AddModulo(-1);
                    }
                    break;
                case Phase.F4: break;
                case Phase.F5: nextPhase = instruction.Op == Operation.Plax ? Phase.F1 : Phase.F3; break;
                case Phase.I6: nextPhase = Phase.I7; break;
                case Phase.I7:
                    nextPhase = Known(pointerHigh.Slice(4, 1), "indirect continuation") == 1 ? Phase.I6 : ResolvedPhase();
                    break;
            }
            completes = nextPhase == Phase.F1;
            if ((Phase == Phase.F3 || Phase == Phase.F4) && instruction.Store && instruction.Op != Operation.Sus)
                Memory.BeginWrite(NowNs, state.Rd, inputs.Protect);
        }

        private Phase ResolvedPhase() => instruction.Indexed ? Phase.F5 : instruction.Jump ? Phase.F1 : Phase.F3;

        private void ExecuteData()
        {
            var op = instruction.Op;
            if (Phase == Phase.F1)
            {
                if (op == Operation.Group1) Group1();
                else if (op == Operation.FlagTest && skip) state.Ci = state.Ci.AddModulo(1);
                else if (op == Operation.Pul) state.Ci = Address(1);
            }
            else if (Phase == Phase.F2)
            {
                switch (op)
                {
                    case Operation.Cari: state.Acc = state.Rd; break;
                    case Operation.Somi: Sum(state.Acc, state.Rd, 0); break;
                    case Operation.Xor: state.Acc = state.Acc.Xor(state.Rd); break;
                    case Operation.Nand: state.Acc = state.Acc.And(state.Rd).Not(); break;
                    case Operation.Io: if (skip) state.Ci = state.Ci.AddModulo(1); break;
                }
            }
            else if (Phase == Phase.F3)
            {
                if (op == Operation.Car || op == Operation.Carx) state.Acc = state.Rd;
                else if (op == Operation.Som || op == Operation.Somx) Sum(state.Acc, state.Rd, 0);
                else if (op == Operation.Sus && skip) state.Ci = state.Ci.AddModulo(1);
                else if (instruction.Swap) state.Re = LogicWord.Concat(LogicWord.Binary(4, 0), state.Rd);
            }
            else if (Phase == Phase.F4 && instruction.Swap) state.Acc = state.Re.Slice(0, 8);
        }

        private void FinishPhase()
        {
            var op = instruction.Op;
            switch (Phase)
            {
                case Phase.F1:
                    if (op == Operation.Pul || (op == Operation.FlagTest && skip)) state.Ci = state.Ci.AddModulo(1);
                    if (op == Operation.FlagTest && (instruction.Code & 1) != 0)
                    {
                        var flag = Bit((instruction.Code & 2) == 0);
                        if ((instruction.Code & 4) != 0) state.V = flag; else state.T = flag;
                    }
                    state.Re = state.Ci;
                    break;
                case Phase.F2:
                    if (instruction.Memory)
                    {
                        state.Re = LogicWord.Concat(state.Ri.Slice(0, 4), state.Rd);
                        if (nextPhase != Phase.I6) ResolveJump();
                    }
                    else
                    {
                        if (op == Operation.Io && skip) state.Ci = state.Ci.AddModulo(1);
                        state.Re = state.Ci;
                    }
                    break;
                case Phase.F3:
                    if (op == Operation.Pug) state.Re = state.Re.AddModulo(1);
                    else if (!instruction.Swap)
                    {
                        if (op == Operation.Sus && skip) state.Ci = state.Ci.AddModulo(1);
                        state.Re = state.Ci;
                    }
                    break;
                case Phase.F4:
                    if (op == Operation.Pug) state.Ci = state.Re.AddModulo(1);
                    state.Re = state.Ci;
                    break;
                case Phase.F5:
                    state.Re = state.Re.IsBinary && state.Rd.IsBinary
                        ? state.Re.AddModulo(state.Rd.ToInt()) : LogicWord.Filled(12, LogicBit.X);
                    if (op == Operation.Plax) state.Ci = state.Re;
                    break;
                case Phase.I6: state.Re = state.Re.AddModulo(1); break;
                case Phase.I7:
                    state.Re = LogicWord.Concat(pointerHigh.Slice(0, 4), state.Rd);
                    state.Bei = pointerHigh.Slice(4, 1);
                    if (nextPhase != Phase.I6) ResolveJump();
                    break;
            }
            if (completes && !forcedEntry)
            {
                completionPending = true;
                state.Bei = Bit(op == Operation.Ind);
                if (op == Operation.Inib) state.Enabled = Bit(false);
                if (op == Operation.Perm) state.Enabled = Bit(true);
                if (op == Operation.Pul) { state.Servicing = Bit(false); ReturnGuard = Bit(true); }
                else if (op != Operation.Ind) ReturnGuard = Bit(false);
                state.Mode = op == Operation.Pare ? ExecutionMode.Stopped :
                    op == Operation.Esp ? ExecutionMode.Waiting : ExecutionMode.Running;
            }
            if (completes && forcedEntry) entryEnding = true;
            stopAtEnd = High(inputs.CycleStep) || (completes && (High(inputs.InstructionStep) ||
                op == Operation.Pare || op == Operation.Esp));
            Phase = nextPhase;
        }

        private void ResolveJump()
        {
            bool take;
            switch (instruction.Op)
            {
                case Operation.Pla: case Operation.Plax: take = true; break;
                case Operation.Plan: take = Known(state.Acc.Slice(7, 1), "PLAN sign") == 1; break;
                case Operation.Plaz: take = Known(state.Acc, "PLAZ operand") == 0; break;
                default: return;
            }
            if (take) state.Ci = state.Re;
            else state.Re = state.Ci;
        }

        private void Group1()
        {
            int variant = instruction.Code & 7;
            bool panel = (instruction.Code & 8) != 0;
            var x = variant < 2 ? Byte(0) : variant < 4 ? state.Acc.Not() : variant < 6 ? state.Acc : Byte(255);
            var y = panel ? inputs.Rc.Slice(0, 8) : Byte(0);
            if (!panel && (variant == 0 || variant == 1 || variant == 2 || variant == 4 || variant == 6 || variant == 7))
            {
                state.Acc = variant == 1 ? Byte(1) : variant == 2 ? state.Acc.Not() : variant == 4 ? state.Acc :
                    variant == 6 ? Byte(255) : Byte(0);
                state.V = Bit(variant == 7); state.T = Bit(false);
            }
            else if (panel && (variant == 0 || variant == 7))
            {
                state.Acc = y; state.V = Bit(variant == 7); state.T = Bit(false);
            }
            else Sum(x, y, variant & 1);
        }

        private void Sum(LogicWord x, LogicWord y, int carry)
        {
            if (!x.IsBinary || !y.IsBinary)
            {
                state.Acc = LogicWord.Filled(8, LogicBit.X); state.V = state.T = LogicWord.Bit(LogicBit.X); return;
            }
            int a = x.ToInt(), b = y.ToInt(), sum = a + b + carry;
            bool c7 = (a & 127) + (b & 127) + carry >= 128;
            state.Acc = Byte(sum & 255); state.V = Bit(sum >= 256); state.T = Bit(c7 != (sum >= 256));
        }

        private void Shift(int mode)
        {
            var a = state.Acc; var v = state.V;
            bool left = mode >= 4 && mode <= 7;
            var outgoing = a.Slice(left ? 7 : 0, 1);
            var incoming = mode == 1 || mode == 3 || mode == 5 || mode == 7 ? v :
                mode == 2 || mode == 6 ? outgoing : mode == 8 ? a.Slice(7, 1) : Bit(false);
            state.Acc = left ? LogicWord.Concat(a.Slice(0, 7), incoming) : LogicWord.Concat(incoming, a.Slice(1, 7));
            state.V = outgoing;
        }
        private int Known(LogicWord value, string context)
        {
            if (!value.IsBinary) Fail("PF_UNDEFINED: " + context);
            return value.ToInt();
        }
        private void Fail(string message) => throw new MachineDiagnostic(NowNs, message);
    }
}
