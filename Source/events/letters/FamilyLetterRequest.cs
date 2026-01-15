using System.Text;
using RimTalk.Data;
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

        public static TalkRequest BuildRequest(
            Pawn initiator,
            Pawn colonist,
            Pawn relative,
            string relationLabel,
            string giftDefName,
            string giftLabel)
        {
            if (initiator == null || colonist == null || relative == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(colonist, relative, relationLabel, giftDefName, giftLabel);

            return new TalkRequest(prompt, initiator)
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
- The writer is the Relative; use the provided Relation and names without reversing roles.
- The gift mentioned in body/giftNote MUST match giftKind exactly.
- giftKind MUST equal GiftDefName from context.
- No markdown, no extra keys.";
        }

        private static string BuildContext(
            Pawn colonist,
            Pawn relative,
            string relationLabel,
            string giftDefName,
            string giftLabel)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FamilyLetter]");
            sb.AppendLine($"Recipient(Colonist): {colonist.LabelShortCap}");
            sb.AppendLine($"Writer(Relative): {relative.LabelShortCap}");
            if (!string.IsNullOrWhiteSpace(relationLabel))
                sb.AppendLine($"HowRelativeCallsColonist: {relationLabel}");
            if (relative.Faction != null)
                sb.AppendLine($"RelativeFaction: {relative.Faction.Name}");
            if (!string.IsNullOrWhiteSpace(giftDefName))
                sb.AppendLine($"GiftDefName: {giftDefName}");
            if (!string.IsNullOrWhiteSpace(giftLabel))
                sb.AppendLine($"GiftLabel: {giftLabel}");
            sb.AppendLine($"ColonyName: {Find.CurrentMap?.info?.parent?.LabelCap ?? "Colony"}");
            sb.AppendLine($"ColonyWealth: {Mathf.RoundToInt(Find.CurrentMap?.wealthWatcher?.WealthTotal ?? 0f)}");
            return sb.ToString().TrimEnd();
        }
    }
}
