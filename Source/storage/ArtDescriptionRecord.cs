using RimTalk_LiteratureExpansion.art.model;
using Verse;

namespace RimTalk_LiteratureExpansion.storage
{
    public sealed class ArtDescriptionRecord : IExposable
    {
        public string Title;
        public string Text;
        public int GeneratedTick;

        public ArtDescriptionRecord()
        {
        }

        public ArtDescriptionRecord(ArtDescription description)
        {
            Title = description?.Title ?? string.Empty;
            Text = description?.Text ?? string.Empty;
            GeneratedTick = GenTicks.TicksGame;
        }

        public ArtDescription ToDescription()
        {
            return new ArtDescription
            {
                Title = Title ?? string.Empty,
                Text = Text ?? string.Empty
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Title, "title");
            Scribe_Values.Look(ref Text, "text");
            Scribe_Values.Look(ref GeneratedTick, "generatedTick", 0);
        }
    }
}
