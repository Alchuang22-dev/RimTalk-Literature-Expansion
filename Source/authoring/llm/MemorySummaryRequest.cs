/*
 * Purpose:
 * - Helper object to construct a TalkRequest or Query<T> for memory summarization.
 *
 * Uses:
 * - RimTalk TalkRequest
 * - PromptService.BuildContext
 * - IndependentBookLlmClient (independent LLM request)
 *
 * Responsibilities:
 * - Provide prompt instructions that request MemorySummarySpec JSON output.
 *
 * Design notes:
 * - BuildRequest must run on the main thread if it calls PromptService.
 * - QueryAsync sends via the independent LLM client (no RimTalk queue).
 *
 * Do NOT:
 * - Do not directly invoke AIService.
 * - Do not inject Constant.Instruction.
 */
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk_LiteratureExpansion.synopsis;
using RimTalk_LiteratureExpansion.synopsis.llm;
using Verse;

namespace RimTalk_LiteratureExpansion.authoring.llm
{
    public static class MemorySummaryRequest
    {
        private const string TemplateResourceName =
            "RimTalk_LiteratureExpansion.promptoverride.templates.Prompt_MemorySummary.txt";

        public static TalkRequest BuildRequest(Pawn pawn)
        {
            if (pawn == null) return null;

            var context = PromptService.BuildContext(new List<Pawn> { pawn });
            var prompt = BuildPrompt();

            return new TalkRequest(prompt, pawn)
            {
                Context = context
            };
        }

        public static Task<MemorySummarySpec> QueryAsync(TalkRequest request)
        {
            if (request == null) return Task.FromResult<MemorySummarySpec>(null);
            return IndependentBookLlmClient.QueryJsonAsync<MemorySummarySpec>(request);
        }

        private static string BuildPrompt()
        {
            var template = LoadTemplate();
            template = template.Replace("{{LANG}}", RimTalk.Data.Constant.Lang);
            template = template.Replace("{{SUMMARY_MAX_CHARS}}", SynopsisTokenPolicy.PromptSynopsisMaxChars.ToString());
            template = template.Replace("{{SUMMARY_MAX_SENTENCES}}", SynopsisTokenPolicy.SynopsisMaxSentences.ToString());
            return template;
        }

        private static string LoadTemplate()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(TemplateResourceName);
            if (stream == null) return DefaultTemplate();

            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return string.IsNullOrWhiteSpace(text) ? DefaultTemplate() : text.Trim();
        }

        private static string DefaultTemplate()
        {
            return
                "Summarize the pawn's recent memories based on the provided context.\n" +
                "Write in {{LANG}}. Return JSON only.\n\n" +
                "Required JSON fields:\n" +
                "- \"summary\": <= {{SUMMARY_MAX_CHARS}} chars and {{SUMMARY_MAX_SENTENCES}} sentences\n" +
                "- \"keywords\": 3-6 short keywords\n" +
                "- \"tone\": 1-2 words\n\n" +
                "Use only the provided context; do not invent new events.";
        }
    }
}
