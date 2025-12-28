/*
 * Purpose:
 * - Draw the settings UI for Literature Expansion.
 *
 * Uses:
 * - Verse.Widgets / Listing_Standard
 * - LiteratureSettings + LiteratureSettingsApi
 *
 * UI requirements:
 * - Checkbox: Enable Literature Expansion
 * - Checkbox: Use same API as RimTalk
 * - If not using RimTalk API:
 *   - Text field: Base URL
 *   - Text field (masked if feasible): API Key
 *   - Text field: Model
 *   - Optional: "Test" button (LOCAL validation only)
 *
 * Design notes:
 * - Keep UI minimal and stable; avoid complex layout.
 * - Put all strings behind translation keys if you already have a translation workflow.
 *
 * Do NOT:
 * - Do not call LLM here.
 * - Do not modify RimTalk settings.
 */
using RimTalk_LiteratureExpansion.settings.util;
using RimTalk_LiteratureExpansion.storage.save;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.settings
{
    public static class LiteratureSettingsWindow
    {
        public static void Draw(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;
            settings.api ??= new LiteratureSettingsApi();

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("RimTalkLE_Settings_AllowBooks".Translate(), ref settings.enabled);
            listing.Gap(6f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArt".Translate(), ref settings.allowArtEdits);
            listing.Gap(6f);
            listing.CheckboxLabeled("RimTalkLE_Settings_UseRimTalkApi".Translate(), ref settings.useRimTalkApi);
            listing.Gap(12f);

            if (!settings.useRimTalkApi)
            {
                listing.Label("RimTalkLE_Settings_StandaloneApi".Translate());
                listing.Gap(4f);

                settings.api.baseUrl = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_BaseUrl".Translate(),
                    settings.api.baseUrl,
                    LiteratureSettingsDef.MaxBaseUrlLength);

                settings.api.apiKey = SettingsUIHelpers.PasswordFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_ApiKey".Translate(),
                    settings.api.apiKey,
                    LiteratureSettingsDef.MaxApiKeyLength);

                settings.api.model = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "RimTalkLE_Settings_Model".Translate(),
                    settings.api.model,
                    LiteratureSettingsDef.MaxModelLength);

                var errors = settings.api.GetValidationErrors();
                if (errors != null && errors.Count > 0)
                {
                    SettingsUIHelpers.DrawValidationMessages(
                        listing,
                        "RimTalkLE_Settings_Validation".Translate(),
                        errors.ToArray());
                }
            }

            listing.Gap(12f);
            listing.Label("RimTalkLE_Settings_Debug".Translate());
            listing.Gap(4f);
            settings.synopsisTokenTarget = SettingsUIHelpers.IntFieldLabeled(
                listing,
                "RimTalkLE_Settings_TokenTarget".Translate(),
                settings.synopsisTokenTarget,
                LiteratureSettingsDef.MinSynopsisTokenTarget,
                LiteratureSettingsDef.MaxSynopsisTokenTarget);

            Rect buttonRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(buttonRect, "RimTalkLE_Settings_ClearBookCache".Translate()))
            {
                var cache = LiteratueSaveData.Current?.SynopsisCache;
                if (cache == null)
                {
                    Log.Warning("[RimTalk LE] No active world data; cannot clear book cache.");
                }
                else
                {
                    int cleared = cache.Clear();
                    Log.Message($"[RimTalk LE] Cleared {cleared} cached book synopses.");
                }
            }

            listing.End();
        }
    }
}
