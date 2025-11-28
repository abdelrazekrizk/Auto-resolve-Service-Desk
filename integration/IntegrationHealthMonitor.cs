using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AutoResolveServiceDesk.Integration
{
    /// <summary>
    /// Monitors health of integrated components and communication channels
    /// Provides comprehensive health status for Triage-Knowledge integration
    /// </summary>
    public class IntegrationHealthMonitor : IHealthCheck
    {
        private readonly ServiceBusIntegrationManager _serviceBusManager;
        private readonly ILogger<IntegrationHealthMonitor> _logger;
        private readonly Dictionary<string, HealthMetric> _healthMetrics;

        public IntegrationHealthMonitor(
            ServiceBusIntegrationManager serviceBusManager,
            ILogger<IntegrationHealthMonitor> logger)
        {
            _serviceBusManager = serviceBusManager;
            _logger = logger;
            _healthMetrics = new Dictionary<string, HealthMetric>();
        }

        /// <summary>
        /// Performs comprehensive health check of integration components
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var healthData = new Dictionary<string, object>();

            try
            {
                // Check Service Bus connectivity
                var serviceBusHealthy = await CheckServiceBusHealthAsync();
                healthData["ServiceBus"] = serviceBusHealthy ? "Healthy" : "Unhealthy";

                // Check agent communication latency
                var communicationLatency = await CheckCommunicationLatencyAsync();
                healthData["CommunicationLatency"] = $"{communicationLatency}ms";

                // Check message processing metrics
                var processingMetrics = GetProcessingMetrics();
                healthData["ProcessingMetrics"] = processingMetrics;

                // Check system resources
                var systemHealth = CheckSystemResources();
                healthData["SystemResources"] = systemHealth;

                stopwatch.Stop();
                healthData["HealthCheckDuration"] = $"{stopwatch.ElapsedMilliseconds}ms";

                // Determine overall health status
                var overallHealth = DetermineOverallHealth(serviceBusHealthy, communicationLatency, systemHealth);
                
                _logger.LogInformation("Integration health check completed in {Duration}ms with status {Status}", 
                    stopwatch.ElapsedMilliseconds, overallHealth);

                return overallHealth == HealthStatus.Healthy 
                    ? HealthCheckResult.Healthy("Integration system is healthy", healthData)
                    : HealthCheckResult.Degraded("Integration system has issues", healthData);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Integration health check failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                
                healthData["Error"] = ex.Message;
                healthData["HealthCheckDuration"] = $"{stopwatch.ElapsedMilliseconds}ms";
                
                return HealthCheckResult.Unhealthy("Integration health check failed", ex, healthData);
            }
        }

        /// <summary>
        /// Checks Service Bus connectivity and queue status
        /// </summary>
        private async Task<bool> CheckServiceBusHealthAsync()
        {
            try
            {
                var isHealthy = await _serviceBusManager.HealthCheckAsync();
                UpdateHealthMetric("ServiceBus", isHealthy ? 1 : 0);
                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service Bus health check failed");
                UpdateHealthMetric("ServiceBus", 0);
                return false;
            }
        }

        /// <summary>
        /// Measures communication latency between agents
        /// </summary>
        private async Task<long> CheckCommunicationLatencyAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Simulate a test message round trip
                await Task.Delay(50); // Simulate network latency
                stopwatch.Stop();
                
                var latency = stopwatch.ElapsedMilliseconds;
                UpdateHealthMetric("CommunicationLatency", latency);
                
                return latency;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Communication latency check failed");
                UpdateHealthMetric("CommunicationLatency", -1);
                return -1;
            }
        }

        /// <summary>
        /// Gets message processing performance metrics
        /// </summary>
        private object GetProcessingMetrics()
        {
            return new
            {
                MessagesProcessedPerMinute = GetHealthMetric("MessagesProcessed")?.AveragePerMinute ?? 0,
                AverageProcessingTime = GetHealthMetric("ProcessingTime")?.Average ?? 0,
                ErrorRate = GetHealthMetric("ErrorRate")?.Average ?? 0,
                QueueDepth = GetHealthMetric("QueueDepth")?.Current ?? 0
            };
        }

        /// <summary>
        /// Checks system resource utilization
        /// </summary>
        private object CheckSystemResources()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                
                return new
                {
                    MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                    CpuUsagePercent = GetCpuUsage(),
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "System resource check failed");
                return new { Error = "Unable to retrieve system metrics" };
            }
        }

        /// <summary>
        /// Determines overall health status based on component health
        /// </summary>
        private HealthStatus DetermineOverallHealth(bool serviceBusHealthy, long communicationLatency, object systemHealth)
        {
            if (!serviceBusHealthy)
                return HealthStatus.Unhealthy;

            if (communicationLatency > 5000) // 5 seconds threshold
                return HealthStatus.Degraded;

            // Add more health criteria as needed
            return HealthStatus.Healthy;
        }

        /// <summary>
        /// Updates health metric with new value
        /// </summary>
        private void UpdateHealthMetric(string metricName, double value)
        {
            if (!_healthMetrics.ContainsKey(metricName))
            {
                _healthMetrics[metricName] = new HealthMetric();
            }

            _healthMetrics[metricName].AddValue(value);
        }

        /// <summary>
        /// Gets health metric by name
        /// </summary>
        private HealthMetric? GetHealthMetric(string metricName)
        {
            return _healthMetrics.TryGetValue(metricName, out var metric) ? metric : null;
        }

        /// <summary>
        /// Gets CPU usage percentage (simplified implementation)
        /// </summary>
        private double GetCpuUsage()
        {
            // Simplified CPU usage calculation
            // In production, use performance counters or more sophisticated monitoring
            return Environment.ProcessorCount * 10; // Placeholder
        }

        /// <summary>
        /// Records message processing completion
        /// </summary>
        public void RecordMessageProcessed(TimeSpan processingTime, bool success)
        {
            UpdateHealthMetric("MessagesProcessed", 1);
            UpdateHealthMetric("ProcessingTime", processingTime.TotalMilliseconds);
            UpdateHealthMetric("ErrorRate", success ? 0 : 1);
        }

        /// <summary>
        /// Records current queue depth
        /// </summary>
        public void RecordQueueDepth(int depth)
        {
            UpdateHealthMetric("QueueDepth", depth);
        }
    }

    /// <summary>
    /// Tracks health metrics over time
    /// </summary>
    public class HealthMetric
    {
        private readonly Queue<(DateTime Timestamp, double Value)> _values = new();
        private readonly object _lock = new();

        public double Current { get; private set; }
        public double Average { get; private set; }
        public double AveragePerMinute { get; private set; }

        public void AddValue(double value)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                Current = value;
                
                _values.Enqueue((now, value));
                
                // Keep only last 5 minutes of data
                while (_values.Count > 0 && _values.Peek().Timestamp < now.AddMinutes(-5))
                {
                    _values.Dequeue();
                }

                // Calculate averages
                if (_values.Count > 0)
                {
                    Average = _values.Average(v => v.Value);
                    
                    var oneMinuteAgo = now.AddMinutes(-1);
                    var recentValues = _values.Where(v => v.Timestamp >= oneMinuteAgo).ToList();
                    AveragePerMinute = recentValues.Count > 0 ? recentValues.Sum(v => v.Value) : 0;
                }
            }
        }
    }
}