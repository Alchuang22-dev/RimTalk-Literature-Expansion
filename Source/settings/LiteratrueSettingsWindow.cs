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

            listing.CheckboxLabeled("Enable literature expansion", ref settings.enabled);
            listing.Gap(6f);
            listing.CheckboxLabeled("Use same API as RimTalk", ref settings.useRimTalkApi);
            listing.Gap(12f);

            if (!settings.useRimTalkApi)
            {
                listing.Label("Standalone API");
                listing.Gap(4f);

                settings.api.baseUrl = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "Base URL",
                    settings.api.baseUrl,
                    LiteratureSettingsDef.MaxBaseUrlLength);

                settings.api.apiKey = SettingsUIHelpers.PasswordFieldLabeled(
                    listing,
                    "API Key",
                    settings.api.apiKey,
                    LiteratureSettingsDef.MaxApiKeyLength);

                settings.api.model = SettingsUIHelpers.TextFieldLabeled(
                    listing,
                    "Model",
                    settings.api.model,
                    LiteratureSettingsDef.MaxModelLength);

                var errors = settings.api.GetValidationErrors();
                if (errors != null && errors.Count > 0)
                {
                    SettingsUIHelpers.DrawValidationMessages(
                        listing,
                        "Validation",
                        errors.ToArray());
                }
            }

            listing.Gap(12f);
            listing.Label("Debug");
            listing.Gap(4f);
            settings.synopsisTokenTarget = SettingsUIHelpers.IntFieldLabeled(
                listing,
                "Book content target tokens",
                settings.synopsisTokenTarget,
                LiteratureSettingsDef.MinSynopsisTokenTarget,
                LiteratureSettingsDef.MaxSynopsisTokenTarget);

            Rect buttonRect = listing.GetRect(LiteratureSettingsDef.RowHeight);
            if (Widgets.ButtonText(buttonRect, "Clear cached book synopses"))
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
