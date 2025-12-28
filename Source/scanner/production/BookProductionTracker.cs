/*
 * File: BookProductionTracker.cs
 *
 * Purpose:
 * - Track books produced via Bill_Production (writing books).
 *
 * Dependencies:
 * - RimWorld Bill_Production
 * - Patch_BillProduction_Finish
 *
 * Responsibilities:
 * - Detect when a book has just been produced.
 * - Mark it as needing synopsis generation.
 *
 * Do NOT:
 * - Do not generate content here.
 * - Do not access LLM services.
 */
using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner.production
{
    public static class BookProductionTracker
    {
        public static void NotifyProduced(Pawn worker)
        {
            if (worker == null || worker.Map == null) return;

            var cache = LiteratueSaveData.Current?.SynopsisCache;
            var map = worker.Map;
            var center = worker.Position;

            int matched = 0;
            int enqueued = 0;

            foreach (var cell in GenRadial.RadialCellsAround(center, 3f, true))
            {
                if (!cell.InBounds(map)) continue;

                var things = cell.GetThingList(map);
                if (things == null || things.Count == 0) continue;

                for (int i = 0; i < things.Count; i++)
                {
                    var thing = things[i];
                    if (thing == null || thing.DestroyedOrNull()) continue;

                    var meta = BookClassifier.Classify(thing);
                    if (meta == null) continue;
                    matched++;

                    if (BookKeyProvider.TryGetKey(thing, out var key) &&
                        cache != null &&
                        cache.Contains(key))
                    {
                        continue;
                    }

                    if (PendingBookQueue.Enqueue(meta, worker))
                        enqueued++;
                }
            }

            if (matched > 0)
            {
                Log.Message($"[RimTalk LE] Production scan near {worker.LabelShort}: books {matched}, enqueued {enqueued}.");
            }
        }
    }
}
