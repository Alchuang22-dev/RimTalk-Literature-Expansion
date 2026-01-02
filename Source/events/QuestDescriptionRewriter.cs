/*
 * Purpose:
 * - Append flavor text to quest descriptions via LLM without changing quest logic.
 *
 * Uses:
 * - RimTalk AIService.Query<T> for JSON output.
 *
 * Responsibilities:
 * - Queue quest descriptions and append flavor text if it returns before a timeout.
 *
 * Design notes:
 * - Only Quest.description is modified; no letters or quest parts are altered.
 * - Late responses (past the deadline) are discarded.
 * - Flavor text must not contain numbers or known entity names.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk_LiteratureExpansion.settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events
{
    [DataContract]
    public sealed class QuestDescriptionSpec : IJsonData
    {
        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "flavor")]
        public string Flavor { get; set; }

        public string GetText()
        {
            return Flavor ?? Description ?? string.Empty;
        }
    }

    public static class QuestDescriptionRewriter
    {
        private const int TimeoutSeconds = 60;
        private const int TargetTokens = 140;
        private const string LogPrefix = "[RimTalk LE] [QuestRewrite]";

        private static readonly Dictionary<int, PendingQuestRewrite> Pending = new Dictionary<int, PendingQuestRewrite>();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();

        public static void TryQueue(Quest quest)
        {
            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled)
            {
                Log.Message($"{LogPrefix} Skip: feature disabled.");
                return;
            }
            if (quest == null)
            {
                Log.Message($"{LogPrefix} Skip: quest is null.");
                return;
            }
            if (quest.hidden || quest.hiddenInUI)
            {
                Log.Message($"{LogPrefix} Skip: quest hidden (id={quest.id}).");
                return;
            }
            if (Find.TickManager == null)
            {
                Log.Message($"{LogPrefix} Skip: TickManager unavailable (quest id={quest.id}).");
                return;
            }

            if (!IsQuestAllowed(settings, quest))
            {
                Log.Message($"{LogPrefix} Skip: quest not allowed (id={quest.id}, def={quest.root?.defName ?? "null"}).");
                return;
            }

            if (Pending.ContainsKey(quest.id))
            {
                Log.Message($"{LogPrefix} Skip: quest already queued (id={quest.id}).");
                return;
            }

            string original = GetResolvedDescription(quest);
            if (string.IsNullOrWhiteSpace(original))
            {
                Log.Message($"{LogPrefix} Skip: empty description (id={quest.id}).");
                return;
            }

            var initiator = TryGetAnyColonist();
            if (initiator == null)
            {
                Log.Message($"{LogPrefix} Skip: no colonist available (id={quest.id}).");
                return;
            }

            var entityTokens = CollectEntityTokens(quest, original);
            var record = new PendingQuestRewrite(
                quest,
                initiator,
                original,
                entityTokens,
                GenTicks.SecondsToTicks(TimeoutSeconds));
            Pending[quest.id] = record;
            Log.Message($"{LogPrefix} Queued quest (id={quest.id}, def={quest.root?.defName ?? "null"}, name={quest.name ?? "?"}, entities={record.EntityTokens.Count}).");
        }

        public static void Tick()
        {
            ProcessPendingActions();

            if (Find.TickManager == null || Pending.Count == 0) return;

            int tick = Find.TickManager.TicksGame;
            var expired = Pending.Where(kvp => tick > kvp.Value.DeadlineTick).Select(kvp => kvp.Key).ToList();
            for (int i = 0; i < expired.Count; i++)
            {
                if (Pending.TryGetValue(expired[i], out var record))
                    Log.Message($"{LogPrefix} Expired (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");
                Pending.Remove(expired[i]);
            }

            if (AIService.IsBusy()) return;

            PendingQuestRewrite next = null;
            foreach (var pending in Pending.Values)
            {
                if (pending.Requested) continue;
                if (tick > pending.DeadlineTick) continue;
                if (next == null || pending.QueuedTick < next.QueuedTick)
                    next = pending;
            }

            if (next != null)
                StartRequest(next);
        }

        private static void StartRequest(PendingQuestRewrite record)
        {
            if (record == null || record.Requested) return;
            var request = BuildRequest(record);
            if (request == null)
            {
                Pending.Remove(record.QuestId);
                Log.Message($"{LogPrefix} Abort: failed to build request (id={record.QuestId}).");
                return;
            }

            record.Requested = true;
            Log.Message($"{LogPrefix} Dispatching LLM request (id={record.QuestId}, def={record.Quest?.root?.defName ?? "null"}).");

            var task = AIService.Query<QuestDescriptionSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                if (spec == null)
                    Log.Message($"{LogPrefix} LLM returned null (id={record.QuestId}).");
                EnqueueAction(() => ApplyResult(record.QuestId, spec));
            }, TaskScheduler.Default);
        }

        private static TalkRequest BuildRequest(PendingQuestRewrite record)
        {
            if (record == null || record.Initiator == null) return null;

            var prompt = BuildPrompt(record.OriginalDescription);
            var context = BuildContext(record);

            return new TalkRequest(prompt, record.Initiator)
            {
                Context = context
            };
        }

        private static string BuildPrompt(string originalDescription)
        {
            int charLimit = Mathf.Clamp(originalDescription?.Length ?? 0, 240, 520);
            return
$@"Write 3-5 sentences of in-universe flavor to append to a quest description, written in the quest issuer's voice.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""flavor""

Constraints:
- Use the quest description as context to pick an appropriate format (plea letter, warning note, official notice), and keep a direct, sender-authored tone.
- Keep it relevant to the situation; avoid unrelated filler.
- Do NOT include any names, factions, places, rewards, requirements, or time limits.
- Do NOT include any numbers or percentages.
- Keep it generic and evocative; add atmosphere without new facts.
- Keep length <= {charLimit} characters (about {TargetTokens} tokens).
- If unsure, return an empty string.
- No markdown, no extra keys.";
        }

        private static string BuildContext(PendingQuestRewrite record)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[QuestDescription]");
            if (!string.IsNullOrWhiteSpace(record.QuestName))
                sb.AppendLine($"QuestName: {record.QuestName}");
            sb.AppendLine("OriginalDescription:");
            sb.AppendLine(record.OriginalDescription);
            return sb.ToString().TrimEnd();
        }

        private static void ApplyResult(int questId, QuestDescriptionSpec spec)
        {
            if (!Pending.TryGetValue(questId, out var record))
                return;

            Pending.Remove(questId);

            if (record == null || spec == null)
            {
                Log.Message($"{LogPrefix} Abort: missing record or spec (id={questId}).");
                return;
            }
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame > record.DeadlineTick)
            {
                Log.Message($"{LogPrefix} Abort: response late (id={questId}).");
                return;
            }

            string flavor = (spec.Flavor ?? spec.Description)?.Trim();
            if (string.IsNullOrWhiteSpace(flavor))
            {
                Log.Message($"{LogPrefix} Abort: empty rewritten text (id={questId}).");
                return;
            }
            if (ContainsAnyDigits(flavor))
            {
                Log.Message($"{LogPrefix} Abort: flavor contains numbers (id={questId}).");
                return;
            }
            if (ContainsAnyEntity(flavor, record.EntityTokens))
            {
                Log.Message($"{LogPrefix} Abort: flavor contains entity names (id={questId}).");
                return;
            }

            if (!IsQuestActive(record.Quest))
            {
                Log.Message($"{LogPrefix} Abort: quest no longer active (id={questId}).");
                return;
            }

            record.Quest.description = $"{record.OriginalDescription}\n\n{flavor}";
            Log.Message($"{LogPrefix} Applied rewrite (id={questId}, def={record.Quest?.root?.defName ?? "null"}).");
        }

        private static bool IsQuestActive(Quest quest)
        {
            if (quest == null) return false;
            if (Find.QuestManager == null) return false;
            return Find.QuestManager.QuestsListForReading.Contains(quest);
        }

        private static bool IsQuestAllowed(LiteratureSettings settings, Quest quest)
        {
            if (settings == null || quest == null) return false;
            var def = quest.root;
            if (def == null || string.IsNullOrWhiteSpace(def.defName)) return false;
            var allowList = settings.questRewriteAllowList;
            if (allowList == null || allowList.Count == 0) return false;
            return allowList.Contains(def.defName);
        }


        private static string GetResolvedDescription(Quest quest)
        {
            if (quest == null) return null;
            var description = quest.description;
            if (description.NullOrEmpty()) return null;
            return description.Resolve().Trim();
        }

        private static Pawn TryGetAnyColonist()
        {
            var maps = Find.Maps;
            if (maps == null) return null;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                var pawns = map?.mapPawns?.FreeColonistsSpawned;
                if (pawns == null || pawns.Count == 0) continue;
                return pawns[0];
            }
            return null;
        }

        private static bool ContainsAnyDigits(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                    return true;
            }
            return false;
        }

        private static bool ContainsAnyEntity(string text, List<string> entities)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (entities == null || entities.Count == 0) return false;
            for (int i = 0; i < entities.Count; i++)
            {
                var token = entities[i];
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (text.Contains(token))
                    return true;
            }
            return false;
        }

        private static void ProcessPendingActions()
        {
            lock (QueueLock)
            {
                while (PendingActions.Count > 0)
                {
                    var action = PendingActions.Dequeue();
                    action?.Invoke();
                }
            }
        }

        private static void EnqueueAction(Action action)
        {
            if (action == null) return;
            lock (QueueLock)
                PendingActions.Enqueue(action);
        }

        private sealed class PendingQuestRewrite
        {
            public int QuestId { get; }
            public Quest Quest { get; }
            public Pawn Initiator { get; }
            public string QuestName { get; }
            public string OriginalDescription { get; }
            public List<string> EntityTokens { get; }
            public int QueuedTick { get; }
            public int DeadlineTick { get; }
            public bool Requested { get; set; }

            public PendingQuestRewrite(
                Quest quest,
                Pawn initiator,
                string originalDescription,
                List<string> entityTokens,
                int timeoutTicks)
            {
                Quest = quest;
                QuestId = quest?.id ?? -1;
                Initiator = initiator;
                QuestName = quest?.name;
                OriginalDescription = originalDescription ?? string.Empty;
                EntityTokens = entityTokens ?? new List<string>();
                QueuedTick = Find.TickManager.TicksGame;
                DeadlineTick = QueuedTick + timeoutTicks;
            }
        }

        private static List<string> CollectEntityTokens(Quest quest, string description)
        {
            var tokens = new HashSet<string>();
            if (quest == null || string.IsNullOrWhiteSpace(description)) return tokens.ToList();

            void AddToken(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (!description.Contains(value)) return;
                tokens.Add(value);
            }

            AddToken(quest.name);

            foreach (var faction in quest.InvolvedFactions)
            {
                if (faction == null) continue;
                AddToken(faction.Name);
                if (faction.def != null)
                    AddToken(faction.def.label);
            }

            foreach (var target in quest.QuestLookTargets)
            {
                var pawn = target.Thing as Pawn;
                if (pawn == null) continue;
                AddToken(pawn.LabelShortCap);
                AddToken(pawn.Name?.ToStringShort);
            }

            var parts = quest.PartsListForReading;
            if (parts != null)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (part == null) continue;
                    TryAddPawnTokensFromPart(part, AddToken);
                }
            }

            return tokens.OrderByDescending(t => t.Length).ToList();
        }

        private static void TryAddPawnTokensFromPart(QuestPart part, Action<string> addToken)
        {
            var type = part.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var pawnField = type.GetField("pawn", flags);
            var pawnProp = type.GetProperty("pawn", flags);
            var pawnObj = pawnField != null ? pawnField.GetValue(part) : pawnProp?.GetValue(part, null);
            if (pawnObj is Pawn pawn)
            {
                addToken(pawn.LabelShortCap);
                addToken(pawn.Name?.ToStringShort);
            }

            var pawnsField = type.GetField("pawns", flags);
            var pawnsProp = type.GetProperty("pawns", flags);
            var pawnList = pawnsField != null ? pawnsField.GetValue(part) : pawnsProp?.GetValue(part, null);
            if (pawnList is System.Collections.IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    if (obj is Pawn listedPawn)
                    {
                        addToken(listedPawn.LabelShortCap);
                        addToken(listedPawn.Name?.ToStringShort);
                    }
                }
            }
        }
    }
}
