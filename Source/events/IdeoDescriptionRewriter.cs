/*
 * Purpose:
 * - Append flavor text to ideology descriptions via LLM without changing facts.
 *
 * Uses:
 * - IndependentBookLlmClient.QueryJsonAsync<T> for JSON output.
 *
 * Responsibilities:
 * - Queue ideology descriptions and append flavor if it returns before a timeout.
 *
 * Design notes:
 * - Only Ideo.description is modified.
 * - Flavor text must not contain numbers or known entity names.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.synopsis.llm;
using RimTalk_LiteratureExpansion.storage.save;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events
{
    [DataContract]
    public sealed class IdeoFlavorSpec : IJsonData
    {
        [DataMember(Name = "flavor")]
        public string Flavor { get; set; }

        public string GetText()
        {
            return Flavor ?? string.Empty;
        }
    }

    public static class IdeoDescriptionRewriter
    {
        private const int TimeoutSeconds = 60;
        private const int TargetTokens = 140;
        private const int ScanIntervalTicks = GenDate.TicksPerHour;
        private const string LogPrefix = "[RimTalk LE] [IdeoRewrite]";

        private static readonly Dictionary<int, PendingIdeoRewrite> Pending = new Dictionary<int, PendingIdeoRewrite>();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();
        private static readonly HashSet<int> Processed = new HashSet<int>();

        private static int _nextScanTick;

        public static void Tick()
        {
            ProcessPendingActions();

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowIdeoDescriptionRewrite) return;
            if (Find.TickManager == null) return;

            int tick = Find.TickManager.TicksGame;
            if (tick >= _nextScanTick)
            {
                _nextScanTick = tick + ScanIntervalTicks;
                TryQueueAll();
            }

            if (Pending.Count == 0) return;

            var expired = Pending.Where(kvp => tick > kvp.Value.DeadlineTick).Select(kvp => kvp.Key).ToList();
            for (int i = 0; i < expired.Count; i++)
            {
                Pending.Remove(expired[i]);
            }

            PendingIdeoRewrite next = null;
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

        public static void TryQueue(Ideo ideo)
        {
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowIdeoDescriptionRewrite) return;
            if (ideo == null || ideo.hidden) return;
            if (Find.TickManager == null) return;

            if (Processed.Contains(ideo.id)) return;
            if (Pending.ContainsKey(ideo.id)) return;

            string original = ideo.description?.Trim();
            if (string.IsNullOrWhiteSpace(original)) return;

            var initiator = TryGetAnyColonist();
            if (initiator == null) return;

            var entityTokens = CollectEntityTokens(ideo, original);
            if (IsAlreadyProcessed(ideo.id))
            {
                Processed.Add(ideo.id);
                Log.Message($"{LogPrefix} Skip: already processed (id={ideo.id}).");
                return;
            }
            var record = new PendingIdeoRewrite(ideo, initiator, original, entityTokens, GenTicks.SecondsToTicks(TimeoutSeconds));
            Pending[ideo.id] = record;
            Log.Message($"{LogPrefix} Queued ideology (id={ideo.id}, name={ideo.name ?? "?"}, entities={record.EntityTokens.Count}).");
        }

        private static void TryQueueAll()
        {
            var manager = Find.IdeoManager;
            if (manager == null) return;
            var ideos = manager.IdeosListForReading;
            if (ideos == null || ideos.Count == 0) return;

            for (int i = 0; i < ideos.Count; i++)
                TryQueue(ideos[i]);
        }

        private static void StartRequest(PendingIdeoRewrite record)
        {
            if (record == null || record.Requested) return;
            var request = BuildRequest(record);
            if (request == null)
            {
                Pending.Remove(record.IdeoId);
                return;
            }

            record.Requested = true;
            Log.Message($"{LogPrefix} Dispatching independent LLM request (id={record.IdeoId}).");

            var task = IndependentBookLlmClient.QueryJsonAsync<IdeoFlavorSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyResult(record.IdeoId, spec));
            }, TaskScheduler.Default);
        }

        private static TalkRequest BuildRequest(PendingIdeoRewrite record)
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
$@"Write 3-5 sentences of in-universe flavor to append to an ideology description.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""flavor""

Constraints:
- Keep it relevant to the ideology's themes; avoid unrelated filler.
- Do NOT include any names, titles, factions, places, rewards, requirements, or time limits.
- Do NOT include any numbers or percentages.
- Keep it generic and evocative; add atmosphere without new facts.
- Keep length <= {charLimit} characters (about {TargetTokens} tokens).
- If unsure, return an empty string.
- No markdown, no extra keys.";
        }

        private static string BuildContext(PendingIdeoRewrite record)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[IdeologyDescription]");
            if (!string.IsNullOrWhiteSpace(record.IdeoName))
                sb.AppendLine($"IdeoName: {record.IdeoName}");
            if (!string.IsNullOrWhiteSpace(record.CultureLabel))
                sb.AppendLine($"Culture: {record.CultureLabel}");
            if (!record.MemeLabels.NullOrEmpty())
                sb.AppendLine($"Memes: {string.Join(", ", record.MemeLabels)}");
            sb.AppendLine("OriginalDescription:");
            sb.AppendLine(record.OriginalDescription);
            return sb.ToString().TrimEnd();
        }

        private static void ApplyResult(int ideoId, IdeoFlavorSpec spec)
        {
            if (!Pending.TryGetValue(ideoId, out var record))
                return;

            Pending.Remove(ideoId);

            if (record == null || spec == null) return;
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame > record.DeadlineTick) return;

            string flavor = spec.Flavor?.Trim();
            if (string.IsNullOrWhiteSpace(flavor))
                return;
            if (ContainsAnyDigits(flavor)) return;
            if (ContainsAnyEntity(flavor, record.EntityTokens)) return;

            if (!IsIdeoActive(record.Ideo)) return;

            record.Ideo.description = $"{record.OriginalDescription}\n\n{flavor}";
            var cache = LiteratueSaveData.Current?.IdeoCache;
            cache?.Set(record.IdeoId, flavor);
            cache?.MarkProcessed(record.IdeoId);
            Processed.Add(record.IdeoId);
            Log.Message($"{LogPrefix} Applied flavor (id={record.IdeoId}, name={record.IdeoName ?? "?"}).");
        }

        private static bool IsIdeoActive(Ideo ideo)
        {
            if (ideo == null) return false;
            var manager = Find.IdeoManager;
            if (manager == null) return false;
            return manager.IdeosListForReading.Contains(ideo);
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

        private static bool IsAlreadyProcessed(int ideoId)
        {
            var cache = LiteratueSaveData.Current?.IdeoCache;
            if (cache == null)
            {
                Log.Message($"{LogPrefix} Cache missing when checking processed (id={ideoId}).");
                return false;
            }

            bool processed = cache.IsProcessed(ideoId);
            Log.Message($"{LogPrefix} Cache check processed={processed} (id={ideoId}, cached={cache.ProcessedCount}).");
            return processed;
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

        private static List<string> CollectEntityTokens(Ideo ideo, string description)
        {
            var tokens = new HashSet<string>();
            if (ideo == null || string.IsNullOrWhiteSpace(description)) return tokens.ToList();

            void AddToken(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (!description.Contains(value)) return;
                tokens.Add(value);
            }

            AddToken(ideo.name);
            AddToken(ideo.adjective);
            AddToken(ideo.memberName);
            AddToken(ideo.leaderTitleMale);
            AddToken(ideo.leaderTitleFemale);
            if (ideo.culture != null)
                AddToken(ideo.culture.label);

            if (ideo.memes != null)
            {
                for (int i = 0; i < ideo.memes.Count; i++)
                {
                    var meme = ideo.memes[i];
                    if (meme == null) continue;
                    AddToken(meme.label);
                }
            }

            return tokens.OrderByDescending(t => t.Length).ToList();
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

        private sealed class PendingIdeoRewrite
        {
            public int IdeoId { get; }
            public Ideo Ideo { get; }
            public Pawn Initiator { get; }
            public string IdeoName { get; }
            public string CultureLabel { get; }
            public List<string> MemeLabels { get; }
            public string OriginalDescription { get; }
            public List<string> EntityTokens { get; }
            public int QueuedTick { get; }
            public int DeadlineTick { get; }
            public bool Requested { get; set; }

            public PendingIdeoRewrite(Ideo ideo, Pawn initiator, string originalDescription, List<string> entityTokens, int timeoutTicks)
            {
                Ideo = ideo;
                IdeoId = ideo?.id ?? -1;
                Initiator = initiator;
                IdeoName = ideo?.name;
                CultureLabel = ideo?.culture?.label;
                MemeLabels = ideo?.memes?.Select(m => m?.label).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
                    ?? new List<string>();
                OriginalDescription = originalDescription ?? string.Empty;
                EntityTokens = entityTokens ?? new List<string>();
                QueuedTick = Find.TickManager.TicksGame;
                DeadlineTick = QueuedTick + timeoutTicks;
            }
        }
    }
}
