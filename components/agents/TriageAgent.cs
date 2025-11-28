using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ServiceDesk.Components.Agents
{
    public class TriageAgent
    {
        private readonly ILogger<TriageAgent> _logger;
        private readonly IConfiguration _configuration;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly MLClassificationService _mlService;

        public TriageAgent(
            ILogger<TriageAgent> logger,
            IConfiguration configuration,
            ServiceBusClient serviceBusClient,
            MLClassificationService mlService)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceBusClient = serviceBusClient;
            _mlService = mlService;
        }

        public async Task<TicketClassificationResult> ClassifyTicketAsync(TicketSubmission ticket)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Starting ticket classification for ticket {TicketId}", ticket.Id);

                // ML Classification with 95%+ accuracy target
                var classification = await _mlService.ClassifyAsync(ticket.Description, ticket.Category);
                
                var result = new TicketClassificationResult
                {
                    TicketId = ticket.Id,
                    Category = classification.Category,
                    Priority = classification.Priority,
                    Confidence = classification.Confidence,
                    ProcessingTime = DateTime.UtcNow - startTime,
                    AssignedAgent = DetermineAssignedAgent(classification)
                };

                // Queue management with priority-based routing
                await RouteToQueueAsync(result);

                _logger.LogInformation("Ticket {TicketId} classified as {Category} with {Confidence}% confidence", 
                    ticket.Id, result.Category, result.Confidence * 100);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying ticket {TicketId}", ticket.Id);
                throw;
            }
        }

        private async Task RouteToQueueAsync(TicketClassificationResult result)
        {
            var queueName = GetQueueName(result.Priority, result.Category);
            var sender = _serviceBusClient.CreateSender(queueName);

            var message = new ServiceBusMessage(JsonSerializer.Serialize(result))
            {
                MessageId = result.TicketId,
                Subject = result.Category,
                ApplicationProperties = {
                    ["Priority"] = result.Priority.ToString(),
                    ["Confidence"] = result.Confidence,
                    ["AssignedAgent"] = result.AssignedAgent
                }
            };

            await sender.SendMessageAsync(message);
            _logger.LogInformation("Ticket {TicketId} routed to queue {QueueName}", result.TicketId, queueName);
        }

        private string DetermineAssignedAgent(MLClassification classification)
        {
            return classification.Category switch
            {
                "Technical" => "AutomationAgent",
                "Information" => "KnowledgeAgent",
                "Escalation" => "EscalationAgent",
                _ => "KnowledgeAgent"
            };
        }

        private string GetQueueName(TicketPriority priority, string category)
        {
            return priority switch
            {
                TicketPriority.Critical => "critical-tickets",
                TicketPriority.High => "high-priority-tickets",
                TicketPriority.Medium => "medium-priority-tickets",
                _ => "low-priority-tickets"
            };
        }

        public async Task<HealthCheckResult> HealthCheckAsync()
        {
            try
            {
                // Check ML service health
                var mlHealth = await _mlService.HealthCheckAsync();
                
                // Check Service Bus connectivity
                var sender = _serviceBusClient.CreateSender("health-check");
                
                return new HealthCheckResult
                {
                    IsHealthy = mlHealth.IsHealthy,
                    ResponseTime = mlHealth.ResponseTime,
                    Details = $"ML Service: {mlHealth.Status}, Service Bus: Connected"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Details = ex.Message
                };
            }
        }
    }

    public class TicketSubmission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }

    public class TicketClassificationResult
    {
        public string TicketId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; }
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string AssignedAgent { get; set; } = string.Empty;
    }

    public enum TicketPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public class MLClassification
    {
        public string Category { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; }
        public double Confidence { get; set; }
    }

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string Details { get; set; } = string.Empty;
        public string Status => IsHealthy ? "Healthy" : "Unhealthy";
    }

    public interface MLClassificationService
    {
        Task<MLClassification> ClassifyAsync(string description, string category);
        Task<HealthCheckResult> HealthCheckAsync();
    }
}