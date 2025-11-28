using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AutoResolveServiceDesk.Integration.Contracts;

namespace AutoResolveServiceDesk.Integration
{
    /// <summary>
    /// Manages Service Bus communication between agents with retry policies and error handling
    /// Handles message routing from Triage Agent to Knowledge Agent
    /// </summary>
    public class ServiceBusIntegrationManager : IDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSender _triageToKnowledgeSender;
        private readonly ServiceBusProcessor _knowledgeProcessor;
        private readonly ILogger<ServiceBusIntegrationManager> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ServiceBusIntegrationManager(
            IConfiguration configuration,
            ILogger<ServiceBusIntegrationManager> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var connectionString = configuration.GetConnectionString("ServiceBus");
            _client = new ServiceBusClient(connectionString);
            
            _triageToKnowledgeSender = _client.CreateSender("triage-to-knowledge");
            _knowledgeProcessor = _client.CreateProcessor("knowledge-requests", new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 10,
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

            _knowledgeProcessor.ProcessMessageAsync += ProcessKnowledgeRequestAsync;
            _knowledgeProcessor.ProcessErrorAsync += ProcessErrorAsync;
        }

        /// <summary>
        /// Routes ticket from Triage Agent to Knowledge Agent for enrichment
        /// </summary>
        public async Task<bool> RouteTicketToKnowledgeAsync(TicketDataContract ticket)
        {
            try
            {
                _logger.LogInformation("Routing ticket {TicketId} to Knowledge Agent", ticket.TicketId);

                var messageBody = JsonSerializer.Serialize(ticket, _jsonOptions);
                var message = new ServiceBusMessage(messageBody)
                {
                    MessageId = ticket.TicketId,
                    CorrelationId = ticket.TicketId,
                    Subject = "KnowledgeEnrichment",
                    TimeToLive = TimeSpan.FromMinutes(30)
                };

                message.ApplicationProperties["Priority"] = ticket.Priority.ToString();
                message.ApplicationProperties["Category"] = ticket.Category;
                message.ApplicationProperties["SubmittedAt"] = ticket.SubmittedAt.ToString("O");

                await _triageToKnowledgeSender.SendMessageAsync(message);
                
                _logger.LogInformation("Successfully routed ticket {TicketId} to Knowledge Agent", ticket.TicketId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to route ticket {TicketId} to Knowledge Agent", ticket.TicketId);
                return false;
            }
        }

        /// <summary>
        /// Processes knowledge enrichment requests with retry logic
        /// </summary>
        private async Task ProcessKnowledgeRequestAsync(ProcessMessageEventArgs args)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var messageBody = args.Message.Body.ToString();
                var ticket = JsonSerializer.Deserialize<TicketDataContract>(messageBody, _jsonOptions);

                if (ticket == null)
                {
                    _logger.LogWarning("Received invalid ticket message: {MessageId}", args.Message.MessageId);
                    await args.DeadLetterMessageAsync(args.Message, "InvalidMessage", "Unable to deserialize ticket data");
                    return;
                }

                _logger.LogInformation("Processing knowledge request for ticket {TicketId}", ticket.TicketId);

                // Simulate knowledge enrichment (replace with actual Knowledge Agent call)
                ticket.Status = TicketStatus.KnowledgeEnriched;
                ticket.ProcessedAt = DateTime.UtcNow;
                ticket.KnowledgeResults = await SimulateKnowledgeEnrichmentAsync(ticket);

                // Update ticket status and complete message
                await args.CompleteMessageAsync(args.Message);
                
                stopwatch.Stop();
                _logger.LogInformation("Completed knowledge enrichment for ticket {TicketId} in {ElapsedMs}ms", 
                    ticket.TicketId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error processing knowledge request for message {MessageId} after {ElapsedMs}ms", 
                    args.Message.MessageId, stopwatch.ElapsedMilliseconds);

                // Implement retry logic
                if (args.Message.DeliveryCount < 3)
                {
                    await args.AbandonMessageAsync(args.Message);
                }
                else
                {
                    await args.DeadLetterMessageAsync(args.Message, "ProcessingFailed", ex.Message);
                }
            }
        }

        /// <summary>
        /// Simulates knowledge enrichment (replace with actual Knowledge Agent integration)
        /// </summary>
        private async Task<List<KnowledgeResult>> SimulateKnowledgeEnrichmentAsync(TicketDataContract ticket)
        {
            await Task.Delay(100); // Simulate processing time

            return new List<KnowledgeResult>
            {
                new KnowledgeResult
                {
                    Title = $"Solution for {ticket.Category}",
                    Content = $"Recommended solution for ticket: {ticket.Title}",
                    RelevanceScore = 0.85,
                    Source = "Knowledge Base"
                }
            };
        }

        /// <summary>
        /// Handles Service Bus processing errors
        /// </summary>
        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus error in {Source}: {ErrorSource}", 
                args.ErrorSource, args.Exception.Message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts the message processing
        /// </summary>
        public async Task StartProcessingAsync()
        {
            await _knowledgeProcessor.StartProcessingAsync();
            _logger.LogInformation("Service Bus integration manager started processing");
        }

        /// <summary>
        /// Stops the message processing
        /// </summary>
        public async Task StopProcessingAsync()
        {
            await _knowledgeProcessor.StopProcessingAsync();
            _logger.LogInformation("Service Bus integration manager stopped processing");
        }

        /// <summary>
        /// Health check for Service Bus connectivity
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                // Simple connectivity test
                var testMessage = new ServiceBusMessage("health-check")
                {
                    TimeToLive = TimeSpan.FromSeconds(30)
                };

                await _triageToKnowledgeSender.SendMessageAsync(testMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service Bus health check failed");
                return false;
            }
        }

        public void Dispose()
        {
            _triageToKnowledgeSender?.DisposeAsync().AsTask().Wait();
            _knowledgeProcessor?.DisposeAsync().AsTask().Wait();
            _client?.DisposeAsync().AsTask().Wait();
        }
    }
}