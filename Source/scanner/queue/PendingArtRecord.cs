using RimTalk_LiteratureExpansion.art;
using RimTalk_LiteratureExpansion.storage;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner.queue
{
    public sealed class PendingArtRecord
    {
        public ArtKey Key { get; }
        public ArtMeta Meta { get; }
        public int EnqueuedTick { get; }
        public int Attempts { get; private set; }

        public PendingArtRecord(ArtKey key, ArtMeta meta)
        {
            Key = key;
            Meta = meta;
            EnqueuedTick = GenTicks.TicksGame;
        }

        public void IncrementAttempts()
        {
            Attempts++;
        }
    }
}
