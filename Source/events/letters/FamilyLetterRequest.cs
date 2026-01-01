using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimWorld;
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
            string relationLabel)
        {
            if (initiator == null || colonist == null || relative == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(colonist, relative, relationLabel);

            return new TalkRequest(prompt, initiator)
            {
                Context = context
            };
        }

        private static string BuildPrompt()
        {
            return
$@"Write a personal letter from a colonist's relative who lives outside the colony.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""body""
- ""giftKind"" (one of: food, medicine, textile, materials, components)
- ""giftNote"" (1 sentence about the gift)

Constraints:
- title <= {TitleMaxChars} chars.
- body <= {BodyMaxChars} chars, about {TargetTokens} tokens.
- The letter should mention the gift and match the giftKind.
- No markdown, no extra keys.";
        }

        private static string BuildContext(Pawn colonist, Pawn relative, string relationLabel)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FamilyLetter]");
            sb.AppendLine($"Colonist: {colonist.LabelShortCap}");
            if (!string.IsNullOrWhiteSpace(relationLabel))
                sb.AppendLine($"Relation: {relationLabel}");
            sb.AppendLine($"Relative: {relative.LabelShortCap}");
            if (relative.Faction != null)
                sb.AppendLine($"RelativeFaction: {relative.Faction.Name}");
            return sb.ToString().TrimEnd();
        }
    }
}
