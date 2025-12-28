/*
 * Purpose:
 * - Persist core feature toggles for Literature Expansion.
 *
 * Uses:
 * - Verse.ModSettings / IExposable
 *
 * Fields:
 * - enabled: master switch
 * - useRimTalkApi: if true, reuse RimTalk Settings_Api / ApiConfig at runtime
 *
 * Responsibilities:
 * - ExposeData() for save/load of settings.
 *
 * Do NOT:
 * - Do not store secrets here if you intend to share settings files publicly.
 *   (If you must store API key, keep it in LiteratureSettingsApi with clear UI warnings.)
 */
using Verse;

namespace RimTalk_LiteratureExpansion.settings
{
    public sealed class LiteratureSettings : ModSettings
    {
        public bool enabled = true;
        public bool useRimTalkApi = true;
        public LiteratureSettingsApi api = new LiteratureSettingsApi();
        public int synopsisTokenTarget = LiteratureSettingsDef.DefaultSynopsisTokenTarget;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref useRimTalkApi, "useRimTalkApi", true);
            Scribe_Deep.Look(ref api, "api");
            Scribe_Values.Look(ref synopsisTokenTarget, "synopsisTokenTarget", LiteratureSettingsDef.DefaultSynopsisTokenTarget);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && api == null)
                api = new LiteratureSettingsApi();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && synopsisTokenTarget <= 0)
                synopsisTokenTarget = LiteratureSettingsDef.DefaultSynopsisTokenTarget;
        }
    }
}
