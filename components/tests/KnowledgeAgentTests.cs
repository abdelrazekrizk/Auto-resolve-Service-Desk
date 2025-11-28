using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using StackExchange.Redis;
using ServiceDesk.Components.Agents;

namespace ServiceDesk.Components.Tests
{
    public class KnowledgeAgentTests
    {
        private readonly Mock<ILogger<KnowledgeAgent>> _mockLogger;
        private readonly Mock<SearchClient> _mockSearchClient;
        private readonly Mock<IDatabase> _mockRedisCache;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly KnowledgeAgent _knowledgeAgent;

        public KnowledgeAgentTests()
        {
            _mockLogger = new Mock<ILogger<KnowledgeAgent>>();
            _mockSearchClient = new Mock<SearchClient>();
            _mockRedisCache = new Mock<IDatabase>();
            _mockConfiguration = new Mock<IConfiguration>();

            _knowledgeAgent = new KnowledgeAgent(
                _mockLogger.Object,
                _mockSearchClient.Object,
                _mockRedisCache.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public async Task SearchKnowledgeAsync_ShouldReturnCachedResult_WhenCacheHit()
        {
            // Arrange
            var query = "password reset";
            var cacheKey = $"knowledge:{query.GetHashCode()}:";
            var cachedResult = new KnowledgeSearchResult
            {
                Query = query,
                Results = new List<KnowledgeItem>
                {
                    new KnowledgeItem
                    {
                        Id = "cached-1",
                        Title = "Password Reset Guide",
                        Relevance = 0.95
                    }
                },
                FromCache = false
            };

            _mockRedisCache.Setup(x => x.StringGetAsync(cacheKey, CommandFlags.None))
                          .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cachedResult));

            // Act
            var result = await _knowledgeAgent.SearchKnowledgeAsync(query);

            // Assert
            Assert.True(result.FromCache);
            Assert.Equal(query, result.Query);
            Assert.Single(result.Results);
            Assert.Equal("cached-1", result.Results.First().Id);
        }

        [Fact]
        public async Task SearchKnowledgeAsync_ShouldMeetPerformanceTarget_WhenSearching()
        {
            // Arrange
            var query = "email configuration";
            _mockRedisCache.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                          .ReturnsAsync(RedisValue.Null);

            var mockSearchResults = CreateMockSearchResults();
            _mockSearchClient.Setup(x => x.SearchAsync<KnowledgeDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(mockSearchResults);

            _mockRedisCache.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), 
                It.IsAny<When>(), CommandFlags.None))
                          .ReturnsAsync(true);

            // Act
            var result = await _knowledgeAgent.SearchKnowledgeAsync(query);

