/*
 * Purpose:
 * - Periodically trigger book scanning (e.g. once per in-game day).
 *
 * Uses:
 * - RimWorld tick system
 * - BookScanScheduler
 *
 * Responsibilities:
 * - Delegate timing decisions to scheduler.
 *
 * Design notes:
 * - Patch should be extremely lightweight.
 *
 * Do NOT:
 * - Do not scan maps directly.
 * - Do not perform LLM calls here.
 */
using HarmonyLib;
using RimTalk_LiteratureExpansion.scanner;
using Verse;

namespace RimTalk_LiteratureExpansion.patches
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class Patch_Tick_DailyScan
    {
        public static void Postfix()
        {
            BookScanScheduler.TryDailyScan();
        }
    }
}
