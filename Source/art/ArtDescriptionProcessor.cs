using System;
using System.Threading.Tasks;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.art
{
    public static class ArtDescriptionProcessor
    {
        private const int MaxAttempts = 3;
        private static bool _processing;

        public static void Tick()
        {
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.allowArtEdits) return;

            if (_processing) return;
            if (!PendingArtQueue.TryDequeue(out var record)) return;
            if (record == null || record.Meta == null) return;
            if (record.Meta.Thing == null || record.Meta.Thing.DestroyedOrNull()) return;

            Log.Message($"[RimTalk LE] Processing art {record.Meta.ThingLabel} ({record.Meta.DefName}).");

            var cache = LiteratueSaveData.Current?.ArtCache;
            if (cache == null) return;

            if (cache.TryGet(record.Key, out _))
            {
                Log.Message($"[RimTalk LE] Art description already cached for {record.Meta.DefName}.");
                return;
            }

            record.IncrementAttempts();
            _processing = true;

            var contextPawn = ResolveContextPawn(record);
            if (contextPawn == null)
            {
                Log.Message($"[RimTalk LE] No context pawn available for art {record.Meta.DefName}; requeue.");
                if (record.Attempts < MaxAttempts)
                    PendingArtQueue.Requeue(record);

                _processing = false;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var description = await ArtDescriptionService.GetOrGenerateAsync(record.Meta, contextPawn);
                    if (description != null)
                    {
                        cache.Set(record.Key, new ArtDescriptionRecord(description));
                        Log.Message($"[RimTalk LE] Saved art description for {record.Meta.DefName}.");
                        return;
                    }

                    if (record.Attempts < MaxAttempts)
                    {
                        Log.Message($"[RimTalk LE] LLM returned null for art {record.Meta.DefName}.");
                        Log.Message($"[RimTalk LE] Art generation failed; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingArtQueue.Requeue(record);
                    }
                }
                catch (Exception ex)
                {
                    if (record.Attempts < MaxAttempts)
                    {
                        Log.Message($"[RimTalk LE] LLM threw exception for art {record.Meta.DefName}: {ex.GetType().Name} - {ex.Message}");
                        Log.Message($"[RimTalk LE] Exception during art generation; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingArtQueue.Requeue(record);
                    }
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        private static Pawn ResolveContextPawn(PendingArtRecord record)
        {
            var map = record?.Meta?.Thing?.Map;
            var pawn = TryPickFirst(map?.mapPawns?.FreeColonistsSpawned);
            if (pawn != null) return pawn;

            pawn = TryPickHumanlike(map?.mapPawns?.AllPawnsSpawned);
            if (pawn != null) return pawn;

            var maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    pawn = TryPickFirst(maps[i]?.mapPawns?.FreeColonistsSpawned);
                    if (pawn != null) return pawn;

                    pawn = TryPickHumanlike(maps[i]?.mapPawns?.AllPawnsSpawned);
                    if (pawn != null) return pawn;
                }
            }

            return null;
        }

        private static Pawn TryPickFirst(System.Collections.Generic.IReadOnlyList<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0) return null;
            return pawns[0];
        }

        private static Pawn TryPickHumanlike(System.Collections.Generic.IReadOnlyList<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0) return null;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn?.RaceProps?.Humanlike == true) return pawn;
            }
            return null;
        }
    }
}
