/*
 * File: MapArtScanner.cs
 *
 * Purpose:
 * - Scan the current Map for art Things that have not yet been processed.
 *
 * Dependencies:
 * - Verse.Map
 * - Map.listerThings
 * - ArtClassifier
 * - PendingArtQueue
 *
 * Responsibilities:
 * - Iterate all Things in the map.
 * - Identify art via ArtClassifier.
 * - Enqueue unprocessed art for later handling.
 *
 * Do NOT:
 * - Do not generate art content here.
 * - Do not write to save data directly.
 * - Do not run LLM calls.
 */
using RimTalk_LiteratureExpansion.art;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.scanner
{
    public static class MapArtScanner
    {
        public static void Scan(Map map)
        {
            if (map == null) return;
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.allowArtEdits)
            {
                Log.Message($"[RimTalk LE] Art scan skipped: allowArtEdits disabled (map {map.uniqueID}).");
                return;
            }

            var cache = LiteratueSaveData.Current?.ArtCache;
            if (cache == null)
            {
                Log.Message($"[RimTalk LE] Art scan skipped: ArtCache unavailable (map {map.uniqueID}).");
                return;
            }
            var things = map.listerThings?.AllThings;
            if (things == null || things.Count == 0)
            {
                Log.Message($"[RimTalk LE] Art scan: no things on map {map.uniqueID}.");
                return;
            }

            int matched = 0;
            int enqueued = 0;
            int cached = 0;
            int noArtComp = 0;
            int notShowable = 0;

            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing == null || thing.DestroyedOrNull()) continue;

                var comp = thing.TryGetComp<RimWorld.CompArt>();
                if (comp == null)
                {
                    noArtComp++;
                    continue;
                }
                if (!comp.CanShowArt)
                {
                    notShowable++;
                    continue;
                }

                var meta = ArtClassifier.Classify(thing);
                if (meta == null) continue;
                matched++;

                if (ArtKeyProvider.TryGetKey(meta.Thing, out var key) &&
                    cache != null &&
                    cache.Contains(key))
                {
                    cached++;
                    continue;
                }

                if (PendingArtQueue.Enqueue(meta))
                    enqueued++;
            }

            if (matched > 0)
            {
                Log.Message($"[RimTalk LE] Scan map {map.uniqueID}: art {matched}, enqueued {enqueued}, cached {cached}, noComp {noArtComp}, notShowable {notShowable}.");
            }
            else
            {
                Log.Message($"[RimTalk LE] Scan map {map.uniqueID}: no art matched (noComp {noArtComp}, notShowable {notShowable}).");
            }
        }
    }
}
