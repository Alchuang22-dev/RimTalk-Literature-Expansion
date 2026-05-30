/*
 * Purpose:
 * - Generate persona weapon name/description from the bonded pawn's memory summary.
 */
using System;
using System.Threading.Tasks;
using RimTalk_LiteratureExpansion;
using RimTalk_LiteratureExpansion.art.llm;
using RimTalk_LiteratureExpansion.authoring.llm;
using RimTalk_LiteratureExpansion.integration;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.art
{
    public static class PersonaWeaponAuthoringPipeline
    {
        public static void StartGeneration(Thing weapon, Pawn pawn, string reason, Action onComplete = null)
        {
            if (weapon == null || pawn == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (!PlayerFactionUtility.IsPlayerFactionPawn(pawn))
            {
                Log.Message("[RimTalk LE] Persona weapon update skipped: bonded pawn is not an initialized player-faction pawn.");
                onComplete?.Invoke();
                return;
            }

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowArtWeaponEdits || !settings.allowArtLabelEdits)
            {
                Log.Message($"[RimTalk LE] Persona weapon update skipped: settings disabled ({ArtCacheUtil.DescribeArtSettings()}).");
                onComplete?.Invoke();
                return;
            }
            if (!ArtDefFilterPolicy.IsAllowed(weapon))
            {
                Log.Message($"[RimTalk LE] Persona weapon update skipped: filtered out by settings ({weapon.def?.defName ?? "unknown"}).");
                onComplete?.Invoke();
                return;
            }

            var cache = LiteratueSaveData.Current?.ArtCache;
            if (cache == null)
            {
                Log.Message("[RimTalk LE] Persona weapon update skipped: ArtCache unavailable.");
                onComplete?.Invoke();
                return;
            }

            if (!ArtKeyProvider.TryGetKey(weapon, out var key))
            {
                Log.Message("[RimTalk LE] Persona weapon update skipped: invalid art key.");
                onComplete?.Invoke();
                return;
            }

            var meta = new ArtMeta(weapon);
            var summaryRequest = MemorySummaryRequest.BuildRequest(pawn);
            if (summaryRequest == null)
            {
                Log.Message("[RimTalk LE] Persona weapon update skipped: unable to build memory summary request.");
                onComplete?.Invoke();
                return;
            }

            Log.Message($"[RimTalk LE] Persona weapon update start ({reason}): {meta.ThingLabel} ({meta.DefName}) for {pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown"}.");

            Task.Run(async () =>
            {
                try
                {
                    var summary = await MemorySummaryRequest.QueryAsync(summaryRequest);
                    if (summary == null)
                    {
                        Log.Message($"[RimTalk LE] Persona weapon update failed: memory summary null ({meta.DefName}).");
                        return;
                    }

                    var description = await PersonaWeaponRequest.QueryAsync(meta, summary, pawn, summaryRequest.Context);
                    if (description == null)
                    {
                        Log.Message($"[RimTalk LE] Persona weapon update failed: LLM returned null ({meta.DefName}).");
                        return;
                    }

                    if (cache.TryGet(key, out var existing) && existing != null && existing.IsManualOverride)
                    {
                        Log.Message($"[RimTalk LE] Persona weapon manual override preserved ({meta.DefName}).");
                        return;
                    }

                    cache.Set(key, ArtDescriptionRecord.FromGenerated(description, existing));
                    Log.Message($"[RimTalk LE] Persona weapon updated: {meta.DefName}.");
                }
                catch (Exception ex)
                {
                    Log.Message($"[RimTalk LE] Persona weapon update exception: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    onComplete?.Invoke();
                }
            });
        }
    }
}
