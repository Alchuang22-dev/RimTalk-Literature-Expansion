using System.Text;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.settings.util;
using RimWorld;
using Verse;

namespace RimTalk_LiteratureExpansion.tv
{
    public static class TvProgramPromptBuilder
    {
        public static string BuildPrompt(Thing tvBuilding)
        {
            var settings = LiteratureMod.Settings;
            string template = BuildTemplate();
            return PromptTemplateUtil.Resolve(
                settings?.promptTvProgram,
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", LiteratureSettingsDef.TvTitleMaxChars.ToString()),
                ("CONTENT_MAX_CHARS", LiteratureSettingsDef.TvContentMaxChars.ToString()));
        }

        public static string BuildDefaultPrompt()
        {
            string template = BuildTemplate();
            return PromptTemplateUtil.ApplyTokens(
                template,
                ("LANG", RimTalkConstantShim.Lang),
                ("TITLE_MAX_CHARS", LiteratureSettingsDef.TvTitleMaxChars.ToString()),
                ("CONTENT_MAX_CHARS", LiteratureSettingsDef.TvContentMaxChars.ToString()));
        }

        private static string BuildTemplate()
        {
            return
$@"You write the in-world television program content that a character in RimWorld is watching right now.
Write in {RimTalkConstantShim.Lang}. Return JSON only.

Required JSON fields:
- ""title"" (the program name, <= {LiteratureSettingsDef.TvTitleMaxChars} chars)
- ""content"" (what is happening in the program right now, <= {LiteratureSettingsDef.TvContentMaxChars} chars)

Constraints:
- The content should reflect the game world context (colony events, faction politics, animal life, raid aftermath, trade, etc.).
- Keep the tone varied: news broadcast, documentary, drama, comedy show, survival tips, or ancient Roman-style arena — whichever fits.
- Do NOT reference real-world Earth TV shows, brands, or celebrities.
- Use the provided game context (season, weather, time of day) as creative inspiration.
- Write as if the pawn is passively absorbing this content while watching.";
        }

        public static string BuildContext(Thing tvBuilding)
        {
            if (tvBuilding == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Television]");
            sb.AppendLine($"DefName: {tvBuilding.def?.defName ?? "unknown"}");
            sb.AppendLine($"Label: {tvBuilding.LabelNoCount ?? "TV"}");

            var map = tvBuilding.Map;
            if (map != null)
            {
                var ticks = Find.TickManager.TicksAbs;
                var longLat = Find.WorldGrid.LongLatOf(map.Tile);

                sb.AppendLine($"Hour: {GenDate.HourOfDay(ticks, longLat.x)}");
                sb.AppendLine($"Season: {GenLocalDate.Season(map).Label()}");
                sb.AppendLine($"Weather: {map.weatherManager?.curWeather?.label ?? "clear"}");
                sb.AppendLine($"Temperature: {UnityEngine.Mathf.RoundToInt(map.mapTemperature.OutdoorTemp)}C");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
