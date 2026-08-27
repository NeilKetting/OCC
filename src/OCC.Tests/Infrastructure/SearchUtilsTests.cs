using OCC.WpfClient.Infrastructure;
using Xunit;

namespace OCC.Tests.Infrastructure
{
    public class SearchUtilsTests
    {
        [Theory]
        [InlineData("Lucky M", "Lucky", "Makubule", "334", true)]
        [InlineData("Lucky Makubule", "Lucky", "Makubule", "334", true)]
        [InlineData("334 Lucky", "Lucky", "Makubule", "334", true)]
        [InlineData("Makubule Lucky", "Lucky", "Makubule", "334", true)]
        [InlineData("Lucky", "Lucky", "Makubule", "334", true)]
        [InlineData("M", "Lucky", "Makubule", "334", true)]
        [InlineData("Lucky NonExistent", "Lucky", "Makubule", "334", false)]
        [InlineData("", "Lucky", "Makubule", "334", true)]
        [InlineData("   ", "Lucky", "Makubule", "334", true)]
        [InlineData(null, "Lucky", "Makubule", "334", true)]
        public void MatchesQuery_MultiWordAndTokenMatching_BehavesAsExpected(
            string? searchQuery, string firstName, string lastName, string empNum, bool expected)
        {
            var result = SearchUtils.MatchesQuery(searchQuery, firstName, lastName, empNum);
            Assert.Equal(expected, result);
        }
    }
}
