/*
 * Purpose:
 * - Trigger an initial book scan when a map is loaded.
 *
 * Uses:
 * - RimWorld Map lifecycle
 * - BookScanScheduler / MapBookScanner
 * - ArtScanScheduler / MapArtScanner
 *
 * Responsibilities:
 * - Ensure existing books are discovered after load.
 *
 * Design notes:
 * - One-time or guarded execution only.
 *
 * Do NOT:
 * - Do not scan repeatedly here.
 * - Do not enqueue duplicate work.
 */
using HarmonyLib;
using RimTalk_LiteratureExpansion.scanner;
using Verse;

namespace RimTalk_LiteratureExpansion.patches
{
    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    public static class Patch_MapLoaded_Scan
    {
        public static void Postfix(Map __instance)
        {
            BookScanScheduler.OnMapLoaded(__instance);
            ArtScanScheduler.OnMapLoaded(__instance);
        }
    }
}
