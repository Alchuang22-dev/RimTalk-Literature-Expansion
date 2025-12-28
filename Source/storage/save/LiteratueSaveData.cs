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

        public LiteratueSaveData(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref SynopsisCache, "synopsisCache");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SynopsisCache == null)
                SynopsisCache = new BookSynopsisCache();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Log.Message($"[RimTalk LE] Literature data loaded. Cached synopses: {SynopsisCache?.Count ?? 0}.");
        }

        public static LiteratueSaveData Current => Find.World?.GetComponent<LiteratueSaveData>();
    }
}