            // Assert - Performance target: <30 seconds
            Assert.True(result.ProcessingTime.TotalSeconds < 30,
                $"Processing time {result.ProcessingTime.TotalSeconds}s exceeds 30 second target");
        }

        [Fact]
        public async Task SearchKnowledgeAsync_ShouldMeetRelevanceTarget_WhenResultsFound()
        {
            // Arrange
            var query = "vpn connection issues";
            _mockRedisCache.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                          .ReturnsAsync(RedisValue.Null);

            var mockSearchResults = CreateMockSearchResults(relevanceScore: 0.90);
            _mockSearchClient.Setup(x => x.SearchAsync<KnowledgeDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(mockSearchResults);

            _mockRedisCache.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), 
                It.IsAny<When>(), CommandFlags.None))
                          .ReturnsAsync(true);

            // Act
            var result = await _knowledgeAgent.SearchKnowledgeAsync(query);

            // Assert - Relevance target: 85%+
            Assert.True(result.HasRelevantResults);
            Assert.True(result.Results.Any(r => r.Relevance >= 0.85),
                "No results meet the 85% relevance target");
        }

        [Theory]
        [InlineData("Technical")]
        [InlineData("Information")]
        [InlineData("Policy")]
        public async Task SearchKnowledgeAsync_ShouldFilterByCategory_WhenCategoryProvided(string category)
        {
            // Arrange
            var query = "test query";
            _mockRedisCache.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                          .ReturnsAsync(RedisValue.Null);

            SearchOptions capturedOptions = null!;
            _mockSearchClient.Setup(x => x.SearchAsync<KnowledgeDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
                            .Callback<string, SearchOptions, CancellationToken>((q, opts, ct) => capturedOptions = opts)
                            .ReturnsAsync(CreateMockSearchResults());

            _mockRedisCache.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), 
                It.IsAny<When>(), CommandFlags.None))
                          .ReturnsAsync(true);

            // Act
            await _knowledgeAgent.SearchKnowledgeAsync(query, category);

            // Assert
            Assert.NotNull(capturedOptions);
            Assert.Equal($"category eq '{category}'", capturedOptions.Filter);
        }

        [Fact]
        public async Task IngestContentAsync_ShouldReturnSuccess_WhenDocumentValid()
        {
            // Arrange
            var document = new KnowledgeDocument
            {
                Id = "test-doc-1",
                Title = "Test Document",
                Content = "This is test content for ingestion",
                Category = "Technical"
            };

            var mockIndexResult = CreateMockIndexResult(true);
            _mockSearchClient.Setup(x => x.IndexDocumentsAsync(
                It.IsAny<IndexDocumentsBatch<KnowledgeDocument>>(), 
                It.IsAny<IndexDocumentsOptions>(), 
                It.IsAny<CancellationToken>()))
                            .ReturnsAsync(mockIndexResult);

            // Act
            var result = await _knowledgeAgent.IngestContentAsync(document);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(document.Id, result.DocumentId);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task GetSearchAnalyticsAsync_ShouldReturnValidAnalytics_WhenCalled()
        {
            // Arrange
            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow;

            // Act
            var analytics = await _knowledgeAgent.GetSearchAnalyticsAsync(from, to);

            // Assert
            Assert.NotNull(analytics);
            Assert.Equal(from, analytics.Period.From);
            Assert.Equal(to, analytics.Period.To);
            Assert.True(analytics.CacheHitRate >= 0 && analytics.CacheHitRate <= 1);
            Assert.True(analytics.AverageResponseTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task HealthCheckAsync_ShouldReturnHealthy_WhenAllServicesOperational()
        {
            // Arrange
            _mockSearchClient.Setup(x => x.SearchAsync<KnowledgeDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(CreateMockSearchResults());

            _mockRedisCache.Setup(x => x.PingAsync(CommandFlags.None))
                          .ReturnsAsync(TimeSpan.FromMilliseconds(10));

            // Act
            var result = await _knowledgeAgent.HealthCheckAsync();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Contains("Search service and Redis cache operational", result.Details);
            Assert.True(result.ResponseTime.TotalMilliseconds > 0);
        }

        private Response<SearchResults<KnowledgeDocument>> CreateMockSearchResults(double relevanceScore = 0.90)
        {
            var mockDocument = new KnowledgeDocument
            {
                Id = "test-1",
                Title = "Test Knowledge Item",
                Content = "Test content",
                Category = "Technical",
                Confidence = 0.95
            };

            var mockSearchResult = new Mock<SearchResult<KnowledgeDocument>>();
            mockSearchResult.Setup(x => x.Document).Returns(mockDocument);
            mockSearchResult.Setup(x => x.Score).Returns(relevanceScore);

            var mockResults = new Mock<SearchResults<KnowledgeDocument>>();
            mockResults.Setup(x => x.TotalCount).Returns(1);
            mockResults.Setup(x => x.GetResultsAsync())
                      .Returns(AsyncEnumerable.Repeat(mockSearchResult.Object, 1));

            var mockResponse = new Mock<Response<SearchResults<KnowledgeDocument>>>();
            mockResponse.Setup(x => x.Value).Returns(mockResults.Object);

            return mockResponse.Object;
        }

        private Response<IndexDocumentsResult> CreateMockIndexResult(bool success)
        {
            var mockIndexingResult = new Mock<IndexingResult>();
            mockIndexingResult.Setup(x => x.Succeeded).Returns(success);
            mockIndexingResult.Setup(x => x.ProcessingTime).Returns(100);

            var mockIndexDocumentsResult = new Mock<IndexDocumentsResult>();
            mockIndexDocumentsResult.Setup(x => x.Results)
                                   .Returns(new List<IndexingResult> { mockIndexingResult.Object });

            var mockResponse = new Mock<Response<IndexDocumentsResult>>();
            mockResponse.Setup(x => x.Value).Returns(mockIndexDocumentsResult.Object);

            return mockResponse.Object;
        }
    }
}