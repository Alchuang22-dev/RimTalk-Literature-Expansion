using RimTalk_LiteratureExpansion.synopsis;
using Verse;

namespace RimTalk_LiteratureExpansion
{
    public sealed class LiteratureGameComponent : GameComponent
    {
        public LiteratureGameComponent()
        {
        }

        public LiteratureGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            BookSynopsisProcessor.Tick();
        }
    }
}
