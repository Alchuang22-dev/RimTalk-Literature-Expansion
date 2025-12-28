using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.storage;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner.queue
{
    public sealed class PendingBookRecord
    {
        public BookKey Key { get; }
        public BookMeta Meta { get; }
        public Pawn Author { get; }
        public int EnqueuedTick { get; }
        public int Attempts { get; private set; }

        public bool HasAuthor => Author != null;

        public PendingBookRecord(BookKey key, BookMeta meta, Pawn author)
        {
            Key = key;
            Meta = meta;
            Author = author;
            EnqueuedTick = GenTicks.TicksGame;
        }

        public void IncrementAttempts()
        {
            Attempts++;
        }
    }
}
