using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.synopsis;

namespace RimTalk_LiteratureExpansion.art
{
    public static class ArtPromptBuilder
    {
        public static string BuildPrompt(ArtMeta meta)
        {
            int tokenTarget = GetTokenTarget();
            return
$@"You write in-world art descriptions for RimWorld objects.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""text""

Constraints:
- Title <= {SynopsisTokenPolicy.TitleMaxChars} chars.
- Text <= {SynopsisTokenPolicy.SynopsisMaxChars} chars.
- ""text"" is the full art description (about {tokenTarget} tokens), not a summary.
- Use only provided hints (original title, author, original description, quality).
- Do not invent unrelated lore. Keep it vivid and concrete.";
        }

        public static string BuildContext(ArtMeta meta)
        {
            if (meta == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Artwork]");
            sb.AppendLine($"ThingLabel: {meta.ThingLabel}");
            sb.AppendLine($"DefName: {meta.DefName}");

            if (meta.Quality.HasValue)
                sb.AppendLine($"Quality: {meta.Quality.Value}");

            if (!string.IsNullOrWhiteSpace(meta.OriginalTitle))
                sb.AppendLine($"OriginalTitle: {meta.OriginalTitle}");

            if (!string.IsNullOrWhiteSpace(meta.AuthorName))
                sb.AppendLine($"Author: {meta.AuthorName}");

            if (!string.IsNullOrWhiteSpace(meta.OriginalDescription))
                sb.AppendLine($"OriginalDescription: {meta.OriginalDescription}");

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
