using RimTalk_LiteratureExpansion.settings;
using RimTalk_LiteratureExpansion.storage;
using RimTalk_LiteratureExpansion.storage.save;
using Verse;

namespace RimTalk_LiteratureExpansion.integration
{
    public static class ArtCacheUtil
    {
        public static bool IsArtEditingEnabled()
        {
            var settings = LiteratureMod.Settings;
            return settings != null && settings.allowArtEdits;
        }

        public static bool TryGetRecord(Thing thing, out ArtDescriptionRecord record)
        {
            record = null;
            if (thing == null) return false;

            var cache = LiteratueSaveData.Current?.ArtCache;
            if (cache == null) return false;

            if (!ArtKeyProvider.TryGetKey(thing, out var key)) return false;
            return cache.TryGet(key, out record);
        }
    }
}
