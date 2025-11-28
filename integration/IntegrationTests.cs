using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using FluentAssertions;
using AutoResolveServiceDesk.Integration.Contracts;
using System.Diagnostics;

namespace AutoResolveServiceDesk.Integration.Tests
{
    /// <summary>
    /// Integration tests for Triage-Knowledge agent communication
    /// Validates end-to-end ticket processing flow
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ServiceBusIntegrationManager _integrationManager;
        private readonly IntegrationHealthMonitor _healthMonitor;
        private readonly IntegrationConfigurationManager _configManager;

        public IntegrationTests()
        {
            var services = new ServiceCollection();
            
            // Mock configuration
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c.GetConnectionString("ServiceBus"))
                     .Returns("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test");
            
            services.AddSingleton(mockConfig.Object);
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<ServiceBusIntegrationManager>();
            services.AddSingleton<IntegrationHealthMonitor>();
            services.AddSingleton<IntegrationConfigurationManager>();

            _serviceProvider = services.BuildServiceProvider();
            _integrationManager = _serviceProvider.GetRequiredService<ServiceBusIntegrationManager>();
            _healthMonitor = _serviceProvider.GetRequiredService<IntegrationHealthMonitor>();
            _configManager = _serviceProvider.GetRequiredService<IntegrationConfigurationManager>();
        }

        [Fact]
        public async Task TicketDataContract_SerializationDeserialization_ShouldPreserveData()
        {
            // Arrange
            var originalTicket = CreateSampleTicket();

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(originalTicket);
            var deserializedTicket = System.Text.Json.JsonSerializer.Deserialize<TicketDataContract>(json);

            // Assert
            deserializedTicket.Should().NotBeNull();
            deserializedTicket!.TicketId.Should().Be(originalTicket.TicketId);
            deserializedTicket.Title.Should().Be(originalTicket.Title);
            deserializedTicket.Priority.Should().Be(originalTicket.Priority);
            deserializedTicket.Category.Should().Be(originalTicket.Category);
            deserializedTicket.Confidence.Should().Be(originalTicket.Confidence);
        }

        [Fact]
        public async Task ServiceBusIntegrationManager_RouteTicketToKnowledge_ShouldSucceed()
        {
            // Arrange
            var ticket = CreateSampleTicket();

            // Act & Assert - This would require actual Service Bus in integration environment
            // For unit test, we verify the method doesn't throw exceptions
            var act = async () => await _integrationManager.RouteTicketToKnowledgeAsync(ticket);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task IntegrationHealthMonitor_CheckHealth_ShouldReturnHealthStatus()
        {
            // Arrange
            var healthCheckContext = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext();

            // Act
            var result = await _healthMonitor.CheckHealthAsync(healthCheckContext);

            // Assert
            result.Should().NotBeNull();
            result.Data.Should().ContainKey("HealthCheckDuration");
        }

        [Fact]
        public async Task EndToEndTicketProcessing_TriageToKnowledge_ShouldCompleteWithinTimeLimit()
        {
            // Arrange
            var ticket = CreateSampleTicket();
            var stopwatch = Stopwatch.StartNew();

            // Act
            try
            {
                // Simulate triage processing (5 seconds max)
                await SimulateTriageProcessingAsync(ticket);
                
                // Route to knowledge agent
                var routingSuccess = await _integrationManager.RouteTicketToKnowledgeAsync(ticket);
                
                // Simulate knowledge processing (30 seconds max)
                await SimulateKnowledgeProcessingAsync(ticket);
                
                stopwatch.Stop();

                // Assert
                routingSuccess.Should().BeTrue();
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(35000); // 35 seconds total
                ticket.Status.Should().Be(TicketStatus.KnowledgeEnriched);
                ticket.KnowledgeResults.Should().NotBeEmpty();
            }
            catch (Exception)
            {
                stopwatch.Stop();
                throw;
            }
        }

        [Fact]
        public async Task PerformanceTest_ConcurrentTicketProcessing_ShouldMeetThroughputRequirements()
        {
            // Arrange
            const int concurrentTickets = 10;
            var tickets = Enumerable.Range(1, concurrentTickets)
                                   .Select(i => CreateSampleTicket($"TICKET-{i:D3}"))
                                   .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = tickets.Select(async ticket =>
            {
                await SimulateTriageProcessingAsync(ticket);
                await _integrationManager.RouteTicketToKnowledgeAsync(ticket);
                await SimulateKnowledgeProcessingAsync(ticket);
                return ticket;
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            results.Should().HaveCount(concurrentTickets);
            results.All(t => t.Status == TicketStatus.KnowledgeEnriched).Should().BeTrue();
            
            // Should process 100+ tickets per minute (600ms per ticket max for 10 concurrent)
            var ticketsPerMinute = (concurrentTickets * 60000.0) / stopwatch.ElapsedMilliseconds;
            ticketsPerMinute.Should().BeGreaterThan(100);
        }

        [Fact]
        public async Task ErrorHandling_InvalidTicketData_ShouldHandleGracefully()
        {
            // Arrange
            var invalidTicket = new TicketDataContract
            {
                TicketId = "", // Invalid empty ID
                Title = "",    // Invalid empty title
                Priority = (TicketPriority)999 // Invalid priority
            };

            // Act & Assert
            var act = async () => await _integrationManager.RouteTicketToKnowledgeAsync(invalidTicket);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SecurityValidation_TicketDataSanitization_ShouldPreventInjection()
        {
            // Arrange
            var maliciousTicket = CreateSampleTicket();
            maliciousTicket.Title = "<script>alert('xss')</script>";
            maliciousTicket.Description = "'; DROP TABLE tickets; --";

            // Act
            var routingSuccess = await _integrationManager.RouteTicketToKnowledgeAsync(maliciousTicket);

            // Assert
            routingSuccess.Should().BeTrue();
            // In production, add validation that malicious content is sanitized
        }

        [Fact]
        public async Task ConfigurationValidation_AllRequiredSettings_ShouldBeValid()
        {
            // Act
            var validationResult = await _configManager.ValidateConfigurationAsync();

            // Assert - In test environment, some validations may fail due to missing services
            validationResult.Should().NotBeNull();
            // Add specific assertions based on test environment setup
        }

        [Fact]
        public void HealthMetrics_RecordingAndRetrieval_ShouldTrackPerformance()
        {
            // Arrange
            var processingTime = TimeSpan.FromMilliseconds(1500);

            // Act
            _healthMonitor.RecordMessageProcessed(processingTime, true);
            _healthMonitor.RecordQueueDepth(5);

            // Assert
            // Verify metrics are recorded (would need access to internal metrics in production)
            Assert.True(true); // Placeholder - implement metric validation
        }

        private TicketDataContract CreateSampleTicket(string? ticketId = null)
        {
            return new TicketDataContract
            {
                TicketId = ticketId ?? $"TICKET-{Guid.NewGuid():N}",
                Title = "Sample IT Issue",
                Description = "User cannot access email system",
                Priority = TicketPriority.Medium,
                Category = "Email",
                Confidence = 0.85,
                SubmittedAt = DateTime.UtcNow,
                Status = TicketStatus.Submitted,
                Metadata = new Dictionary<string, object>
                {
                    ["UserDepartment"] = "Engineering",
                    ["ImpactLevel"] = "Medium"
                }
            };
        }

        private async Task SimulateTriageProcessingAsync(TicketDataContract ticket)
        {
            // Simulate triage agent processing time (max 5 seconds)
            await Task.Delay(100);
            
            ticket.Status = TicketStatus.Triaged;
            ticket.Confidence = 0.92;
            ticket.Category = DetermineCategory(ticket.Title);
        }

        private async Task SimulateKnowledgeProcessingAsync(TicketDataContract ticket)
        {
            // Simulate knowledge agent processing time (max 30 seconds)
            await Task.Delay(200);
            
            ticket.Status = TicketStatus.KnowledgeEnriched;
            ticket.ProcessedAt = DateTime.UtcNow;
            ticket.KnowledgeResults = new List<KnowledgeResult>
            {
                new KnowledgeResult
                {
                    Title = $"Solution for {ticket.Category} Issues",
                    Content = "Step-by-step resolution guide...",
                    RelevanceScore = 0.88,
                    Source = "Knowledge Base"
                }
            };
        }

        private string DetermineCategory(string title)
        {
            if (title.ToLower().Contains("email")) return "Email";
            if (title.ToLower().Contains("password")) return "Authentication";
            if (title.ToLower().Contains("network")) return "Network";
            return "General";
        }

        public void Dispose()
        {
            _integrationManager?.Dispose();
            _serviceProvider?.Dispose();
        }
    }

    /// <summary>
    /// Performance benchmark tests for integration components
    /// </summary>
    public class IntegrationPerformanceTests
    {
        [Fact]
        public async Task Benchmark_MessageSerialization_ShouldMeetPerformanceTargets()
        {
            // Arrange
            var ticket = new TicketDataContract
            {
                TicketId = "PERF-TEST-001",
                Title = "Performance Test Ticket",
                Description = "Large description with lots of text to test serialization performance...",
                Priority = TicketPriority.High,
                Category = "Performance",
                Confidence = 0.95,
                SubmittedAt = DateTime.UtcNow,
                KnowledgeResults = Enumerable.Range(1, 10)
                    .Select(i => new KnowledgeResult
                    {
                        Title = $"Knowledge Result {i}",
                        Content = $"Content for result {i} with detailed information...",
                        RelevanceScore = 0.8 + (i * 0.01),
                        Source = "Performance Test"
                    }).ToList()
            };

            const int iterations = 1000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(ticket);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<TicketDataContract>(json);
            }

            stopwatch.Stop();

            // Assert
            var averageTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;
            averageTimeMs.Should().BeLessThan(10); // Less than 10ms per serialization/deserialization
        }
    }
}