using System;
using System.Collections.Generic;

namespace PatinhoFeio
{
    /// <summary>4096 cells and timed M1/M3/M4 transactions. IDX/EXT are cells 0/1.</summary>
    public sealed class FunctionalMemory
    {
        private enum Access { None, M1, Gap, M3, M4 }
        private readonly LogicWord[] cells = new LogicWord[4096];
        private Access access;
        private long started, ends;
        private int address;
        private LogicWord writeData;
        private bool blocked;
        internal LogicWord ReadData { get; private set; } = LogicWord.Filled(8, LogicBit.U);
        public bool HasTransaction => access != Access.None;
        public LogicWord this[int index] => cells[Address(index)];

        public FunctionalMemory(IEnumerable<KeyValuePair<int, LogicWord>> image = null)
        {
            for (int i = 0; i < cells.Length; i++) cells[i] = LogicWord.Filled(8, LogicBit.U);
            if (image == null) return;
            var seen = new HashSet<int>();
            foreach (var entry in image)
            {
                Address(entry.Key);
                if (entry.Value.Width != 8 || !seen.Add(entry.Key)) throw new ArgumentException("Invalid sparse image");
                cells[entry.Key] = entry.Value;
            }
        }
        private static int Address(int value) => value >= 0 && value < 4096 ? value :
            throw new ArgumentOutOfRangeException(nameof(value));

        internal void BeginRead(long now, LogicWord location, bool split)
        {
            if (HasTransaction) throw new MachineDiagnostic(now, "PF_MEMORY: unrestored or overlapping read");
            if (!location.IsBinary) throw new MachineDiagnostic(now, "PF_MEMORY: unknown address");
            address = Address(location.ToInt()); started = now; ends = now + (split ? 600 : 1600);
            access = split ? Access.M1 : Access.M4;
        }
        internal void BeginWrite(long now, LogicWord data, LogicWord protection)
        {
            if (access != Access.Gap) throw new MachineDiagnostic(now, "PF_MEMORY: M3 requires M1");
            if (!protection.IsBinary) throw new MachineDiagnostic(now, "PF_MEMORY: undefined protection");
            blocked = protection.ToInt() == 1 && address >= 0xF80;
            writeData = data; started = now; ends = now + 1000; access = Access.M3;
        }
        internal bool WriteEnable(long nextEdge) => access == Access.M3 && !blocked && nextEdge == ends;
        internal void Tick(long now)
        {
            if ((access == Access.M1 || access == Access.M4) && now == started + 400) ReadData = cells[address];
            if (now != ends) return;
            if (access == Access.M3 && !blocked) cells[address] = writeData;
            if (access == Access.M1) access = Access.Gap;
            else if (access != Access.Gap) access = Access.None;
        }
        internal void Abort()
        {
            // CE-09: only the interrupted cell loses its guarantee, including the M1–M3 gap.
            if (HasTransaction) cells[address] = LogicWord.Filled(8, LogicBit.X);
            access = Access.None;
        }
    }
}
