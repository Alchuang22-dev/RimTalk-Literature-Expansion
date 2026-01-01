using System.Linq;
using System.Text;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events.letters
{
    public static class AllyDiplomacyLetterRequest
    {
        private const int TitleMaxChars = 48;
        private const int BodyMaxChars = 600;
        private const int TargetTokens = 180;

        public static TalkRequest BuildRequest(Pawn initiator, Faction faction, Map map, string colonyName, int goodwillDelta)
        {
            if (initiator == null || faction == null) return null;

            var prompt = BuildPrompt(goodwillDelta);
            var context = BuildContext(faction, map, colonyName, goodwillDelta);

            return new TalkRequest(prompt, initiator)
            {
                Context = context
            };
        }

        private static string BuildPrompt(int goodwillDelta)
        {
            return
$@"Write a friendly diplomatic letter from an allied faction to the player colony.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""title""
- ""body""

Constraints:
- title <= {TitleMaxChars} chars.
- body <= {BodyMaxChars} chars, about {TargetTokens} tokens.
- Mention the alliance and that relations improve by {goodwillDelta}.
- Use at least one concrete detail from the provided colony or ideology context when available.
- Keep the tone coherent and grounded; avoid surreal or random content.
- No markdown, no extra keys.";
        }

        private static string BuildContext(Faction faction, Map map, string colonyName, int goodwillDelta)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AllyDiplomacy]");
            sb.AppendLine($"Faction: {faction.Name}");
            if (!string.IsNullOrWhiteSpace(colonyName))
                sb.AppendLine($"Colony: {colonyName}");
            if (faction.leader != null)
                sb.AppendLine($"Leader: {faction.leader.LabelShortCap}");
            sb.AppendLine($"GoodwillChange: +{goodwillDelta}");

            if (map != null)
            {
                int colonistCount = map.mapPawns?.FreeColonistsSpawned?.Count ?? 0;
                sb.AppendLine($"Colonists: {colonistCount}");
                if (map.wealthWatcher != null)
                    sb.AppendLine($"ColonyWealth: {Mathf.RoundToInt(map.wealthWatcher.WealthTotal)}");
            }

            if (ModsConfig.IdeologyActive)
            {
                var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (ideo != null)
                {
                    sb.AppendLine($"Ideology: {ideo.name}");
                    var roles = ideo.RolesListForReading;
                    if (roles != null && roles.Count > 0)
                    {
                        sb.AppendLine("Roles:");
                        for (int i = 0; i < roles.Count; i++)
                        {
                            var role = roles[i];
                            if (role == null) continue;
                            var assigned = role.ChosenPawns();
                            var assignedLine = assigned != null ? string.Join(", ", assigned.Select(p => p.LabelShortCap)) : string.Empty;
                            if (!string.IsNullOrWhiteSpace(assignedLine))
                                sb.AppendLine($"- {role.LabelCap}: {assignedLine}");
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
