/*
 * File: SynopsisPromptBuilder.cs
 *
 * Purpose:
 * - Build the LLM prompt for generating a book synopsis.
 *
 * Dependencies:
 * - BookMeta
 * - Pawn context (if provided externally)
 *
 * Responsibilities:
 * - Construct a clear, bounded instruction.
 * - Request structured JSON output (BookSynopsis).
 *
 * Do NOT:
 * - Do not call AIService.
 * - Do not inject RimTalk Constant.Instruction.
 */
using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.book;

namespace RimTalk_LiteratureExpansion.synopsis
{
    public static class SynopsisPromptBuilder
    {
        public static string BuildPrompt(BookMeta meta)
        {
            int tokenTarget = GetTokenTarget();
            return
$@"You write the in-world text content of a RimWorld book.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""synopsis""

Constraints:
- Title <= {SynopsisTokenPolicy.TitleMaxChars} chars.
- Synopsis <= {SynopsisTokenPolicy.SynopsisMaxChars} chars and {SynopsisTokenPolicy.SynopsisMaxSentences} sentences.
- Invent a NEW title; do not reuse OriginalTitle text or translation-key fragments.
- ""synopsis"" is the book's actual content text (about {tokenTarget} tokens), not a summary.
- Use only the provided hints (type, benefits, skill, original description); do not add unrelated lore.
- If benefits imply training, write practical task-style instructions and examples.
- If type is CB_ChildrensBook or CB_ColoringBook: gentle, simple story/activity.
- If type is VBE_Newspaper: brief news bulletin using any provided time fields.
- If type is VBE_SkillBook or benefits imply training: practical guide tone.
- If type is Journal: first-person diary entry style.";
        }

        public static string BuildContext(BookMeta meta)
        {
            if (meta == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Book]");
            sb.AppendLine($"Type: {meta.Type}");

            if (!string.IsNullOrWhiteSpace(meta.Title))
                sb.AppendLine($"OriginalTitle: {meta.Title}");

            if (!string.IsNullOrWhiteSpace(meta.FlavorUI))
                sb.AppendLine($"OriginalBlurb: {meta.FlavorUI}");

            if (!string.IsNullOrWhiteSpace(meta.DescriptionDetailed))
                sb.AppendLine($"OriginalDescription: {meta.DescriptionDetailed}");

            var benefits = ExtractBenefitLines(meta.DescriptionDetailed);
            if (!string.IsNullOrWhiteSpace(benefits))
            {
                sb.AppendLine("[Benefits]");
                sb.AppendLine(benefits);
            }

            if (meta.Type == BookType.VBE_SkillBook && !string.IsNullOrWhiteSpace(meta.SkillDefName))
                sb.AppendLine($"Skill: {meta.SkillDefName}");

            if (meta.Type == BookType.VBE_Newspaper)
            {
                if (meta.VbeExpireTime.HasValue)
                    sb.AppendLine($"NewspaperExpireTime: {meta.VbeExpireTime.Value}");
                if (meta.VbeExpireTimeAbs.HasValue)
                    sb.AppendLine($"NewspaperExpireTimeAbs: {meta.VbeExpireTimeAbs.Value}");
            }

            return sb.ToString().TrimEnd();
        }

        private static int GetTokenTarget()
        {
            var settings = LiteratureMod.Settings;
            int target = settings?.synopsisTokenTarget ?? LiteratureSettingsDef.DefaultSynopsisTokenTarget;
            if (target < LiteratureSettingsDef.MinSynopsisTokenTarget)
                target = LiteratureSettingsDef.MinSynopsisTokenTarget;
            if (target > LiteratureSettingsDef.MaxSynopsisTokenTarget)
                target = LiteratureSettingsDef.MaxSynopsisTokenTarget;
            return target;
        }

        private static string ExtractBenefitLines(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;

            var sb = new StringBuilder();
            var lines = description.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("-", System.StringComparison.Ordinal))
                    sb.AppendLine(trimmed);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
