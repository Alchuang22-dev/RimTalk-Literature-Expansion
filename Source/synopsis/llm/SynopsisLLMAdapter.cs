using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.synopsis.model;

namespace RimTalk_LiteratureExpansion.synopsis.llm
{
    public static class SynopsisLLMAdapter
    {
        public static Task<BookSynopsis> QuerySynopsisAsync(TalkRequest request)
        {
            if (request == null) return Task.FromResult<BookSynopsis>(null);
            return IndependentBookLlmClient.QueryJsonAsync<BookSynopsis>(request);
        }
    }
}
