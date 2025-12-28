/*
 * Purpose:
 * - Override CompArt description text when cached art description is available.
 */
using HarmonyLib;
using RimTalk_LiteratureExpansion.integration;
using RimWorld;
using Verse;

namespace RimTalk_LiteratureExpansion.patches
{
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.GenerateImageDescription))]
    public static class Patch_CompArt_Description
    {
        public static bool Prefix(CompArt __instance, ref TaggedString __result)
        {
            if (__instance?.parent == null) return true;
            if (!ArtCacheUtil.IsArtEditingEnabled()) return true;

            if (ArtCacheUtil.TryGetRecord(__instance.parent, out var record) &&
                !string.IsNullOrWhiteSpace(record.Text))
            {
                __result = record.Text;
                return false;
            }

            return true;
        }
    }
}
