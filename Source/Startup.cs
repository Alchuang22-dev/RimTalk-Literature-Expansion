using HarmonyLib;
using Verse;

namespace RimTalk_LiteratureExpansion
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            var harmony = new Harmony("RimTalk_LiteratureExpansion");
            harmony.PatchAll();
        }
    }
}
