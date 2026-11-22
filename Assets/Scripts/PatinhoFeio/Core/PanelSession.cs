using System;
using System.Collections.Generic;

namespace PatinhoFeio
{
    /// <summary>One museum computer with retained ferrite and no attached peripherals.</summary>
    public sealed class PanelSession
    {
        // /010: PNL 0, INC, PARE. A second PARTIDA runs INC / PLA /013 in a loop.
        static IEnumerable<KeyValuePair<int, LogicWord>> ResidentExample()
        {
            byte[] program = { 0x88, 0x85, 0x9D, 0x85, 0x00, 0x13 };
            for (int i = 0; i < program.Length; i++)
                yield return new KeyValuePair<int, LogicWord>(0x010 + i, LogicWord.Binary(8, program[i]));
        }

        static IEnumerable<KeyValuePair<int, LogicWord>> RetainedCells(FunctionalMemory memory)
        {
            for (int i = 0; i < 4096; i++) yield return new KeyValuePair<int, LogicWord>(i, memory[i]);
        }

        static LogicWord Bit(bool high) => LogicWord.Binary(1, high ? 1 : 0);
        readonly SystemInputs controls = new SystemInputs
        {
            Rc = LogicWord.Binary(12, 0), Increment = Bit(true), Protect = Bit(true)
        };
        SystemMachine system;
        public bool Powered { get; private set; }
        public bool Running => Powered && Diagnostic == null && !system.Stopped;
        public string Diagnostic { get; private set; }
        public PanelMode Mode => system.Mode;
        public MachineState Registers => system.Core.Registers;
        public int Switches => controls.Rc.ToInt();
        public bool Sequential => controls.Increment == Bit(true);
        public bool Protected => controls.Protect == Bit(true);

        // Bit positions shared by the physical lenses and exposure counters.
        public const int Acc = 0, Ri = 8, Rd = 16, Ci = 24, Re = 36, Carry = 48,
            Overflow = 49, Halt = 50, Phase1 = 51, Rc = 58, External = 70;
        readonly int[] litTicks = new int[Rc];
        int frameTicks;

        public PanelSession() => PowerOn();

        public void PowerOn()
        {
            if (Powered) return;
            var image = system == null ? ResidentExample() : RetainedCells(system.Core.Memory);
            var interfaces = new InterfaceState[15];
            for (int i = 0; i < interfaces.Length; i++) interfaces[i] = InterfaceState.Unknown();
            system = new SystemMachine(MachineState.Unknown(), image, interfaces);
            Powered = true;
            Prepare();
        }

        public void PowerOff()
        {
            if (!Powered) return;
            Prepare(); // An interrupted ferrite transaction loses its guarantee.
            Powered = false;
        }

        public void Prepare()
        {
            if (!Powered) return;
            Diagnostic = null;
            frameTicks = 0;
            controls.Prepare = Bit(true);
            system.PrepareAtPanel(controls);
            controls.Prepare = Bit(false);
            Apply();
        }

        public void ToggleSwitch(int historicalIndex)
        {
            if (historicalIndex < 0 || historicalIndex > 11) throw new ArgumentOutOfRangeException(nameof(historicalIndex));
            controls.Rc = LogicWord.Binary(12, Switches ^ (1 << (11 - historicalIndex)));
            Apply();
        }
        public void ToggleIncrement() { controls.Increment = controls.Increment.Not(); Apply(); }
        public void ToggleProtection() { controls.Protect = controls.Protect.Not(); Apply(); }
        public void Select(PanelMode mode) { controls.Mode = mode; controls.Select = Bit(true); Pulse(); }
        public void Start()
        {
            bool available = !Running;
            controls.Start = Bit(true);
            Pulse();
            // CE, CP and MP occupy just one 2 µs cycle. Finish it inside the click
            // so the next key change cannot overtake a pending store on a fast UI.
            if (available && Running && system.Manual)
                for (int i = 0; i < 40 && Running; i++) Tick();
        }
        public void Wait() { controls.Wait = Bit(true); Pulse(); }
        public void Interrupt() { controls.Interrupt = Bit(true); Pulse(); }

        void Apply()
        {
            if (Powered && Diagnostic == null) system.ApplyInputs(system.Core.NowNs, controls);
        }

        void Pulse()
        {
            Apply();
            if (Powered && Diagnostic == null) Tick();
            controls.Select = controls.Start = controls.Wait = controls.Interrupt = Bit(false);
            Apply();
            frameTicks = 0;
        }

        void Tick()
        {
            try { system.AdvanceTo(system.Core.NowNs + 50); }
            catch (MachineDiagnostic failure) { Diagnostic = failure.Message; }
        }

        ulong LampBits()
        {
            var r = Registers;
            ulong phases = system.Core.Active && !system.Manual ? 1UL << (Phase1 + (int)system.Core.Phase) : 0;
            return r.Acc.KnownOnes | ((ulong)r.Ri.KnownOnes << Ri) | ((ulong)r.Rd.KnownOnes << Rd)
                | ((ulong)r.Ci.KnownOnes << Ci) | ((ulong)r.Re.KnownOnes << Re)
                | ((ulong)r.V.KnownOnes << Carry) | ((ulong)r.T.KnownOnes << Overflow)
                | (Running ? 0 : 1UL << Halt) | phases;
        }

        public void RunFrame(int clocks)
        {
            frameTicks = 0;
            if (!Running || clocks <= 0) return;
            Array.Clear(litTicks, 0, litTicks.Length);
            ulong previous = LampBits();
            int since = 0;
            for (int i = 0; i < clocks && Running; i++)
            {
                Tick();
                ulong current = LampBits();
                if (current == previous) continue;
                Accumulate(previous, i + 1 - since);
                previous = current;
                since = i + 1;
            }
            Accumulate(previous, clocks - since);
            frameTicks = clocks;
        }

        void Accumulate(ulong mask, int ticks)
        {
            for (int bit = 0; mask != 0; bit++, mask >>= 1)
                if ((mask & 1) != 0) litTicks[bit] += ticks;
        }

        public float Brightness(int lamp)
        {
            if (!Powered || lamp == External) return 0;
            if (lamp >= Rc) return (Switches >> (lamp - Rc)) & 1;
            if (frameTicks == 0) return (LampBits() & (1UL << lamp)) == 0 ? 0 : 1;
            return (float)litTicks[lamp] / frameTicks;
        }
    }
}
