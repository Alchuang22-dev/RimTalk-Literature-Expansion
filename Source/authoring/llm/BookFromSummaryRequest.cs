/*
 * Purpose:
 * - Helper object to generate book title/synopsis from a memory summary.
 *
 * Uses:
 * - IndependentBookLlmClient (independent LLM request)
 *
 * Responsibilities:
 * - Build instruction text based on MemorySummarySpec.
 *
 * Design notes:
 * - This represents the SECOND stage of the authoring pipeline.
 *
 * Do NOT:
 * - Do not access pawn context directly.
 * - Do not perform book classification or UI updates.
 */
using System.Text;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.synopsis;
using RimTalk_LiteratureExpansion.synopsis.llm;
using Verse;

namespace RimTalk_LiteratureExpansion.authoring.llm
{
    public static class BookFromSummaryRequest
    {
        public static TalkRequest BuildRequest(BookMeta meta, MemorySummarySpec summary, Pawn author, string baseContext = null)
        {
            if (summary == null || author == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(meta, summary, baseContext);

            return new TalkRequest(prompt, author)
            {
                Context = context
            };
        }

        public static Task<BookTitleSpec> QueryAsync(
            BookMeta meta,
            MemorySummarySpec summary,
            Pawn author,
            string baseContext = null)
        {
            var request = BuildRequest(meta, summary, author, baseContext);
            if (request == null) return Task.FromResult<BookTitleSpec>(null);
            return IndependentBookLlmClient.QueryJsonAsync<BookTitleSpec>(request);
        }

        private static string BuildPrompt()
        {
            int tokenTarget = GetTokenTarget();
            int storyTarget = tokenTarget + LiteratureSettingsDef.StoryTokenBonus;
            return
$@"Write in {RimTalk.Data.Constant.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""synopsis""

Constraints:
- Title <= {SynopsisTokenPolicy.TitleMaxChars} chars.
- Synopsis <= {SynopsisTokenPolicy.SynopsisMaxChars} chars and {SynopsisTokenPolicy.SynopsisMaxSentences} sentences.
- Invent a NEW title; do not reuse OriginalTitle text.
- ""synopsis"" is the book's actual content text (about {tokenTarget} tokens), not a summary.
- If the memory summary reads like a story, you may extend to ~{storyTarget} tokens.
- Preserve proper names. Do not mention that this is a summary.";
        }

        private static string BuildContext(BookMeta meta, MemorySummarySpec summary, string baseContext)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(baseContext))
                sb.AppendLine(baseContext.TrimEnd());

            sb.AppendLine("[MemorySummary]");
            sb.AppendLine(summary.Summary ?? string.Empty);

            if (summary.Keywords != null && summary.Keywords.Length > 0)
                sb.AppendLine("Keywords: " + string.Join(", ", summary.Keywords));

            if (!string.IsNullOrWhiteSpace(summary.Tone))
                sb.AppendLine("Tone: " + summary.Tone);

            if (meta != null)
            {
                sb.AppendLine("[Book]");
                sb.AppendLine($"Type: {meta.Type}");
                if (!string.IsNullOrWhiteSpace(meta.Title))
                    sb.AppendLine($"OriginalTitle: {meta.Title}");
                if (!string.IsNullOrWhiteSpace(meta.DescriptionDetailed))
                    sb.AppendLine($"OriginalDescription: {meta.DescriptionDetailed}");
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
    }
}
