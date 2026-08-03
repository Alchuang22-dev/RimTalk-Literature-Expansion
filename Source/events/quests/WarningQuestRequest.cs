/*
 * Purpose:
 * - Build the LLM request for warning quests.
 *
 * Uses:
 * - Literature Expansion standalone LLM request
 *
 * Responsibilities:
 * - Provide a concise prompt and structured context for LLM output.
 */
using System.Text;
using RimTalk_LiteratureExpansion.llm;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.settings.util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events.quests
{
    public static class WarningQuestRequest
    {
        private const int TitleMaxChars = 56;
        private const int BodyMaxChars = 900;
        private const int TargetTokens = 220;
        private const int DefaultOfferDays = 3;
        private const int DefaultDeliveryDays = 3;

        public static LiteratureLlmRequest BuildRequest(
            Faction faction,
            Settlement settlement,
            Map map,
            int silverDemand,
            int offerDays,
            int deliveryDays)
        {
            if (faction == null || settlement == null) return null;

            var prompt = BuildPrompt(offerDays, deliveryDays);
            var context = BuildContext(faction, settlement, map, silverDemand, offerDays, deliveryDays);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt(int offerDays, int deliveryDays)
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate(offerDays, deliveryDays);
            var prompt = PromptTemplateUtil.Resolve(
                settings?.promptQuestWarning,
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", offerDays.ToString()),
                ("DELIVERY_DAYS", deliveryDays.ToString()));
            return ApplyIssuerBoundary(prompt);
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate(DefaultOfferDays, DefaultDeliveryDays);
            var prompt = PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", TitleMaxChars.ToString()),
                ("BODY_MAX_CHARS", BodyMaxChars.ToString()),
                ("TARGET_TOKENS", TargetTokens.ToString()),
                ("OFFER_DAYS", DefaultOfferDays.ToString()),
                ("DELIVERY_DAYS", DefaultDeliveryDays.ToString()));
            return ApplyIssuerBoundary(prompt);
        }

        private static string ApplyIssuerBoundary(string prompt)
        {
            const string marker = "[TaskBoundary:QuestIssuer]";
            if (string.IsNullOrWhiteSpace(prompt)) return marker;
            if (prompt.Contains(marker)) return prompt;
            return
$@"{prompt.TrimEnd()}

{marker}
- IssuerFaction is the speaker.
- RecipientFaction and RecipientColony are the addressee.
- Never swap issuer and recipient identities.";
        }

        private static string BuildTemplate(int offerDays, int deliveryDays)
        {
            return
$@"Write a hostile warning quest description from the issuing faction's point of view.
Write in {RimTalkConstantShim.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""description""

Constraints:
- title <= {TitleMaxChars} chars.
- description <= {BodyMaxChars} chars, about {TargetTokens} tokens.
- IssuerFaction is the speaker. RecipientFaction and RecipientColony receive the demand.
- Write in IssuerFaction's first-person plural voice. Never speak as RecipientFaction.
- Use the issuer's voice and keep it tense; the description should read like a threat-backed task request, not generic flavor prose.
- Include a short background or grievance explaining why the faction is demanding payment.
- Include the demanded silver amount exactly as provided in QuestData.
- Mention payment can be delivered by caravan or transport pods.
- Mention the offer window ({offerDays} days) and delivery window ({deliveryDays} days) without changing numbers.
- Mention a raid will occur 1-2 days after the deadline if unpaid.
- Do not add new numbers, factions, or rewards.
- No markdown, no extra keys.";
        }

        private static string BuildContext(
            Faction faction,
            Settlement settlement,
            Map map,
            int silverDemand,
            int offerDays,
            int deliveryDays)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[WarningQuest]");
            sb.AppendLine($"IssuerFaction: {faction.Name}");
            sb.AppendLine($"IssuerSettlement: {settlement.LabelCap}");
            if (Faction.OfPlayer != null)
                sb.AppendLine($"RecipientFaction: {Faction.OfPlayer.Name}");
            sb.AppendLine($"SilverDemand: {silverDemand}");
            sb.AppendLine($"OfferDays: {offerDays}");
            sb.AppendLine($"DeliveryDays: {deliveryDays}");
            if (map != null)
            {
                sb.AppendLine($"RecipientColony: {map.info?.parent?.LabelCap ?? "Colony"}");
                sb.AppendLine($"RecipientColonists: {map.mapPawns?.FreeColonistsSpawned?.Count ?? 0}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"RecipientWealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
