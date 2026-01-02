using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.Service;
using RimTalk_LiteratureExpansion.events.letters;
using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage.save;
using RimWorld;
using Verse;

namespace RimTalk_LiteratureExpansion.events
{
    public static class LetterEventScheduler
    {
        private const int CheckIntervalTicks = GenDate.TicksPerHour;
        private const int DiplomacyGoodwillDelta = 6;
        private const int DiplomacyRetryTicks = GenDate.TicksPerQuadrum;
        private const int FamilyRetryTicks = GenDate.TicksPerDay * 3;
        private const int FamilyMinIntervalTicks = GenDate.TicksPerDay * 10;
        private const int FamilyMaxIntervalTicks = GenDate.TicksPerDay * 25;
        private const int FriendOpinionThreshold = Pawn_RelationsTracker.FriendOpinionThreshold;
        private const int FriendCandidateLimit = 40;

        private static int _nextCheckTick;
        private static bool _diplomacyPending;
        private static bool _familyPending;
        private static readonly object QueueLock = new object();
        private static readonly Queue<Action> PendingActions = new Queue<Action>();

        public static void Tick()
        {
            ProcessPendingActions();

            var settings = LiteratureMod.Settings;
            if (settings != null && !settings.enabled) return;

            if (Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if (tick < _nextCheckTick) return;
            _nextCheckTick = tick + CheckIntervalTicks;

            var data = LiteratueSaveData.Current;
            if (data == null) return;

            TryScheduleAllyDiplomacy(data, tick);
            TryScheduleFamilyLetter(data, tick);
        }

        public static void DebugTriggerAllyDiplomacy()
        {
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextAllyDiplomacyTick = tick;
            TryScheduleAllyDiplomacy(data, tick);
        }

        public static void DebugTriggerFamilyLetter()
        {
            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            data.NextFamilyLetterTick = tick;
            TryScheduleFamilyLetter(data, tick);
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

        private static void TryScheduleAllyDiplomacy(LiteratueSaveData data, int tick)
        {
            if (_diplomacyPending) return;
            if (data.NextAllyDiplomacyTick <= 0)
                data.NextAllyDiplomacyTick = tick + Rand.RangeInclusive(GenDate.TicksPerQuadrum, GenDate.TicksPerYear);
            if (tick < data.NextAllyDiplomacyTick) return;
            if (AIService.IsBusy()) return;

            var map = GetBestPlayerMap();
            var initiator = map != null ? GetAnyColonist(map) : null;
            if (initiator == null) return;

            var faction = GetRandomAlliedFaction();
            if (faction == null)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                return;
            }

            string colonyName = map?.info?.parent?.LabelCap ?? "Colony";
            var request = AllyDiplomacyLetterRequest.BuildRequest(initiator, faction, map, colonyName, DiplomacyGoodwillDelta);
            if (request == null)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                return;
            }

            _diplomacyPending = true;
            Log.Message($"[RimTalk LE] [Letter] Scheduling ally diplomacy letter from {faction.Name}.");

            var task = AIService.Query<AllyDiplomacyLetterSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyAllyDiplomacyResult(spec, faction, map));
            }, TaskScheduler.Default);
        }

        private static void ApplyAllyDiplomacyResult(AllyDiplomacyLetterSpec spec, Faction faction, Map map)
        {
            _diplomacyPending = false;

            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            if (spec == null || faction == null || faction.defeated)
            {
                data.NextAllyDiplomacyTick = tick + DiplomacyRetryTicks;
                Log.Message("[RimTalk LE] [Letter] Ally diplomacy letter failed; retry scheduled.");
                return;
            }

            string label = !spec.Title.NullOrEmpty()
                ? spec.Title
                : "RimTalkLE_Letter_AllyDiplomacy_Label".Translate();

            string body = !spec.Body.NullOrEmpty()
                ? spec.Body
                : "RimTalkLE_Letter_AllyDiplomacy_Fallback".Translate(faction.Name);

            var goodwillLine = "RimTalkLE_Letter_AllyDiplomacy_GoodwillLine".Translate(DiplomacyGoodwillDelta.ToString());
            if (!goodwillLine.NullOrEmpty())
                body = $"{body}\n\n{goodwillLine}";

            Faction.OfPlayer.TryAffectGoodwillWith(faction, DiplomacyGoodwillDelta, canSendMessage: false, canSendHostilityLetter: false);

            var target = map != null ? new LookTargets(map.Center, map) : LookTargets.Invalid;
            var letter = LetterMaker.MakeLetter(label, body, LetterDefOf.PositiveEvent, target, faction);
            Find.LetterStack.ReceiveLetter(letter, LetterTextRewriter.CustomLetterDebugInfo);
            data.NextAllyDiplomacyTick = tick + GenDate.TicksPerYear;
        }

        private static void TryScheduleFamilyLetter(LiteratueSaveData data, int tick)
        {
            if (_familyPending) return;
            if (data.NextFamilyLetterTick <= 0)
                data.NextFamilyLetterTick = tick + Rand.RangeInclusive(FamilyMinIntervalTicks, FamilyMaxIntervalTicks);
            if (tick < data.NextFamilyLetterTick) return;
            if (AIService.IsBusy()) return;

            if (!TryPickFamilyLetterPawns(out var colonist, out var relative, out var relationLabel, out var map))
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                return;
            }

            var request = FamilyLetterRequest.BuildRequest(colonist, colonist, relative, relationLabel);
            if (request == null)
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                return;
            }

            _familyPending = true;
            Log.Message($"[RimTalk LE] [Letter] Scheduling family letter for {colonist.LabelShortCap}.");

            var task = AIService.Query<FamilyLetterSpec>(request);
            task.ContinueWith(t =>
            {
                var spec = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                EnqueueAction(() => ApplyFamilyLetterResult(spec, colonist, relative, map));
            }, TaskScheduler.Default);
        }

        private static void ApplyFamilyLetterResult(FamilyLetterSpec spec, Pawn colonist, Pawn relative, Map map)
        {
            _familyPending = false;

            var data = LiteratueSaveData.Current;
            if (data == null || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;

            if (spec == null || colonist == null || map == null)
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                Log.Message("[RimTalk LE] [Letter] Family letter failed; retry scheduled.");
                return;
            }

            if (!LetterGiftResolver.TryResolveGift(spec.GiftKind, out var giftDef, out var giftCount))
            {
                data.NextFamilyLetterTick = tick + FamilyRetryTicks;
                Log.Message("[RimTalk LE] [Letter] Family letter gift resolution failed; retry scheduled.");
                return;
            }

            var gift = ThingMaker.MakeThing(giftDef);
            gift.stackCount = giftCount;

            var dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, Gen.YieldSingle(gift), canRoofPunch: false, forbid: false);

            string label = !spec.Title.NullOrEmpty()
                ? spec.Title
                : "RimTalkLE_Letter_Family_Label".Translate();

            string body = !spec.Body.NullOrEmpty()
                ? spec.Body
                : "RimTalkLE_Letter_Family_Fallback".Translate(colonist.LabelShortCap);

            string giftNote = !spec.GiftNote.NullOrEmpty()
                ? spec.GiftNote
                : "RimTalkLE_Letter_Family_GiftNoteFallback".Translate();

            if (!giftNote.NullOrEmpty())
                body = $"{body}\n\n{giftNote}";

            var giftLine = "RimTalkLE_Letter_GiftLine".Translate(gift.LabelCap, giftCount.ToString());
            if (!giftLine.NullOrEmpty())
                body = $"{body}\n\n{giftLine}";

            LookTargets target = colonist != null && colonist.Spawned
                ? new LookTargets(colonist)
                : new LookTargets(map.Center, map);

            var letter = LetterMaker.MakeLetter(label, body, LetterDefOf.PositiveEvent, target);
            Find.LetterStack.ReceiveLetter(letter, LetterTextRewriter.CustomLetterDebugInfo);
            data.NextFamilyLetterTick = tick + Rand.RangeInclusive(FamilyMinIntervalTicks, FamilyMaxIntervalTicks);
        }

        private static Map GetBestPlayerMap()
        {
            var maps = Find.Maps;
            if (maps == null) return null;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (map != null && map.IsPlayerHome && map.mapPawns?.FreeColonistsSpawned?.Count > 0)
                    return map;
            }
            return null;
        }

        private static Pawn GetAnyColonist(Map map)
        {
            var pawns = map?.mapPawns?.FreeColonistsSpawned;
            if (pawns == null || pawns.Count == 0) return null;
            return pawns[0];
        }

        private static Faction GetRandomAlliedFaction()
        {
            var factions = Find.FactionManager?.AllFactionsVisible;
            if (factions == null) return null;

            var candidates = new List<Faction>();
            foreach (var faction in factions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.defeated) continue;
                if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Ally) continue;
                candidates.Add(faction);
            }

            return candidates.Count > 0 ? candidates.RandomElement() : null;
        }

        private static bool TryPickFamilyLetterPawns(
            out Pawn colonist,
            out Pawn relative,
            out string relationLabel,
            out Map map)
        {
            colonist = null;
            relative = null;
            relationLabel = null;
            map = GetBestPlayerMap();
            if (map == null) return false;

            var colonists = map.mapPawns?.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return false;

            var candidates = new List<(Pawn colonist, Pawn relative, string relation, int priority)>();
            for (int i = 0; i < colonists.Count; i++)
            {
                var pawn = colonists[i];
                if (pawn?.relations == null) continue;

                var relations = pawn.relations.DirectRelations;
                for (int j = 0; j < relations.Count; j++)
                {
                    var relation = relations[j];
                    var other = relation.otherPawn;
                    if (!IsValidRelative(other, map)) continue;

                    string label = relation.def?.label ?? "relative";
                    int priority = GetOffMapRelationPriority(other, map);
                    candidates.Add((pawn, other, label, priority));
                }
            }

            if (candidates.Count == 0)
            {
                TryAddFriendCandidates(colonists, map, candidates);
            }

            if (candidates.Count == 0) return false;

            int bestPriority = candidates.Max(c => c.priority);
            var topCandidates = candidates.Where(c => c.priority == bestPriority).ToList();
            var chosen = topCandidates.RandomElement();
            colonist = chosen.colonist;
            relative = chosen.relative;
            relationLabel = chosen.relation;
            return true;
        }

        private static bool IsValidRelative(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.RaceProps?.Humanlike != true) return false;
            if (pawn.Spawned && pawn.Map == map) return false;
            return true;
        }

        private static void TryAddFriendCandidates(
            IList<Pawn> colonists,
            Map map,
            List<(Pawn colonist, Pawn relative, string relation, int priority)> candidates)
        {
            if (colonists == null || colonists.Count == 0) return;
            var worldPawns = Find.WorldPawns?.AllPawnsAlive;
            if (worldPawns == null || worldPawns.Count == 0) return;

            string friendLabel = "RimTalkLE_Letter_RelationFriend".Translate();
            int added = 0;

            for (int i = 0; i < colonists.Count; i++)
            {
                var colonist = colonists[i];
                if (colonist?.relations == null) continue;

                for (int j = 0; j < worldPawns.Count; j++)
                {
                    var other = worldPawns[j];
                    if (other == null || other == colonist) continue;
                    if (!IsValidRelative(other, map)) continue;

                    if (colonist.relations.OpinionOf(other) < FriendOpinionThreshold)
                        continue;

                    int priority = GetOffMapRelationPriority(other, map);
                    candidates.Add((colonist, other, friendLabel, priority));
                    added++;
                    if (added >= FriendCandidateLimit)
                        return;
                }
            }
        }

        private static int GetOffMapRelationPriority(Pawn pawn, Map map)
        {
            if (pawn == null) return 0;
            if (pawn.Spawned && pawn.Map == map) return 0;
            if (pawn.Faction == Faction.OfPlayer && pawn.IsColonist) return 2;
            if (pawn.Faction == Faction.OfPlayer) return 1;
            return 0;
        }
    }
}
