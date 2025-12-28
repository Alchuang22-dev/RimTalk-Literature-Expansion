using System.Runtime.Serialization;
using RimTalk.Data;

namespace RimTalk_LiteratureExpansion.synopsis.model
{
    [DataContract]
    public sealed class BookSynopsis : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "synopsis")]
        public string Synopsis { get; set; }

        public string GetText()
        {
            if (string.IsNullOrWhiteSpace(Title)) return Synopsis ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Synopsis)) return Title ?? string.Empty;
            return $"{Title}: {Synopsis}";
        }
    }
}
