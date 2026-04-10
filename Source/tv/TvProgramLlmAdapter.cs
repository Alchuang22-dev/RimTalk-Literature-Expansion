using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk_LiteratureExpansion.synopsis.llm;

namespace RimTalk_LiteratureExpansion.tv
{
    public static class TvProgramLlmAdapter
    {
        public static Task<TvProgramContent> QueryAsync(TalkRequest request)
        {
            if (request == null) return Task.FromResult<TvProgramContent>(null);
            return IndependentBookLlmClient.QueryJsonAsync<TvProgramContent>(request);
        }
    }
}
