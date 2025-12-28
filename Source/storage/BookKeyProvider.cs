using Verse;

namespace RimTalk_LiteratureExpansion.storage
{
    public static class BookKeyProvider
    {
        public static bool TryGetKey(Thing thing, out BookKey key)
        {
            key = null;
            if (thing == null || thing.DestroyedOrNull()) return false;

            var loadId = thing.GetUniqueLoadID();
            if (string.IsNullOrEmpty(loadId)) return false;

            var mapId = thing.Map?.uniqueID ?? 0;
            key = new BookKey($"{loadId}|{mapId}");
            return key.IsValid;
        }
    }
}
