using LudeonTK;
using RimTalk_LiteratureExpansion.events;
using Verse;

namespace RimTalk_LiteratureExpansion.events
{
    public static class LetterDebugActions
    {
        [DebugAction("RimTalk LE", "Trigger ally diplomacy letter", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TriggerAllyDiplomacyLetter()
        {
            LetterEventScheduler.DebugTriggerAllyDiplomacy();
        }

        [DebugAction("RimTalk LE", "Trigger family letter", false, false, false, false, false, 0, false,
            actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TriggerFamilyLetter()
        {
            LetterEventScheduler.DebugTriggerFamilyLetter();
        }
    }
}
