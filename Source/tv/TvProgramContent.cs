using System.Runtime.Serialization;
using RimTalk.Data;

namespace RimTalk_LiteratureExpansion.tv
{
    [DataContract]
    public sealed class TvProgramContent : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }

        public string GetText()
        {
            if (string.IsNullOrWhiteSpace(Title)) return Content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Content)) return Title ?? string.Empty;
            return $"{Title}: {Content}";
        }
    }
}
