using System;
using System.Collections.Generic;

namespace PatinhoFeio
{
    // instruction-set.md, Entrada e saída: one decoder for CPU and all 15 interfaces.
    public enum IoOperation { None, Inhibit, ClearReady, SetReady, ClearPending, Permit, Start, Stop,
        SkipNulls, TestReady, TestOk, TestNoPending, Input, Output, Invalid }

    public readonly struct IoRequest
    {
        public int Channel { get; }
        public IoOperation Operation { get; }
        public LogicWord Data { get; }
        public bool Strobe => Operation != IoOperation.None && Operation != IoOperation.Invalid;
        public IoRequest(int channel, IoOperation operation, LogicWord data)
        {
            if (channel < 0 || channel > 15 || data.Width != 8) throw new ArgumentException("IO request");
            Channel = channel; Operation = operation; Data = data;
        }
        public static IoOperation Decode(int channel, LogicWord operand)
        {
            if (!operand.IsBinary) return IoOperation.Invalid;
            switch (operand.ToInt())
            {
                case 0x10: return IoOperation.Inhibit;
                case 0x11: return IoOperation.ClearReady;
                case 0x12: return IoOperation.SetReady;
                case 0x14: return IoOperation.ClearPending;
                case 0x15: return IoOperation.Permit;
                case 0x16: return IoOperation.Start;
                case 0x17: return IoOperation.Stop;
                case 0x18: return channel == 14 ? IoOperation.SkipNulls : IoOperation.Invalid;
                case 0x21: return IoOperation.TestReady;
                case 0x22: return IoOperation.TestOk;
                case 0x24: return IoOperation.TestNoPending;
                case 0x40: return IoOperation.Input;
                case 0x80: return IoOperation.Output;
                default: return IoOperation.Invalid;
            }
        }
    }

    public sealed class InterfaceState
    {
        public LogicWord Data { get; internal set; }
        public LogicWord Control { get; internal set; }
        public LogicWord Ready { get; internal set; }
        public LogicWord Pending { get; internal set; }
        public LogicWord Permitted { get; internal set; }
        public InterfaceState(LogicWord data, LogicWord control, LogicWord ready, LogicWord pending, LogicWord permitted)
        {
            if (data.Width != 8 || control.Width != 1 || ready.Width != 1 || pending.Width != 1 || permitted.Width != 1)
                throw new ArgumentException("Interface widths");
            Data = data; Control = control; Ready = ready; Pending = pending; Permitted = permitted;
        }
        public static InterfaceState Unknown() => new InterfaceState(LogicWord.Filled(8, LogicBit.U),
            LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U), LogicWord.Bit(LogicBit.U));
        public InterfaceState Snapshot() => new InterfaceState(Data, Control, Ready, Pending, Permitted);
    }

    public readonly struct DeviceEvent
    {
        public int Channel { get; }
        public int Slot { get; }
        public bool WriteData { get; }
        public LogicWord Data { get; }
        public bool Complete { get; }
        public DeviceEvent(int channel, int slot, bool writeData, LogicWord data, bool complete)
        {
            if (channel < 1 || channel > 15 || slot < 0 || slot > 1 || data.Width != 8 || !writeData && !complete)
                throw new ArgumentException("Device event");
            Channel = channel; Slot = slot; WriteData = writeData; Data = data; Complete = complete;
        }
    }

    /// <summary>Owns only committed interface state. Projections never mutate it (io.md CE-03/09).</summary>
    public sealed class IoInterface
    {
        private InterfaceState state;
        public IoInterface(InterfaceState initial) { state = initial.Snapshot(); }
        public InterfaceState State => state.Snapshot();
        public InterfaceState Direct(IoRequest request)
        {
            var s = state.Snapshot();
            switch (request.Operation)
            {
                case IoOperation.Inhibit: s.Permitted = LogicWord.Binary(1, 0); break;
                case IoOperation.Permit: s.Permitted = LogicWord.Binary(1, 1); break;
                case IoOperation.ClearReady: s.Ready = LogicWord.Binary(1, 0); break;
                case IoOperation.SetReady: s.Ready = LogicWord.Binary(1, 1); break;
                case IoOperation.ClearPending: s.Pending = LogicWord.Binary(1, 0); break;
                case IoOperation.Stop: s.Control = LogicWord.Binary(1, 0); break;
                case IoOperation.Output: s.Data = request.Data; goto case IoOperation.Start;
                case IoOperation.Start: s.Control = LogicWord.Binary(1, 1); s.Ready = LogicWord.Binary(1, 0); break;
            }
            return s;
        }
        public static InterfaceState AfterEvents(InterfaceState direct, IEnumerable<DeviceEvent> events)
        {
            var s = direct.Snapshot();
            foreach (var e in events)
            {
                if (e.WriteData) s.Data = e.Data;
                if (e.Complete) { s.Control = LogicWord.Binary(1, 0); s.Ready = LogicWord.Binary(1, 1); }
            }
            s.Pending = s.Pending.Or(s.Ready.And(s.Permitted));
            return s;
        }
        internal void Commit(InterfaceState projected) { state = projected.Snapshot(); }
        internal void Prepare()
        {
            state.Control = state.Ready = state.Pending = state.Permitted = LogicWord.Binary(1, 0);
        }
    }
}
