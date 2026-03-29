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
 * - Track produced book Things directly from recipe output.
 *
 * Do NOT:
 * - Do not generate content here.
 * - Do not access LLM services.
 * - Do not scan based on position; use direct product tracking.
 */
using System.Collections.Generic;
using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using RimTalk_LiteratureExpansion.synopsis;
using RimWorld;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner.production
{
    public static class BookProductionTracker
    {
        public static void NotifyProduced(Pawn worker)
        {
            if (worker == null) return;
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;
        }

        public static void NotifyProducts(IEnumerable<Thing> products, Pawn worker, RecipeDef recipeDef)
        {
            if (products == null) return;
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            int matched = 0;
            int enqueued = 0;
            int cached = 0;

            var cache = LiteratueSaveData.Current?.SynopsisCache;
            var mapOverride = worker?.Map;

            foreach (var product in products)
            {
                if (product == null || product.DestroyedOrNull()) continue;

                var meta = BookClassifier.Classify(product);
                if (meta == null) continue;
                if (!BookFilterPolicy.IsAllowed(meta)) continue;
                matched++;

                if (BookKeyProvider.TryGetKey(meta.Thing, mapOverride, out var key) &&
                    cache != null &&
                    cache.Contains(key))
                {
                    cached++;
                    continue;
                }

                if (PendingBookQueue.Enqueue(meta, worker, mapOverride))
                    enqueued++;
            }

            if (matched > 0)
            {
                Log.Message($"[RimTalk LE] Produced books via {recipeDef?.defName ?? "recipe"}: matched {matched}, enqueued {enqueued}, cached {cached}.");
            }

            if (enqueued > 0)
            {
                BookSynopsisProcessor.Tick();
            }
        }
    }
}
