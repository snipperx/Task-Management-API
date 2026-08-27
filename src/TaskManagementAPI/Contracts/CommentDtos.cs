using System.ComponentModel.DataAnnotations;

namespace TaskManagementAPI.Contracts;

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCommentRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public class UpdateCommentRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
