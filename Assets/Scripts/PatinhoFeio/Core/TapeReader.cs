using System;
using System.Collections.Generic;
using System.Linq;

namespace PatinhoFeio
{
    /// <summary>Finite immutable scenario medium. CE-12 is behavioral, not a mechanical model.</summary>
    public sealed class TapeReader
    {
        private readonly LogicWord[] media;
        private readonly int frameTicks;
        private int remaining;
        public int Position { get; private set; }
        public bool Active { get; private set; }
        public bool Suppress { get; private set; }
        public TapeReader(IEnumerable<LogicWord> media, int position, int frameTicks)
        {
            this.media = (media ?? throw new ArgumentNullException(nameof(media))).ToArray();
            if (this.media.Any(b => b.Width != 8)) throw new ArgumentException("Reader byte width");
            if (frameTicks < 1) throw new MachineDiagnostic(0, "PF_READER: positive FRAME_TICKS required");
            if (position < 0 || position > this.media.Length) throw new MachineDiagnostic(0, "PF_READER: invalid media position or direction");
            Position = position; this.frameTicks = frameTicks;
        }
        private TapeReader(TapeReader source)
        {
            media = source.media; frameTicks = source.frameTicks; remaining = source.remaining;
            Position = source.Position; Active = source.Active; Suppress = source.Suppress;
        }
        internal TapeReader Snapshot() => new TapeReader(this);
        internal TapeReader Project(IoOperation command, LogicWord loaded, LogicWord online, LogicWord stalled,
            long atNs, bool diagnose, out DeviceEvent? arrival)
        {
            var next = new TapeReader(this); arrival = null;
            if (command == IoOperation.Output && diagnose)
                throw new MachineDiagnostic(atNs, "PF_READER: SAI reaction outside contract");
            if (command == IoOperation.Stop) { next.Prepare(); return next; }
            if (command == IoOperation.SkipNulls) next.Suppress = true;
            if (!Active)
            {
                if (command == IoOperation.Start || command == IoOperation.SkipNulls)
                { next.Active = true; next.remaining = frameTicks; }
                return next; // No consumption on the initiating edge, even L=1.
            }
            if (loaded != LogicWord.Binary(1, 1) || online != LogicWord.Binary(1, 1) ||
                stalled != LogicWord.Binary(1, 0) || Position == media.Length) return next;
            if (--next.remaining != 0) return next;
            var data = media[Position];
            if (next.Suppress && !data.IsBinary)
            {
                if (diagnose) throw new MachineDiagnostic(atNs, "PF_READER: unknown frame during null suppression");
                return this;
            }
            next.Position++;
            if (next.Suppress && data.ToInt() == 0) next.remaining = frameTicks;
            else
            {
                next.Active = next.Suppress = false;
                arrival = new DeviceEvent(14, 0, true, data, true);
            }
            return next;
        }
        internal void Prepare() { Active = Suppress = false; remaining = 0; }
    }
}
