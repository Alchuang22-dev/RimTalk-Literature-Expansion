/*
 * File: LiteratueSaveData.cs
 *
 * Purpose:
 * - Persist Literature Expansion data across save/load.
 *
 * Dependencies:
 * - Verse.IExposable
 * - BookSynopsisCache (or underlying data)
 *
 * Responsibilities:
 * - Expose cached synopsis and processed-book markers.
 *
 * Do NOT:
 * - Do not perform logic during ExposeData beyond serialization.
 */
using RimTalk_LiteratureExpansion.storage;
using RimWorld.Planet;
using Verse;

namespace RimTalk_LiteratureExpansion.storage.save
{
    public sealed class LiteratueSaveData : WorldComponent
    {
        public BookSynopsisCache SynopsisCache = new BookSynopsisCache();
        public ArtDescriptionCache ArtCache = new ArtDescriptionCache();

        public LiteratueSaveData(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref SynopsisCache, "synopsisCache");
            Scribe_Deep.Look(ref ArtCache, "artCache");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SynopsisCache == null)
                SynopsisCache = new BookSynopsisCache();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && ArtCache == null)
                ArtCache = new ArtDescriptionCache();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Log.Message($"[RimTalk LE] Literature data loaded. Cached synopses: {SynopsisCache?.Count ?? 0}, art: {ArtCache?.Count ?? 0}.");
        }

        public static LiteratueSaveData Current => Find.World?.GetComponent<LiteratueSaveData>();
    }
}
