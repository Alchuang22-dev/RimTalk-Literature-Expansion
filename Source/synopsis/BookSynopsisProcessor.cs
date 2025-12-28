using System;
using System.Threading.Tasks;
using RimTalk.Service;
using RimTalk_LiteratureExpansion.authoring;
using RimTalk_LiteratureExpansion.authoring.llm;
using RimTalk_LiteratureExpansion.integration;
using RimTalk_LiteratureExpansion.scanner.queue;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using RimTalk_LiteratureExpansion.synopsis.model;
using Verse;

namespace RimTalk_LiteratureExpansion.synopsis
{
    public static class BookSynopsisProcessor
    {
        private const int MaxAttempts = 3;
        private static bool _processing;

        public static void Tick()
        {
            if (_processing) return;
            if (AIService.IsBusy()) return;
            if (!PendingBookQueue.TryDequeue(out var record)) return;
            if (record == null || record.Meta == null) return;
            if (record.Meta.Thing == null || record.Meta.Thing.DestroyedOrNull()) return;

            Log.Message($"[RimTalk LE] Processing book {record.Meta.Title} ({record.Meta.DefName}) [{record.Meta.Type}].");

            var cache = LiteratueSaveData.Current?.SynopsisCache;
            if (cache == null) return;

            if (cache.TryGet(record.Key, out var cached))
            {
                BookTextApplier.Apply(record.Meta, cached.ToSynopsis());
                Log.Message($"[RimTalk LE] Applied cached synopsis for {record.Meta.DefName}.");
                return;
            }

            record.IncrementAttempts();
            _processing = true;

            var summaryRequest = record.HasAuthor ? MemorySummaryRequest.BuildRequest(record.Author) : null;
            if (record.HasAuthor && summaryRequest == null)
                Log.Message($"[RimTalk LE] Failed to build memory summary request for {record.Meta.DefName}.");

            var contextPawn = ResolveContextPawn(record);
            if (contextPawn == null)
            {
                Log.Message($"[RimTalk LE] No context pawn available for {record.Meta.DefName}; requeue.");
                if (record.Attempts < MaxAttempts)
                    PendingBookQueue.Requeue(record);

                _processing = false;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    BookSynopsis synopsis = null;

                    if (record.HasAuthor && summaryRequest != null)
                    {
                        Log.Message($"[RimTalk LE] Prepare Generating from author memories for {record.Meta.DefName}.");
                        synopsis = await BookAuthoringPipeline.GenerateFromSummaryRequestAsync(
                            record.Meta,
                            record.Author,
                            summaryRequest);
                    }

                    if (synopsis == null)
                    {
                        Log.Message($"[RimTalk LE] Prepare Generating synopsis via LLM for {record.Meta.DefName}.");
                        synopsis = await BookSynopsisService.GetOrGenerateAsync(record.Meta, contextPawn);
                    }

                    if (synopsis != null)
                    {
                        cache.Set(record.Key, new BookSynopsisRecord(synopsis, record.Meta.Type));
                        BookTextApplier.Apply(record.Meta, synopsis);
                        Log.Message($"[RimTalk LE] Saved synopsis for {record.Meta.DefName}.");
                        return;
                    }

                    if (record.Attempts < MaxAttempts)
                    {
                        Log.Message($"[RimTalk LE] LLM returned null for {record.Meta.DefName}.");
                        Log.Message($"[RimTalk LE] Synopsis generation failed; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingBookQueue.Requeue(record);
                    }
                }
                catch (Exception ex)
                {
                    if (record.Attempts < MaxAttempts)
                    {
                        Log.Message($"[RimTalk LE] LLM threw exception for {record.Meta.DefName}: {ex.GetType().Name} - {ex.Message}");
                        Log.Message($"[RimTalk LE] Exception during synopsis generation; requeue {record.Meta.DefName} (attempt {record.Attempts}).");
                        PendingBookQueue.Requeue(record);
                    }
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        private static Pawn ResolveContextPawn(PendingBookRecord record)
        {
            if (record?.Author != null) return record.Author;

            var map = record?.Meta?.Thing?.Map;
            var pawns = map?.mapPawns?.FreeColonistsSpawned;
            var pawn = TryPickFirst(pawns);
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
