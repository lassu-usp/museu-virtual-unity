namespace PatinhoFeio
{
    // ISA M1977, docs/instruction-set.md. A single decode contract; no RTL dependency.
    internal enum Operation { Unknown, Pla, Plax, Arm, Armx, Car, Carx, Som, Somx, Plan, Plaz,
        Sus, Pug, Group1, FlagTest, Pul, Tre, Inib, Perm, Esp, Pare, Tri, Ind, Shift, Xor, Nand, Somi, Cari, Io }

    internal readonly struct Instruction
    {
        public Operation Op { get; }
        public int Code { get; }
        public bool Memory => Op >= Operation.Pla && Op <= Operation.Pug;
        public bool Indexed => Op == Operation.Plax || Op == Operation.Armx || Op == Operation.Carx || Op == Operation.Somx;
        public bool Jump => Op == Operation.Pla || Op == Operation.Plax || Op == Operation.Plan || Op == Operation.Plaz;
        public bool Swap => Op == Operation.Tre || Op == Operation.Tri;
        public bool Store => Op == Operation.Arm || Op == Operation.Armx || Op == Operation.Pug || Op == Operation.Sus || Swap;
        public bool Short => Code >= 0x80 && Code <= 0x9F;
        private Instruction(Operation op, int code) { Op = op; Code = code; }
        public static Instruction Decode(LogicWord word)
        {
            if (!word.IsBinary) return new Instruction(Operation.Unknown, -1);
            int code = word.ToInt();
            var family = code >> 4;
            if (family <= 7) return new Instruction(Operation.Pla + family, code);
            if (family == 8) return new Instruction(Operation.Group1, code);
            if (code <= 0x97) return new Instruction(Operation.FlagTest, code);
            if (code <= 0x9F) return new Instruction(Operation.Pul + (code - 0x98), code);
            switch (family)
            {
                case 10: return new Instruction(Operation.Plan, code);
                case 11: return new Instruction(Operation.Plaz, code);
                case 12: return new Instruction(Operation.Io, code);
                case 14: return new Instruction(Operation.Sus, code);
                case 15: return new Instruction(Operation.Pug, code);
            }
            switch (code)
            {
                case 0xD1: return new Instruction(Operation.Shift, code);
                case 0xD2: return new Instruction(Operation.Xor, code);
                case 0xD4: return new Instruction(Operation.Nand, code);
                case 0xD8: return new Instruction(Operation.Somi, code);
                case 0xDA: return new Instruction(Operation.Cari, code);
                default: return new Instruction(Operation.Unknown, code);
            }
        }
    }
}
