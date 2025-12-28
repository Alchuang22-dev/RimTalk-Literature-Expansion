using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.synopsis.model;
using Verse;

namespace RimTalk_LiteratureExpansion.storage
{
    public sealed class BookSynopsisRecord : IExposable
    {
        public string Title;
        public string Synopsis;
        public BookType Type = BookType.Unknown;
        public int GeneratedTick;

        public BookSynopsisRecord()
        {
        }

        public BookSynopsisRecord(BookSynopsis synopsis, BookType type)
        {
            Title = synopsis?.Title ?? string.Empty;
            Synopsis = synopsis?.Synopsis ?? string.Empty;
            Type = type;
            GeneratedTick = GenTicks.TicksGame;
        }

        public BookSynopsis ToSynopsis()
        {
            return new BookSynopsis
            {
                Title = Title ?? string.Empty,
                Synopsis = Synopsis ?? string.Empty
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Title, "title");
            Scribe_Values.Look(ref Synopsis, "synopsis");
            Scribe_Values.Look(ref Type, "type", BookType.Unknown);
            Scribe_Values.Look(ref GeneratedTick, "generatedTick", 0);
        }
    }
}
