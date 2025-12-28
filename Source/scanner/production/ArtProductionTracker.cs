/*
 * File: ArtProductionTracker.cs
 *
 * Purpose:
 * - Track newly generated art and enqueue for LLM description.
 */
using RimTalk_LiteratureExpansion.art;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.settings;
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
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.allowArtEdits) return;

            var meta = ArtClassifier.Classify(thing);
            if (meta == null) return;

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
