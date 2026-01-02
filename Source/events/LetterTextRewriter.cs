/*
 * Purpose:
 * - Append flavor text to letters via LLM without changing facts.
 *
 * Uses:
 * - IndependentBookLlmClient.QueryJsonAsync<T> for JSON output.
 *
 * Responsibilities:
 * - Queue letter text and append flavor if it returns before a timeout.
 *
 * Design notes:
 * - Only letter text is modified; labels and gameplay data are untouched.
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
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events
{
    [DataContract]
    public sealed class LetterFlavorSpec : IJsonData
    {
        [DataMember(Name = "flavor")]
        public string Flavor { get; set; }

        public string GetText()
        {
            return Flavor ?? string.Empty;
        }
    }

    public static class LetterTextRewriter
    {
        public const string CustomLetterDebugInfo = "RimTalkLE_CustomLetter";
        private const int TimeoutSeconds = 60;
        private const int TargetTokens = 140;
        private const string LogPrefix = "[RimTalk LE] [LetterRewrite]";

        private static readonly Dictionary<int, PendingLetterRewrite> Pending = new Dictionary<int, PendingLetterRewrite>();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();
        private static readonly object QueueLock = new object();

        public static void Tick()
        {
            ProcessPendingActions();

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowLetterTextRewrite) return;
            if (Find.TickManager == null || Pending.Count == 0) return;

            int tick = Find.TickManager.TicksGame;
            var expired = Pending.Where(kvp => tick > kvp.Value.DeadlineTick).Select(kvp => kvp.Key).ToList();
            for (int i = 0; i < expired.Count; i++)
                Pending.Remove(expired[i]);

            PendingLetterRewrite next = null;
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

        public static void TryQueue(Letter letter)
        {
            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.allowLetterTextRewrite) return;
            if (letter == null) return;
            if (letter is BundleLetter) return;
            if (!string.IsNullOrWhiteSpace(letter.debugInfo) && letter.debugInfo.Contains(CustomLetterDebugInfo))
                return;
            if (Find.TickManager == null) return;

            if (!IsLetterAllowed(settings, letter))
            {
                Log.Message($"{LogPrefix} Skip: letter not allowed (id={letter.ID}, def={letter.def?.defName ?? "null"}).");
                return;
            }

            if (!TryGetLetterText(letter, out var originalText))
                return;
            if (string.IsNullOrWhiteSpace(originalText))
                return;

            if (Pending.ContainsKey(letter.ID))
                return;

            var initiator = TryGetAnyColonist();
            if (initiator == null)
                return;

            var entityTokens = CollectEntityTokens(letter, originalText);
            var record = new PendingLetterRewrite(letter, initiator, originalText, entityTokens, GenTicks.SecondsToTicks(TimeoutSeconds));
            Pending[letter.ID] = record;
            Log.Message($"{LogPrefix} Queued letter (id={letter.ID}, def={letter.def?.defName ?? "null"}, entities={record.EntityTokens.Count}).");
        }

        private static void StartRequest(PendingLetterRewrite record)
        {
            if (record == null || record.Requested) return;
            var request = BuildRequest(record);
            if (request == null)
            {
                Pending.Remove(record.LetterId);
                return;
            }

            record.Requested = true;
            Log.Message($"{LogPrefix} Dispatching independent LLM request (id={record.LetterId}).");

            var task = IndependentBookLlmClient.QueryJsonAsync<LetterFlavorSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyResult(record.LetterId, spec));
            }, TaskScheduler.Default);
        }

        private static TalkRequest BuildRequest(PendingLetterRewrite record)
        {
            if (record == null || record.Initiator == null) return null;

            var prompt = BuildPrompt(record.OriginalText);
            var context = BuildContext(record);

            return new TalkRequest(prompt, record.Initiator)
            {
                Context = context
            };
        }

        private static string BuildPrompt(string originalText)
        {
            int charLimit = Mathf.Clamp(originalText?.Length ?? 0, 240, 520);
            return
$@"Write 3-5 sentences of in-universe flavor to append to a letter.
Write in {Constant.Lang}. Return JSON only.

Required JSON fields:
- ""flavor""

Constraints:
- Use the letter's situation as context; avoid unrelated filler.
- Do NOT include any names, factions, places, rewards, requirements, or time limits.
- Do NOT include any numbers or percentages.
- Keep it generic and evocative; add atmosphere without new facts.
- Keep length <= {charLimit} characters (about {TargetTokens} tokens).
- If unsure, return an empty string.
- No markdown, no extra keys.";
        }

        private static string BuildContext(PendingLetterRewrite record)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Letter]");
            if (!string.IsNullOrWhiteSpace(record.LetterLabel))
                sb.AppendLine($"Label: {record.LetterLabel}");
            if (!string.IsNullOrWhiteSpace(record.RelatedFaction))
                sb.AppendLine($"Faction: {record.RelatedFaction}");
            sb.AppendLine("OriginalText:");
            sb.AppendLine(record.OriginalText);
            return sb.ToString().TrimEnd();
        }

        private static void ApplyResult(int letterId, LetterFlavorSpec spec)
        {
            if (!Pending.TryGetValue(letterId, out var record))
                return;

            Pending.Remove(letterId);

            if (record == null || spec == null) return;
            if (Find.TickManager == null) return;
            if (Find.TickManager.TicksGame > record.DeadlineTick) return;

            string flavor = spec.Flavor?.Trim();
            if (string.IsNullOrWhiteSpace(flavor))
                return;
            if (ContainsAnyDigits(flavor)) return;
            if (ContainsAnyEntity(flavor, record.EntityTokens)) return;

            if (!IsLetterActive(record.Letter)) return;

            if (!TryAppendFlavorToLetter(record.Letter, record.OriginalText, flavor))
                return;

            Log.Message($"{LogPrefix} Applied flavor (id={record.LetterId}).");
        }

        private static bool IsLetterActive(Letter letter)
        {
            if (letter == null) return false;
            var stack = Find.LetterStack;
            if (stack == null) return false;
            if (stack.LettersListForReading.Contains(letter))
                return true;

            var field = typeof(LetterStack).GetField("letterQueue",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var queue = field?.GetValue(stack) as List<Letter>;
            return queue != null && queue.Contains(letter);
        }

        private static bool IsLetterAllowed(LiteratureSettings settings, Letter letter)
        {
            if (settings == null || letter == null) return false;
            var def = letter.def;
            if (def == null || string.IsNullOrWhiteSpace(def.defName)) return false;
            var allowList = settings.letterRewriteAllowList;
            if (allowList == null || allowList.Count == 0) return false;
            return allowList.Contains(def.defName);
        }

        private static bool TryGetLetterText(Letter letter, out string text)
        {
            text = null;
            if (letter == null) return false;

            if (letter is ChoiceLetter choice)
            {
                text = choice.Text.Resolve();
                return true;
            }

            if (letter is RimWorld.ChoiceLetter_GrowthMoment growth)
            {
                text = growth.text.Resolve();
                return true;
            }

            var type = letter.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var textField = type.GetField("text", flags);
            if (textField != null)
            {
                var value = textField.GetValue(letter);
                if (value != null)
                {
                    text = value.ToString();
                    return true;
                }
            }

            var textProp = type.GetProperty("Text", flags);
            if (textProp != null)
            {
                var value = textProp.GetValue(letter, null);
                if (value != null)
                {
                    text = value.ToString();
                    return true;
                }
            }

            return false;
        }

        private static bool TryAppendFlavorToLetter(Letter letter, string originalText, string flavor)
        {
            if (letter == null) return false;
            string combined = $"{originalText}\n\n{flavor}";

            if (letter is ChoiceLetter choice)
            {
                choice.Text = combined;
                return true;
            }

            if (letter is RimWorld.ChoiceLetter_GrowthMoment growth)
            {
                growth.text = combined;
                if (!growth.mouseoverText.NullOrEmpty())
                    growth.mouseoverText = $"{growth.mouseoverText}\n\n{flavor}";
                else
                    growth.mouseoverText = combined;
                return true;
            }

            var type = letter.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var textField = type.GetField("text", flags);
            if (textField != null)
            {
                textField.SetValue(letter, combined);
                var mouseoverField = type.GetField("mouseoverText", flags);
                if (mouseoverField != null)
                {
                    var existing = mouseoverField.GetValue(letter);
                    if (existing != null)
                        mouseoverField.SetValue(letter, $"{existing}\n\n{flavor}");
                    else
                        mouseoverField.SetValue(letter, combined);
                }
                return true;
            }

            var textProp = type.GetProperty("Text", flags);
            if (textProp != null && textProp.CanWrite)
            {
                textProp.SetValue(letter, combined, null);
                return true;
            }

            return false;
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

        private static List<string> CollectEntityTokens(Letter letter, string text)
        {
            var tokens = new HashSet<string>();
            if (letter == null || string.IsNullOrWhiteSpace(text)) return tokens.ToList();

            void AddToken(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                if (!text.Contains(value)) return;
                tokens.Add(value);
            }

            if (letter.relatedFaction != null)
                AddToken(letter.relatedFaction.Name);

            if (letter.lookTargets.IsValid())
            {
                foreach (var target in letter.lookTargets.targets)
                {
                    var pawn = target.Thing as Pawn;
                    if (pawn == null) continue;
                    AddToken(pawn.LabelShortCap);
                    AddToken(pawn.Name?.ToStringShort);
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

        private sealed class PendingLetterRewrite
        {
            public int LetterId { get; }
            public Letter Letter { get; }
            public Pawn Initiator { get; }
            public string OriginalText { get; }
            public string LetterLabel { get; }
            public string RelatedFaction { get; }
            public List<string> EntityTokens { get; }
            public int QueuedTick { get; }
            public int DeadlineTick { get; }
            public bool Requested { get; set; }

            public PendingLetterRewrite(Letter letter, Pawn initiator, string originalText, List<string> entityTokens, int timeoutTicks)
            {
                Letter = letter;
                LetterId = letter?.ID ?? -1;
                Initiator = initiator;
                OriginalText = originalText ?? string.Empty;
                LetterLabel = letter?.Label.Resolve();
                RelatedFaction = letter?.relatedFaction?.Name;
                EntityTokens = entityTokens ?? new List<string>();
                QueuedTick = Find.TickManager.TicksGame;
                DeadlineTick = QueuedTick + timeoutTicks;
            }
        }
    }
}
