namespace TaskManagementAPI.Common;

public class ErrorResponse
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
}
