using System.Text.Json.Serialization;

namespace AutoResolveServiceDesk.Integration.Contracts
{
    /// <summary>
    /// Shared data contract for ticket communication between agents
    /// Ensures consistent data format across Triage and Knowledge agents
    /// </summary>
    public class TicketDataContract
    {
        [JsonPropertyName("ticketId")]
        public string TicketId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public TicketPriority Priority { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("submittedAt")]
        public DateTime SubmittedAt { get; set; }

        [JsonPropertyName("processedAt")]
        public DateTime? ProcessedAt { get; set; }

        [JsonPropertyName("knowledgeResults")]
        public List<KnowledgeResult> KnowledgeResults { get; set; } = new();

        [JsonPropertyName("status")]
        public TicketStatus Status { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class KnowledgeResult
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("relevanceScore")]
        public double RelevanceScore { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public enum TicketPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum TicketStatus
    {
        Submitted,
        Triaged,
        KnowledgeEnriched,
        InProgress,
        Resolved,
        Closed
    }
}