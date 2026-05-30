/*
 * Purpose:
 * - Build the LLM request for advertisement quests.
 *
 * Uses:
 * - RimTalk TalkRequest
 *
 * Responsibilities:
 * - Provide a concise prompt and structured context for LLM output.
 */
using System.Collections.Generic;
using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.settings.util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events.quests
{
    public static class AdvertisementQuestRequest
    {
        private const int TitleMaxChars = 56;
        private const int BodyMaxChars = 900;
        private const int TargetTokens = 220;
        private const int DefaultOfferDays = 3;
        private const int DefaultDeliveryDays = 3;

        public static TalkRequest BuildRequest(
            Pawn initiator,
            Faction faction,
            Settlement settlement,
            Map map,
            IReadOnlyList<QuestOfferOption> options,
            int offerDays,
            int deliveryDays)
        {
            if (initiator == null || faction == null || settlement == null || options == null || options.Count == 0)
                return null;

            var prompt = BuildPrompt(offerDays, deliveryDays);
            var context = BuildContext(faction, settlement, map, options, offerDays, deliveryDays);

            return new TalkRequest(prompt, initiator)
            {
                Context = context
            };
        }

        private static string BuildPrompt(int offerDays, int deliveryDays)
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(offerDays, deliveryDays);
            return PromptTemplateUtil.Resolve(
                settings?.promptQuestAdvert,
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", offerDays.ToString()),
                ("DELIVERY_DAYS", deliveryDays.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate(DefaultOfferDays, DefaultDeliveryDays);
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", DefaultOfferDays.ToString()),
                ("DELIVERY_DAYS", DefaultDeliveryDays.ToString()));
        }

        private static string BuildTemplate(int offerDays, int deliveryDays)
        {
            return
$@"Write a merchant advertisement quest description from the issuing faction's point of view.
Write in {RimTalkConstantShim.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""description""

Constraints:
- title <= {TitleMaxChars} chars.
- description <= {BodyMaxChars} chars, about {TargetTokens} tokens.
- Use the issuer's voice and keep it grounded; the description should read like a posted task request, not generic flavor prose.
- Include a short background reason for why the faction is making this offer.
- Include all three options with their silver amounts and item names exactly as provided in QuestData.
- Mention payment can be delivered by caravan or transport pods.
- Mention the offer window ({offerDays} days) and delivery window ({deliveryDays} days) without changing numbers.
- Do not add new items, options, or numbers.
- No markdown, no extra keys.";
        }

        private static string BuildContext(
            Faction faction,
            Settlement settlement,
            Map map,
            IReadOnlyList<QuestOfferOption> options,
            int offerDays,
            int deliveryDays)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AdvertQuest]");
            sb.AppendLine($"Faction: {faction.Name}");
            sb.AppendLine($"Settlement: {settlement.LabelCap}");
            sb.AppendLine($"OfferDays: {offerDays}");
            sb.AppendLine($"DeliveryDays: {deliveryDays}");
            if (map != null)
            {
                sb.AppendLine($"Colony: {map.info?.parent?.LabelCap ?? "Colony"}");
                sb.AppendLine($"Colonists: {map.mapPawns?.FreeColonistsSpawned?.Count ?? 0}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"Wealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            sb.AppendLine("Options:");
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null) continue;
                sb.AppendLine($"- {option.Key}: pay {option.SilverCost} silver -> {option.ItemsLabel}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
