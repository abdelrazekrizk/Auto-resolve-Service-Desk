using Microsoft.AspNetCore.Mvc;

namespace AutomationAgent.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class AutomationController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            agent = "automation",
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteWorkflow([FromBody] WorkflowRequest request)
    {
        // Mock workflow execution
        var workflowId = $"WF-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        
        var result = new
        {
            workflowId = workflowId,
            ticketId = request.TicketId,
            workflowType = request.WorkflowType,
            status = "completed",
            steps = GetWorkflowSteps(request.WorkflowType),
            executionTime = "3.2s",
            timestamp = DateTime.UtcNow.ToString("O")
        };

        return Ok(result);
    }

    private List<object> GetWorkflowSteps(string workflowType)
    {
        return workflowType.ToLower() switch
        {
            "password_reset" => new List<object>
            {
                new { step = 1, action = "Validate user identity", status = "completed" },
                new { step = 2, action = "Generate temporary password", status = "completed" },
                new { step = 3, action = "Send password via email", status = "completed" },
                new { step = 4, action = "Update user record", status = "completed" }
            },
            "software_update" => new List<object>
            {
                new { step = 1, action = "Check current version", status = "completed" },
                new { step = 2, action = "Download update package", status = "completed" },
                new { step = 3, action = "Install update", status = "completed" },
                new { step = 4, action = "Verify installation", status = "completed" }
            },
            _ => new List<object>
            {
                new { step = 1, action = "Execute generic workflow", status = "completed" }
            }
        };
    }
}

public class WorkflowRequest
{
    public string TicketId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}