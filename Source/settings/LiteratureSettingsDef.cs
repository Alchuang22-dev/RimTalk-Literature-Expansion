/*
 * Purpose:
 * - Centralize default values and constants for settings UI.
 *
 * Examples:
 * - defaultBaseUrl = "https://api.openai.com/v1"
 * - defaultModel = "gpt-4o-mini" (or leave blank)
 * - max input lengths
 *
 * Do NOT:
 * - Do not import RimTalk Constant.Instruction or override it.
 */
namespace RimTalk_LiteratureExpansion.settings
{
    public static class LiteratureSettingsDef
    {
        public const string DefaultBaseUrl = "";
        public const string DefaultModel = "";

        public const int MaxBaseUrlLength = 200;
        public const int MaxApiKeyLength = 200;
        public const int MaxModelLength = 120;

        public const int DefaultSynopsisTokenTarget = 340;
        public const int MinSynopsisTokenTarget = 120;
        public const int MaxSynopsisTokenTarget = 800;
        public const int StoryTokenBonus = 220;

        public const float RowHeight = 24f;
        public const float FieldGap = 6f;
    }
}
