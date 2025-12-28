/*
 * Purpose:
 * - Enqueue art for LLM description after art is initialized.
 */
using HarmonyLib;
using RimTalk_LiteratureExpansion.scanner.production;
using RimWorld;

namespace RimTalk_LiteratureExpansion.patches
{
    [HarmonyPatch(typeof(CompArt), "InitializeArtInternal")]
    public static class Patch_CompArt_Initialize
    {
        public static void Postfix(CompArt __instance)
        {
            if (__instance?.parent == null) return;
            ArtProductionTracker.NotifyGenerated(__instance.parent);
        }
    }
}
