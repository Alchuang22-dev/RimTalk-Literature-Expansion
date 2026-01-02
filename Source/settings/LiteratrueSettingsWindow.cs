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
using System.Collections.Generic;
using System.Linq;
using RimTalk_LiteratureExpansion.settings.util;
using RimTalk_LiteratureExpansion.storage.save;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.settings
{
    public static class LiteratureSettingsWindow
    {
        private static Vector2 _settingsScroll;
        private static float _settingsViewHeight;
        private static Vector2 _questFilterScroll;
        private static Vector2 _letterFilterScroll;

        public static void Draw(Rect inRect, LiteratureSettings settings)
        {
            if (settings == null) return;
            settings.api ??= new LiteratureSettingsApi();

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(_settingsViewHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref _settingsScroll, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("RimTalkLE_Settings_AllowBooks".Translate(), ref settings.enabled);
            listing.Gap(6f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowArt".Translate(), ref settings.allowArtEdits);
            listing.Gap(6f);
            listing.CheckboxLabeled("RimTalkLE_Settings_UseRimTalkApi".Translate(), ref settings.useRimTalkApi);
            listing.Gap(12f);

            listing.Label("RimTalkLE_Settings_TextOverrides".Translate());
            listing.Gap(4f);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowIdeoDescriptionRewrite".Translate(), ref settings.allowIdeoDescriptionRewrite);
            listing.CheckboxLabeled("RimTalkLE_Settings_AllowLetterTextRewrite".Translate(), ref settings.allowLetterTextRewrite);
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

            listing.Gap(12f);
            listing.Label("RimTalkLE_Settings_QuestFilter".Translate());
            listing.Gap(4f);
            DrawQuestFilterNotes(listing);
            listing.Gap(6f);
            Rect filterRect = listing.GetRect(240f);
            DrawQuestFilter(filterRect, settings);

            listing.Gap(12f);
            listing.Label("RimTalkLE_Settings_LetterFilter".Translate());
            listing.Gap(4f);
            listing.Label("RimTalkLE_Settings_LetterFilterHelp".Translate());
            listing.Gap(6f);
            Rect letterFilterRect = listing.GetRect(240f);
            DrawLetterFilter(letterFilterRect, settings);

            listing.End();
            if (Event.current.type == EventType.Layout)
                _settingsViewHeight = listing.CurHeight + 10f;
            Widgets.EndScrollView();
        }

        private static void DrawQuestFilter(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            var defs = DefDatabase<QuestScriptDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
            {
                Widgets.Label(rect, "RimTalkLE_Settings_QuestFilterNone".Translate());
                return;
            }

            float rowHeight = LiteratureSettingsDef.RowHeight;
            Rect buttonRow = new Rect(rect.x, rect.y, rect.width, rowHeight);
            float halfWidth = (rect.width - LiteratureSettingsDef.FieldGap) / 2f;
            Rect allRect = new Rect(rect.x, rect.y, halfWidth, rowHeight);
            Rect noneRect = new Rect(allRect.xMax + LiteratureSettingsDef.FieldGap, rect.y, halfWidth, rowHeight);

            var allowSet = new HashSet<string>(settings.questRewriteAllowList ?? new List<string>());
            bool changed = false;

            if (Widgets.ButtonText(allRect, "RimTalkLE_Settings_SelectAll".Translate()))
            {
                allowSet.Clear();
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                    allowSet.Add(def.defName);
                }
                changed = true;
            }

            if (Widgets.ButtonText(noneRect, "RimTalkLE_Settings_ClearAll".Translate()))
            {
                allowSet.Clear();
                changed = true;
            }

            float gap = 4f;
            Rect scrollOut = new Rect(rect.x, rect.y + rowHeight + gap, rect.width, rect.height - rowHeight - gap);
            float viewHeight = defs.Count * (rowHeight + 2f);
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOut, ref _questFilterScroll, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (var def in defs.OrderBy(d => d.defName))
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                string label = def.label;
                string display = string.IsNullOrWhiteSpace(label) ? def.defName : $"{label} ({def.defName})";
                bool enabled = allowSet.Contains(def.defName);
                listing.CheckboxLabeled(display, ref enabled);
                if (enabled)
                    allowSet.Add(def.defName);
                else
                    allowSet.Remove(def.defName);
            }

            listing.End();
            Widgets.EndScrollView();

            if (!changed)
            {
                var existing = settings.questRewriteAllowList ?? new List<string>();
                if (existing.Count != allowSet.Count || existing.Any(x => !allowSet.Contains(x)))
                    changed = true;
            }

            if (changed)
                settings.questRewriteAllowList = allowSet.OrderBy(x => x).ToList();
        }

        private static void DrawQuestFilterNotes(Listing_Standard listing)
        {
            if (listing == null) return;
            string leftText = "RimTalkLE_Settings_QuestFilterHelp".Translate();
            string rightText = "RimTalkLE_Settings_QuestFilterExistingNote".Translate();

            Rect rect = listing.GetRect(Text.LineHeight);
            Rect leftRect = rect.LeftPart(0.7f);
            Rect rightRect = rect.RightPart(0.3f);

            Widgets.Label(leftRect, leftText);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rightRect, rightText);
            Text.Anchor = anchor;

            listing.Gap(listing.verticalSpacing);
        }

        private static void DrawLetterFilter(Rect rect, LiteratureSettings settings)
        {
            if (settings == null) return;
            var defs = DefDatabase<LetterDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
            {
                Widgets.Label(rect, "RimTalkLE_Settings_LetterFilterNone".Translate());
                return;
            }

            float rowHeight = LiteratureSettingsDef.RowHeight;
            Rect buttonRow = new Rect(rect.x, rect.y, rect.width, rowHeight);
            float halfWidth = (rect.width - LiteratureSettingsDef.FieldGap) / 2f;
            Rect allRect = new Rect(rect.x, rect.y, halfWidth, rowHeight);
            Rect noneRect = new Rect(allRect.xMax + LiteratureSettingsDef.FieldGap, rect.y, halfWidth, rowHeight);

            var allowSet = new HashSet<string>(settings.letterRewriteAllowList ?? new List<string>());
            bool changed = false;

            if (Widgets.ButtonText(allRect, "RimTalkLE_Settings_SelectAll".Translate()))
            {
                allowSet.Clear();
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                    allowSet.Add(def.defName);
                }
                changed = true;
            }

            if (Widgets.ButtonText(noneRect, "RimTalkLE_Settings_ClearAll".Translate()))
            {
                allowSet.Clear();
                changed = true;
            }

            float gap = 4f;
            Rect scrollOut = new Rect(rect.x, rect.y + rowHeight + gap, rect.width, rect.height - rowHeight - gap);
            float viewHeight = defs.Count * (rowHeight + 2f);
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOut, ref _letterFilterScroll, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (var def in defs.OrderBy(d => d.defName))
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName)) continue;
                string label = def.label;
                string display = string.IsNullOrWhiteSpace(label) ? def.defName : $"{label} ({def.defName})";
                bool enabled = allowSet.Contains(def.defName);
                listing.CheckboxLabeled(display, ref enabled);
                if (enabled)
                    allowSet.Add(def.defName);
                else
                    allowSet.Remove(def.defName);
            }

            listing.End();
            Widgets.EndScrollView();

            if (!changed)
            {
                var existing = settings.letterRewriteAllowList ?? new List<string>();
                if (existing.Count != allowSet.Count || existing.Any(x => !allowSet.Contains(x)))
                    changed = true;
            }

            if (changed)
                settings.letterRewriteAllowList = allowSet.OrderBy(x => x).ToList();
        }

    }
}
