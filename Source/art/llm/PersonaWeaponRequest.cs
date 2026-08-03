/*
 * Purpose:
 * - Generate persona weapon name and description from a pawn memory summary.
 *
 * Uses:
 * - IndependentBookLlmClient (independent LLM request)
 */
using System.Text;
using System.Threading.Tasks;
using RimTalk_LiteratureExpansion.authoring;
using RimTalk_LiteratureExpansion.art.model;
using RimTalk_LiteratureExpansion.llm;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.settings.util;
using RimTalk_LiteratureExpansion.synopsis;
using RimTalk_LiteratureExpansion.synopsis.llm;
using Verse;

namespace RimTalk_LiteratureExpansion.art.llm
{
    public static class PersonaWeaponRequest
    {
        public static Task<ArtDescription> QueryAsync(
            ArtMeta meta,
            MemorySummarySpec summary,
            Pawn pawn,
            string baseContext = null)
        {
            var request = BuildRequest(meta, summary, pawn, baseContext);
            if (request == null) return Task.FromResult<ArtDescription>(null);
            return IndependentBookLlmClient.QueryJsonAsync<ArtDescription>(request);
        }

        private static LiteratureLlmRequest BuildRequest(
            ArtMeta meta,
            MemorySummarySpec summary,
            Pawn pawn,
            string baseContext)
        {
            if (summary == null || pawn == null || meta == null) return null;

            var prompt = BuildPrompt();
            var context = BuildContext(meta, summary, baseContext);

            return new LiteratureLlmRequest(prompt)
            {
                Context = context
            };
        }

        private static string BuildPrompt()
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate();
            return PromptTemplateUtil.Resolve(
                settings?.promptPersonaWeapon,
                template,
                ("LANG", RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate();
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", SynopsisTokenPolicy.TitleMaxChars.ToString()),
                ("SYNOPSIS_MAX_CHARS", SynopsisTokenPolicy.SynopsisMaxChars.ToString()));
        }

        private static string BuildTemplate()
        {
            return
$@"You are naming a persona weapon and writing its in-world description.
Write in {RimTalk_LiteratureExpansion.RimTalkConstantShim.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""text""

Constraints:
- Title <= {SynopsisTokenPolicy.TitleMaxChars} chars.
- Text <= {SynopsisTokenPolicy.SynopsisMaxChars} chars.
- ""text"" is a vivid description grounded in the pawn's memories.
- Keep it consistent with RimWorld tone; no meta commentary.";
        }

        private static string BuildContext(ArtMeta meta, MemorySummarySpec summary, string baseContext)
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

            sb.AppendLine("[PersonaWeapon]");
            sb.AppendLine($"ThingLabel: {meta.ThingLabel}");
            sb.AppendLine($"DefName: {meta.DefName}");
            if (meta.Quality.HasValue)
                sb.AppendLine($"Quality: {meta.Quality.Value}");
            if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
                sb.AppendLine($"OriginalTitle: {meta.OriginalTitle}");
            if (!string.IsNullOrWhiteSpace(meta.OriginalDescription))
                sb.AppendLine($"OriginalDescription: {meta.OriginalDescription}");

            return sb.ToString().TrimEnd();
        }
    }
}
