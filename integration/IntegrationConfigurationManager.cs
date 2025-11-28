using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoResolveServiceDesk.Integration
{
    /// <summary>
    /// Manages configuration and secrets for integration components
    /// Provides secure access to connection strings and settings
    /// </summary>
    public class IntegrationConfigurationManager
    {
        private readonly IConfiguration _configuration;
        private readonly SecretClient? _keyVaultClient;
        private readonly ILogger<IntegrationConfigurationManager> _logger;
        private readonly Dictionary<string, string> _cachedSecrets;

        public IntegrationConfigurationManager(
            IConfiguration configuration,
            ILogger<IntegrationConfigurationManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _cachedSecrets = new Dictionary<string, string>();

            // Initialize Key Vault client if configured
            var keyVaultUrl = _configuration["KeyVault:Url"];
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                try
                {
                    _keyVaultClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
                    _logger.LogInformation("Key Vault client initialized for {KeyVaultUrl}", keyVaultUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize Key Vault client for {KeyVaultUrl}", keyVaultUrl);
                }
            }
        }

        /// <summary>
        /// Gets Service Bus connection string with fallback to Key Vault
        /// </summary>
        public async Task<string> GetServiceBusConnectionStringAsync()
        {
            return await GetSecretAsync("ServiceBus:ConnectionString", "ServiceBus-ConnectionString");
        }

        /// <summary>
        /// Gets Redis connection string with fallback to Key Vault
        /// </summary>
        public async Task<string> GetRedisConnectionStringAsync()
        {
            return await GetSecretAsync("Redis:ConnectionString", "Redis-ConnectionString");
        }

        /// <summary>
        /// Gets Azure Search connection details
        /// </summary>
        public async Task<AzureSearchConfig> GetAzureSearchConfigAsync()
        {
            return new AzureSearchConfig
            {
                Endpoint = await GetSecretAsync("AzureSearch:Endpoint", "AzureSearch-Endpoint"),
                ApiKey = await GetSecretAsync("AzureSearch:ApiKey", "AzureSearch-ApiKey"),
                IndexName = _configuration["AzureSearch:IndexName"] ?? "knowledge-base"
            };
        }

        /// <summary>
        /// Gets integration performance settings
        /// </summary>
        public IntegrationPerformanceConfig GetPerformanceConfig()
        {
            return new IntegrationPerformanceConfig
            {
                MaxConcurrentMessages = _configuration.GetValue<int>("Integration:MaxConcurrentMessages", 10),
                MessageTimeoutSeconds = _configuration.GetValue<int>("Integration:MessageTimeoutSeconds", 300),
                RetryAttempts = _configuration.GetValue<int>("Integration:RetryAttempts", 3),
                RetryDelaySeconds = _configuration.GetValue<int>("Integration:RetryDelaySeconds", 5),
                HealthCheckIntervalSeconds = _configuration.GetValue<int>("Integration:HealthCheckIntervalSeconds", 30),
                MaxProcessingTimeSeconds = _configuration.GetValue<int>("Integration:MaxProcessingTimeSeconds", 35)
            };
        }

        /// <summary>
        /// Gets monitoring and logging configuration
        /// </summary>
        public MonitoringConfig GetMonitoringConfig()
        {
            return new MonitoringConfig
            {
                ApplicationInsightsConnectionString = _configuration.GetConnectionString("ApplicationInsights") ?? string.Empty,
                LogLevel = _configuration.GetValue<string>("Logging:LogLevel:Default", "Information"),
                EnableDetailedLogging = _configuration.GetValue<bool>("Monitoring:EnableDetailedLogging", false),
                EnablePerformanceCounters = _configuration.GetValue<bool>("Monitoring:EnablePerformanceCounters", true),
                MetricsRetentionDays = _configuration.GetValue<int>("Monitoring:MetricsRetentionDays", 30)
            };
        }

        /// <summary>
        /// Gets security configuration settings
        /// </summary>
        public SecurityConfig GetSecurityConfig()
        {
            return new SecurityConfig
            {
                EnableEncryptionInTransit = _configuration.GetValue<bool>("Security:EnableEncryptionInTransit", true),
                EnableManagedIdentity = _configuration.GetValue<bool>("Security:EnableManagedIdentity", true),
                TokenExpirationMinutes = _configuration.GetValue<int>("Security:TokenExpirationMinutes", 60),
                EnableAuditLogging = _configuration.GetValue<bool>("Security:EnableAuditLogging", true),
                AllowedOrigins = _configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>()
            };
        }

        /// <summary>
        /// Gets a secret from configuration or Key Vault
        /// </summary>
        private async Task<string> GetSecretAsync(string configKey, string keyVaultSecretName)
        {
            // Check cache first
            if (_cachedSecrets.TryGetValue(configKey, out var cachedValue))
            {
                return cachedValue;
            }

            // Try configuration first
            var configValue = _configuration[configKey];
            if (!string.IsNullOrEmpty(configValue))
            {
                _cachedSecrets[configKey] = configValue;
                return configValue;
            }

            // Try Key Vault if available
            if (_keyVaultClient != null)
            {
                try
                {
                    var secret = await _keyVaultClient.GetSecretAsync(keyVaultSecretName);
                    var secretValue = secret.Value.Value;
                    
                    _cachedSecrets[configKey] = secretValue;
                    _logger.LogDebug("Retrieved secret {SecretName} from Key Vault", keyVaultSecretName);
                    
                    return secretValue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve secret {SecretName} from Key Vault", keyVaultSecretName);
                }
            }

            _logger.LogError("Unable to retrieve configuration value for {ConfigKey}", configKey);
            throw new InvalidOperationException($"Configuration value '{configKey}' not found in configuration or Key Vault");
        }

        /// <summary>
        /// Validates all required configuration values
        /// </summary>
        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync()
        {
            var result = new ConfigurationValidationResult();

            try
            {
                // Validate Service Bus connection
                var serviceBusConnection = await GetServiceBusConnectionStringAsync();
                result.ServiceBusConfigValid = !string.IsNullOrEmpty(serviceBusConnection);

                // Validate Redis connection
                var redisConnection = await GetRedisConnectionStringAsync();
                result.RedisConfigValid = !string.IsNullOrEmpty(redisConnection);

                // Validate Azure Search config
                var searchConfig = await GetAzureSearchConfigAsync();
                result.AzureSearchConfigValid = !string.IsNullOrEmpty(searchConfig.Endpoint) && 
                                               !string.IsNullOrEmpty(searchConfig.ApiKey);

                // Validate performance config
                var perfConfig = GetPerformanceConfig();
                result.PerformanceConfigValid = perfConfig.MaxConcurrentMessages > 0 && 
                                               perfConfig.MessageTimeoutSeconds > 0;

                result.IsValid = result.ServiceBusConfigValid && 
                                result.RedisConfigValid && 
                                result.AzureSearchConfigValid && 
                                result.PerformanceConfigValid;

                _logger.LogInformation("Configuration validation completed. Valid: {IsValid}", result.IsValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration validation failed");
                result.ValidationError = ex.Message;
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Clears cached secrets (useful for testing or secret rotation)
        /// </summary>
        public void ClearSecretCache()
        {
            _cachedSecrets.Clear();
            _logger.LogInformation("Secret cache cleared");
        }
    }

    public class AzureSearchConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
    }

    public class IntegrationPerformanceConfig
    {
        public int MaxConcurrentMessages { get; set; }
        public int MessageTimeoutSeconds { get; set; }
        public int RetryAttempts { get; set; }
        public int RetryDelaySeconds { get; set; }
        public int HealthCheckIntervalSeconds { get; set; }
        public int MaxProcessingTimeSeconds { get; set; }
    }

    public class MonitoringConfig
    {
        public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public bool EnableDetailedLogging { get; set; }
        public bool EnablePerformanceCounters { get; set; }
        public int MetricsRetentionDays { get; set; }
    }

    public class SecurityConfig
    {
        public bool EnableEncryptionInTransit { get; set; }
        public bool EnableManagedIdentity { get; set; }
        public int TokenExpirationMinutes { get; set; }
        public bool EnableAuditLogging { get; set; }
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }

    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public bool ServiceBusConfigValid { get; set; }
        public bool RedisConfigValid { get; set; }
        public bool AzureSearchConfigValid { get; set; }
        public bool PerformanceConfigValid { get; set; }
        public string? ValidationError { get; set; }
    }
}