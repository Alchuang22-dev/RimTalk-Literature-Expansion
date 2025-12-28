using RimWorld;
using Verse;

namespace RimTalk_LiteratureExpansion.art
{
    public static class ArtClassifier
    {
        public static ArtMeta Classify(Thing thing)
        {
            if (thing == null) return null;

            var comp = thing.TryGetComp<CompArt>();
            if (comp == null) return null;
            if (!comp.CanShowArt) return null;

            return new ArtMeta(thing);
        }
    }
}
