using System.Text;
using RimTalk.Service;
using RimTalk_LiteratureExpansion.llm;
using RimTalk_LiteratureExpansion.settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events.letters
{
    public static class FamilyLetterRequest
    {
        private const int TitleMaxChars = 48;
        private const int BodyMaxChars = 700;
        private const int TargetTokens = 220;

        public static LiteratureLlmRequest BuildRequest(
            Pawn colonist,
            Pawn relative,
            string senderRelationToRecipient,
            string recipientRelationToSender,
            string giftDefName,
            string giftLabel)
        {
            if (colonist == null || relative == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(
                colonist,
                relative,
                senderRelationToRecipient,
                recipientRelationToSender,
                giftDefName,
                giftLabel);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt()
        {
            return
$@"Write a personal letter from a colonist's relative who lives outside the colony.
Write in {RimTalkConstantShim.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""body""
- ""giftKind"" (ThingDef.defName of the gift's base item; NEVER use `MinifiedThing`. If the gift is a Building, use the building's defName — it will be delivered in minified form automatically)
- ""giftNote"" (1 sentence about the gift)

Constraints:
- title <= {TitleMaxChars} chars.
- body <= {BodyMaxChars} chars, about {TargetTokens} tokens.
- SenderName is always the writer and RecipientName is always the addressee.
- SenderRelationToRecipient describes who the sender is to the recipient.
- RecipientRelationToSender describes who the recipient is to the sender.
- Write in the sender's first-person voice and address the recipient as ""you"". Never swap either relationship.
- The gift mentioned in body/giftNote MUST match giftKind exactly.
- giftKind MUST equal GiftDefName from context.
- No markdown, no extra keys.";
        }

        private static string BuildContext(
            Pawn colonist,
            Pawn relative,
            string senderRelationToRecipient,
            string recipientRelationToSender,
            string giftDefName,
            string giftLabel)
        {
            var sb = new StringBuilder();
            var recipientMap = colonist.MapHeld ?? Find.CurrentMap;
            sb.AppendLine("[FamilyLetter]");
            sb.AppendLine($"SenderName: {relative.LabelShortCap}");
            sb.AppendLine($"RecipientName: {colonist.LabelShortCap}");
            if (!string.IsNullOrWhiteSpace(senderRelationToRecipient))
                sb.AppendLine($"SenderRelationToRecipient: {senderRelationToRecipient}");
            if (!string.IsNullOrWhiteSpace(recipientRelationToSender))
                sb.AppendLine($"RecipientRelationToSender: {recipientRelationToSender}");
            if (relative.Faction != null)
                sb.AppendLine($"SenderFaction: {relative.Faction.Name}");
            if (colonist.Faction != null)
                sb.AppendLine($"RecipientFaction: {colonist.Faction.Name}");
            if (!string.IsNullOrWhiteSpace(giftDefName))
                sb.AppendLine($"GiftDefName: {giftDefName}");
            if (!string.IsNullOrWhiteSpace(giftLabel))
                sb.AppendLine($"GiftLabel: {giftLabel}");
            sb.AppendLine($"RecipientColony: {recipientMap?.info?.parent?.LabelCap ?? "Colony"}");
            sb.AppendLine($"RecipientColonyWealth: {Mathf.RoundToInt(recipientMap?.wealthWatcher?.WealthTotal ?? 0f)}");

            var colonistProfile = PromptService.CreatePawnContext(colonist, PromptService.InfoLevel.Short);
            if (!string.IsNullOrWhiteSpace(colonistProfile))
            {
                sb.AppendLine("[RimTalkProfile:Recipient]");
                sb.AppendLine(colonistProfile);
            }

            var relativeProfile = PromptService.CreatePawnContext(relative, PromptService.InfoLevel.Short);
            if (!string.IsNullOrWhiteSpace(relativeProfile))
            {
                sb.AppendLine("[RimTalkProfile:Sender]");
                sb.AppendLine(relativeProfile);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
