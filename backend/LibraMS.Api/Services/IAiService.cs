using LibraMS.Api.Models;

namespace LibraMS.Api.Services;

public interface IAiService
{
    Task<AiDescribeResponse> DescribeBookAsync(AiDescribeRequest req);
    Task<AiSearchResponse> ParseNaturalSearchAsync(AiSearchRequest req, IEnumerable<string> availableGenres);
    Task<AiRecommendResponse> RecommendBooksAsync(Guid userId, IEnumerable<Loan> loanHistory, IEnumerable<Book> catalog);
}
