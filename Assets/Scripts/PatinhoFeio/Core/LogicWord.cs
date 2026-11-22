using System;
using System.Globalization;

namespace PatinhoFeio
{
    // CE-09 / docs/vectors.md: U is uninitialized, X is indeterminate, neither is zero.
    public enum LogicBit : byte { U, X, Zero, One }

    /// <summary>Immutable, width-checked word. Bit indices use least significant bit = 0.</summary>
    public readonly struct LogicWord : IEquatable<LogicWord>
    {
        private readonly uint ones, unknown, uninitialized;
        public int Width { get; }
        public uint KnownOnes => ones;
        public bool IsBinary => Width > 0 && (unknown | uninitialized) == 0;
        private static uint Mask(int width) => (1u << width) - 1;

        private LogicWord(int width, uint ones, uint unknown, uint uninitialized)
        {
            if (width < 1 || width > 16) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            this.ones = ones & Mask(width);
            this.unknown = unknown & Mask(width);
            this.uninitialized = uninitialized & Mask(width);
        }

        public static LogicWord Binary(int width, int value)
        {
            if (width < 1 || width > 16 || value < 0 || value > Mask(width))
                throw new ArgumentOutOfRangeException(nameof(value));
            return new LogicWord(width, (uint)value, 0, 0);
        }

        public static LogicWord Filled(int width, LogicBit bit) => new LogicWord(width,
            bit == LogicBit.One ? Mask(width) : 0,
            bit == LogicBit.X ? Mask(width) : 0,
            bit == LogicBit.U ? Mask(width) : 0);

        public static LogicWord Bit(LogicBit bit) => Filled(1, bit);
        public LogicBit this[int index]
        {
            get
            {
                if (index < 0 || index >= Width) throw new ArgumentOutOfRangeException(nameof(index));
                uint mask = 1u << index;
                return (uninitialized & mask) != 0 ? LogicBit.U : (unknown & mask) != 0 ? LogicBit.X :
                    (ones & mask) != 0 ? LogicBit.One : LogicBit.Zero;
            }
        }

        public int ToInt()
        {
            if (!IsBinary) throw new InvalidOperationException("Indeterminate word cannot be converted to an integer: " + this);
            return (int)ones;
        }

        public LogicWord Slice(int low, int width)
        {
            if (low < 0 || width < 1 || low + width > Width) throw new ArgumentOutOfRangeException(nameof(low));
            return new LogicWord(width, ones >> low, unknown >> low, uninitialized >> low);
        }

        public static LogicWord Concat(LogicWord high, LogicWord low) => new LogicWord(high.Width + low.Width,
            (high.ones << low.Width) | low.ones, (high.unknown << low.Width) | low.unknown,
            (high.uninitialized << low.Width) | low.uninitialized);

        public LogicWord Not() => new LogicWord(Width, ~ones & ~(unknown | uninitialized), unknown, uninitialized);

        public LogicWord And(LogicWord other) => Combine(other, false);
        public LogicWord Or(LogicWord other) => Not().And(other.Not()).Not();
        public LogicWord Xor(LogicWord other) => Combine(other, true);
        private LogicWord Combine(LogicWord other, bool xor)
        {
            if (Width != other.Width) throw new ArgumentException("Word widths differ");
            uint result = 0, x = 0, u = 0;
            for (int i = 0; i < Width; i++)
            {
                var a = this[i]; var b = other[i]; uint mask = 1u << i;
                if (!xor && (a == LogicBit.Zero || b == LogicBit.Zero)) continue;
                if (a == LogicBit.U || b == LogicBit.U) u |= mask;
                else if (a == LogicBit.X || b == LogicBit.X) x |= mask;
                else if (xor ? a != b : a == LogicBit.One && b == LogicBit.One) result |= mask;
            }
            return new LogicWord(Width, result, x, u);
        }

        // Address arithmetic (CE-05); unknown arithmetic stays indeterminate.
        public LogicWord AddModulo(int value) => IsBinary
            ? Binary(Width, (ToInt() + value) & (int)Mask(Width)) : Filled(Width, LogicBit.X);

        public static LogicWord Parse(string text, int width)
        {
            if (text == null) throw new FormatException("Missing logic word");
            if (text.StartsWith("0x", StringComparison.Ordinal) && width % 4 == 0 && text.Length == 2 + width / 4)
            {
                foreach (char c in text.Substring(2))
                    if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'F')) throw new FormatException("Invalid hexadecimal word");
                return Binary(width, int.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            if (!text.StartsWith("0b", StringComparison.Ordinal) || text.Length != width + 2)
                throw new FormatException("Invalid word width or notation: " + text);
            uint bits = 0, x = 0, u = 0;
            foreach (char c in text.Substring(2))
            {
                bits <<= 1; x <<= 1; u <<= 1;
                switch (c)
                {
                    case '0': break;
                    case '1': bits |= 1; break;
                    case 'X': x |= 1; break;
                    case 'U': u |= 1; break;
                    default: throw new FormatException("Invalid logic symbol: " + c);
                }
            }
            return new LogicWord(width, bits, x, u);
        }

        public override string ToString()
        {
            if (IsBinary && Width % 4 == 0) return "0x" + ones.ToString("X" + Width / 4, CultureInfo.InvariantCulture);
            var chars = new char[Width];
            const string symbols = "UX01";
            for (int i = 0; i < Width; i++) chars[Width - 1 - i] = symbols[(int)this[i]];
            return "0b" + new string(chars);
        }
        public bool Equals(LogicWord other) => Width == other.Width && ones == other.ones &&
            unknown == other.unknown && uninitialized == other.uninitialized;
        public override bool Equals(object obj) => obj is LogicWord other && Equals(other);
        public override int GetHashCode() => unchecked((((Width * 397) ^ (int)ones) * 397 ^ (int)unknown) * 397 ^ (int)uninitialized);
        public static bool operator ==(LogicWord a, LogicWord b) => a.Equals(b);
        public static bool operator !=(LogicWord a, LogicWord b) => !a.Equals(b);
    }
}
