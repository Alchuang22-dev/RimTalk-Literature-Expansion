/*
 * File: ArtProductionTracker.cs
 *
 * Purpose:
 * - Track newly generated art and enqueue for LLM description.
 */
using RimTalk_LiteratureExpansion.art;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner.production
{
    public static class ArtProductionTracker
    {
        public static void NotifyGenerated(Thing thing)
        {
            if (thing == null || thing.DestroyedOrNull()) return;
            if (!RimTalk_LiteratureExpansion.integration.ArtCacheUtil.IsArtEditingEnabled())
            {
                Log.Message($"[RimTalk LE] Art generation skipped: art category edits disabled ({RimTalk_LiteratureExpansion.integration.ArtCacheUtil.DescribeArtSettings()}).");
                return;
            }

            var meta = ArtClassifier.Classify(thing);
            if (meta == null)
            {
                var comp = thing.TryGetComp<RimWorld.CompArt>();
                if (comp != null)
                {
                    var settings = RimTalk_LiteratureExpansion.settings.LiteratureMod.Settings;
                    bool allowLabelEdits = settings != null && settings.allowArtLabelEdits;
                    if (!RimTalk_LiteratureExpansion.art.ArtEditPolicy.ShouldGenerate(thing, allowLabelEdits))
                        Log.Message($"[RimTalk LE] Art generation skipped: not eligible ({thing.LabelCap}, {thing.def?.defName}).");
                }
                return;
            }

            var cache = LiteratueSaveData.Current?.ArtCache;
            if (ArtKeyProvider.TryGetKey(thing, out var key) &&
                cache != null &&
                cache.Contains(key))
            {
                return;
            }

            if (PendingArtQueue.Enqueue(meta))
                Log.Message($"[RimTalk LE] Enqueued art {meta.ThingLabel} ({meta.DefName}) from generation.");
        }
    }
}
