using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace ServiceDesk.Components.Agents
{
    public class KnowledgeAgent
    {
        private readonly ILogger<KnowledgeAgent> _logger;
        private readonly SearchClient _searchClient;
        private readonly IDatabase _redisCache;
        private readonly IConfiguration _configuration;

        public KnowledgeAgent(
            ILogger<KnowledgeAgent> logger,
            SearchClient searchClient,
            IDatabase redisCache,
            IConfiguration configuration)
        {
            _logger = logger;
            _searchClient = searchClient;
            _redisCache = redisCache;
            _configuration = configuration;
        }

        public async Task<KnowledgeSearchResult> SearchKnowledgeAsync(string query, string category = "")
        {
            var startTime = DateTime.UtcNow;
            var cacheKey = $"knowledge:{query.GetHashCode()}:{category}";

            try
            {
                _logger.LogInformation("Starting knowledge search for query: {Query}", query);

                // Check Redis cache first (70%+ hit rate target)
                var cachedResult = await _redisCache.StringGetAsync(cacheKey);
                if (cachedResult.HasValue)
                {
                    _logger.LogInformation("Cache hit for query: {Query}", query);
                    var cached = JsonSerializer.Deserialize<KnowledgeSearchResult>(cachedResult!);
                    cached!.FromCache = true;
                    cached.ProcessingTime = DateTime.UtcNow - startTime;
                    return cached;
                }

                // Semantic search with Azure AI Search
                var searchOptions = new SearchOptions
                {
                    Size = 10,
                    IncludeTotalCount = true,
                    QueryType = SearchQueryType.Semantic,
                    SemanticSearch = new SemanticSearchOptions
                    {
                        SemanticConfigurationName = "knowledge-semantic-config",
                        QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                        QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
                    },
                    SearchFields = { "title", "content", "category", "tags" },
                    Select = { "id", "title", "content", "category", "confidence", "lastUpdated" }
                };

                if (!string.IsNullOrEmpty(category))
                {
                    searchOptions.Filter = $"category eq '{category}'";
                }

                var searchResults = await _searchClient.SearchAsync<KnowledgeDocument>(query, searchOptions);
                
                var results = new List<KnowledgeItem>();
                await foreach (var result in searchResults.Value.GetResultsAsync())
                {
                    results.Add(new KnowledgeItem
                    {
                        Id = result.Document.Id,
                        Title = result.Document.Title,
                        Content = result.Document.Content,
                        Category = result.Document.Category,
                        Relevance = result.Score ?? 0,
                        Confidence = result.Document.Confidence,
                        LastUpdated = result.Document.LastUpdated
                    });
                }

                var searchResult = new KnowledgeSearchResult
                {
                    Query = query,
                    Results = results,
                    TotalCount = (int)(searchResults.Value.TotalCount ?? 0),
                    ProcessingTime = DateTime.UtcNow - startTime,
                    FromCache = false,
                    HasRelevantResults = results.Any(r => r.Relevance >= 0.85) // 85%+ relevance target
                };

                // Cache results for future queries
                var cacheExpiry = TimeSpan.FromMinutes(30);
                await _redisCache.StringSetAsync(cacheKey, JsonSerializer.Serialize(searchResult), cacheExpiry);

                _logger.LogInformation("Knowledge search completed. Found {Count} results with {RelevantCount} highly relevant", 
                    results.Count, results.Count(r => r.Relevance >= 0.85));

                return searchResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during knowledge search for query: {Query}", query);
                throw;
            }
        }

        public async Task<ContentIngestionResult> IngestContentAsync(KnowledgeDocument document)
        {
            try
            {
                _logger.LogInformation("Ingesting knowledge document: {DocumentId}", document.Id);

                // Add vector embeddings for semantic search
                document.ContentVector = await GenerateEmbeddingsAsync(document.Content);
                document.LastUpdated = DateTime.UtcNow;

                var batch = IndexDocumentsBatch.Upload(new[] { document });
                var result = await _searchClient.IndexDocumentsAsync(batch);

                var ingestionResult = new ContentIngestionResult
                {
                    DocumentId = document.Id,
                    Success = result.Value.Results.All(r => r.Succeeded),
                    ProcessingTime = TimeSpan.FromMilliseconds(result.Value.Results.First().ProcessingTime),
                    ErrorMessage = result.Value.Results.FirstOrDefault(r => !r.Succeeded)?.ErrorMessage
                };

                if (ingestionResult.Success)
                {
                    // Invalidate related cache entries
                    await InvalidateCacheAsync(document.Category);
                    _logger.LogInformation("Successfully ingested document: {DocumentId}", document.Id);
                }

                return ingestionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting document: {DocumentId}", document.Id);
                return new ContentIngestionResult
                {
                    DocumentId = document.Id,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<SearchAnalytics> GetSearchAnalyticsAsync(DateTime from, DateTime to)
        {
            try
            {
                // Get analytics from Redis cache patterns
                var keys = await GetCacheKeysAsync("knowledge:*");
                var analytics = new SearchAnalytics
                {
                    Period = new DateRange { From = from, To = to },
                    TotalQueries = keys.Count,
                    CacheHitRate = await CalculateCacheHitRateAsync(),
                    AverageResponseTime = await CalculateAverageResponseTimeAsync(),
                    TopQueries = await GetTopQueriesAsync(10),
                    CategoryDistribution = await GetCategoryDistributionAsync()
                };

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving search analytics");
                throw;
            }
        }

        private async Task<float[]> GenerateEmbeddingsAsync(string content)
        {
            // Placeholder for Azure OpenAI embeddings generation
            // In production, this would call Azure OpenAI Service
            await Task.Delay(10); // Simulate API call
            return new float[1536]; // Standard embedding size
        }

        private async Task InvalidateCacheAsync(string category)
        {
            var pattern = $"knowledge:*:{category}";
            var keys = await GetCacheKeysAsync(pattern);
            
            if (keys.Any())
            {
                await _redisCache.KeyDeleteAsync(keys.ToArray());
                _logger.LogInformation("Invalidated {Count} cache entries for category: {Category}", 
                    keys.Count, category);
            }
        }

        private async Task<List<RedisKey>> GetCacheKeysAsync(string pattern)
        {
            // Simplified cache key retrieval
            // In production, use Redis SCAN for better performance
            return new List<RedisKey>();
        }

        private async Task<double> CalculateCacheHitRateAsync()
        {
            // Calculate cache hit rate from metrics
            await Task.Delay(1);
            return 0.75; // 75% hit rate example
        }

        private async Task<TimeSpan> CalculateAverageResponseTimeAsync()
        {
            await Task.Delay(1);
            return TimeSpan.FromMilliseconds(250); // 250ms average
        }

        private async Task<List<string>> GetTopQueriesAsync(int count)
        {
            await Task.Delay(1);
            return new List<string> { "password reset", "email issues", "vpn connection" };
        }

        private async Task<Dictionary<string, int>> GetCategoryDistributionAsync()
        {
            await Task.Delay(1);
            return new Dictionary<string, int>
            {
                ["Technical"] = 45,
                ["Information"] = 35,
                ["Policy"] = 20
            };
        }

        public async Task<HealthCheckResult> HealthCheckAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                // Test search service
                var testQuery = "health check";
                var searchOptions = new SearchOptions { Size = 1 };
                await _searchClient.SearchAsync<KnowledgeDocument>(testQuery, searchOptions);
                
                // Test Redis cache
                await _redisCache.PingAsync();
                
                var responseTime = DateTime.UtcNow - startTime;
                
                return new HealthCheckResult
                {
                    IsHealthy = true,
                    ResponseTime = responseTime,
                    Details = "Search service and Redis cache operational"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge Agent health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Details = ex.Message
                };
            }
        }
    }

    public class KnowledgeDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
        public float[] ContentVector { get; set; } = Array.Empty<float>();
    }

    public class KnowledgeSearchResult
    {
        public string Query { get; set; } = string.Empty;
        public List<KnowledgeItem> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool FromCache { get; set; }
        public bool HasRelevantResults { get; set; }
    }

    public class KnowledgeItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Relevance { get; set; }
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ContentIngestionResult
    {
        public string DocumentId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SearchAnalytics
    {
        public DateRange Period { get; set; } = new();
        public int TotalQueries { get; set; }
        public double CacheHitRate { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public List<string> TopQueries { get; set; } = new();
        public Dictionary<string, int> CategoryDistribution { get; set; } = new();
    }

    public class DateRange
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }
}