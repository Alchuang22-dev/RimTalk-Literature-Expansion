using System.Threading.Tasks;
using RimTalk_LiteratureExpansion.llm;
using RimTalk_LiteratureExpansion.synopsis.llm;

namespace RimTalk_LiteratureExpansion.tv
{
    public static class TvProgramLlmAdapter
    {
        public static Task<TvProgramContent> QueryAsync(LiteratureLlmRequest request)
        {
            if (request == null) return Task.FromResult<TvProgramContent>(null);
            return IndependentBookLlmClient.QueryJsonAsync<TvProgramContent>(request);
        }
    }
}
