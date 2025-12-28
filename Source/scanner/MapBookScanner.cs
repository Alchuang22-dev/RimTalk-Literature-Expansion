/*
 * File: MapBookScanner.cs
 *
 * Purpose:
 * - Scan the current Map for book Things that have not yet been processed.
 *
 * Dependencies:
 * - Verse.Map
 * - Map.listerThings
 * - BookClassifier
 * - PendingBookQueue
 *
 * Responsibilities:
 * - Iterate all Things in the map.
 * - Identify books via BookClassifier.
 * - Enqueue unprocessed books for later handling.
 *
 * Do NOT:
 * - Do not generate book content.
 * - Do not write to save data directly.
 * - Do not run LLM calls.
 */
using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner
{
    public static class MapBookScanner
    {
        public static void Scan(Map map)
        {
            if (map == null) return;

            var cache = LiteratueSaveData.Current?.SynopsisCache;
            var things = map.listerThings?.AllThings;
            if (things == null || things.Count == 0) return;

            int matched = 0;
            int enqueued = 0;
            int cached = 0;

            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing == null || thing.DestroyedOrNull()) continue;

                if (thing.def != null &&
                    thing.def.category != ThingCategory.Item &&
                    !(thing is Book))
                {
                    continue;
                }

                var meta = BookClassifier.Classify(thing);
                if (meta == null) continue;
                matched++;

                if (BookKeyProvider.TryGetKey(meta.Thing, out var key) &&
                    cache != null &&
                    cache.Contains(key))
                {
                    cached++;
                    continue;
                }

                if (PendingBookQueue.Enqueue(meta))
                    enqueued++;
            }

            if (matched > 0)
            {
                Log.Message($"[RimTalk LE] Scan map {map.uniqueID}: books {matched}, enqueued {enqueued}, cached {cached}.");
            }
        }
    }
}
