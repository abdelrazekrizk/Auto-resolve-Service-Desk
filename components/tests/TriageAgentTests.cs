using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Azure.Messaging.ServiceBus;
using ServiceDesk.Components.Agents;

namespace ServiceDesk.Components.Tests
{
    public class TriageAgentTests
    {
        private readonly Mock<ILogger<TriageAgent>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ServiceBusClient> _mockServiceBusClient;
        private readonly Mock<MLClassificationService> _mockMLService;
        private readonly TriageAgent _triageAgent;

        public TriageAgentTests()
        {
            _mockLogger = new Mock<ILogger<TriageAgent>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockServiceBusClient = new Mock<ServiceBusClient>();
            _mockMLService = new Mock<MLClassificationService>();
            
            _triageAgent = new TriageAgent(
                _mockLogger.Object,
                _mockConfiguration.Object,
                _mockServiceBusClient.Object,
                _mockMLService.Object);
        }

        [Fact]
        public async Task ClassifyTicketAsync_ShouldReturnValidClassification_WhenTicketIsValid()
        {
            // Arrange
            var ticket = new TicketSubmission
            {
                Id = "TEST-001",
                Description = "Cannot access email system",
                Category = "Technical",
                UserEmail = "user@company.com"
            };

            var mlClassification = new MLClassification
            {
                Category = "Technical",
                Priority = TicketPriority.High,
                Confidence = 0.95
            };

            _mockMLService.Setup(x => x.ClassifyAsync(ticket.Description, ticket.Category))
                         .ReturnsAsync(mlClassification);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                               .Returns(mockSender.Object);

            // Act
            var result = await _triageAgent.ClassifyTicketAsync(ticket);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ticket.Id, result.TicketId);
            Assert.Equal("Technical", result.Category);
            Assert.Equal(TicketPriority.High, result.Priority);
            Assert.Equal(0.95, result.Confidence);
            Assert.Equal("AutomationAgent", result.AssignedAgent);
            Assert.True(result.ProcessingTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task ClassifyTicketAsync_ShouldMeetPerformanceTarget_WhenProcessingTicket()
        {
            // Arrange
            var ticket = new TicketSubmission
            {
                Description = "Password reset request",
                Category = "Information"
            };

            var mlClassification = new MLClassification
            {
                Category = "Information",
                Priority = TicketPriority.Medium,
                Confidence = 0.88
            };

            _mockMLService.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ReturnsAsync(mlClassification);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                               .Returns(mockSender.Object);

            // Act
            var result = await _triageAgent.ClassifyTicketAsync(ticket);

            // Assert - Performance target: <5 seconds
            Assert.True(result.ProcessingTime.TotalSeconds < 5, 
                $"Processing time {result.ProcessingTime.TotalSeconds}s exceeds 5 second target");
        }

        [Theory]
        [InlineData("Technical", "AutomationAgent")]
        [InlineData("Information", "KnowledgeAgent")]
        [InlineData("Escalation", "EscalationAgent")]
        [InlineData("Unknown", "KnowledgeAgent")]
        public async Task ClassifyTicketAsync_ShouldAssignCorrectAgent_BasedOnCategory(
            string category, string expectedAgent)
        {
            // Arrange
            var ticket = new TicketSubmission
            {
                Description = "Test ticket",
                Category = category
            };

            var mlClassification = new MLClassification
            {
                Category = category,
                Priority = TicketPriority.Medium,
                Confidence = 0.90
            };

            _mockMLService.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ReturnsAsync(mlClassification);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                               .Returns(mockSender.Object);

            // Act
            var result = await _triageAgent.ClassifyTicketAsync(ticket);

            // Assert
            Assert.Equal(expectedAgent, result.AssignedAgent);
        }

        [Fact]
        public async Task ClassifyTicketAsync_ShouldMeetAccuracyTarget_WhenConfidenceIsHigh()
        {
            // Arrange
            var ticket = new TicketSubmission
            {
                Description = "System outage - critical issue",
                Category = "Technical"
            };

            var mlClassification = new MLClassification
            {
                Category = "Technical",
                Priority = TicketPriority.Critical,
                Confidence = 0.97 // Above 95% accuracy target
            };

            _mockMLService.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ReturnsAsync(mlClassification);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                               .Returns(mockSender.Object);

            // Act
            var result = await _triageAgent.ClassifyTicketAsync(ticket);

            // Assert - Accuracy target: 95%+
            Assert.True(result.Confidence >= 0.95, 
                $"Confidence {result.Confidence * 100}% is below 95% accuracy target");
        }

        [Fact]
        public async Task HealthCheckAsync_ShouldReturnHealthy_WhenAllServicesAreOperational()
        {
            // Arrange
            var mlHealthResult = new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = TimeSpan.FromMilliseconds(100)
            };

            _mockMLService.Setup(x => x.HealthCheckAsync())
                         .ReturnsAsync(mlHealthResult);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender("health-check"))
                               .Returns(mockSender.Object);

            // Act
            var result = await _triageAgent.HealthCheckAsync();

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Contains("ML Service: Healthy", result.Details);
            Assert.Contains("Service Bus: Connected", result.Details);
        }

        [Fact]
        public async Task ClassifyTicketAsync_ShouldLogInformation_WhenProcessingTicket()
        {
            // Arrange
            var ticket = new TicketSubmission { Id = "LOG-TEST-001" };
            var mlClassification = new MLClassification
            {
                Category = "Technical",
                Priority = TicketPriority.Medium,
                Confidence = 0.85
            };

            _mockMLService.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ReturnsAsync(mlClassification);

            var mockSender = new Mock<ServiceBusSender>();
            _mockServiceBusClient.Setup(x => x.CreateSender(It.IsAny<string>()))
                               .Returns(mockSender.Object);

            // Act
            await _triageAgent.ClassifyTicketAsync(ticket);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting ticket classification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}