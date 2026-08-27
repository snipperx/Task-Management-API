namespace TaskManagementAPI.Common;

/// <summary>Base class for all expected/handled application errors.</summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }
    protected AppException(string message) : base(message) { }
}

/// <summary>404 - a requested resource does not exist.</summary>
public sealed class NotFoundException : AppException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string resource, object key)
        : base($"{resource} with id '{key}' was not found.") { }
}

/// <summary>400 - the request payload failed validation.</summary>
public sealed class ValidationException : AppException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
        => Errors = errors;
}

/// <summary>401 - the caller could not be authenticated.</summary>
public sealed class UnauthorizedException : AppException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public UnauthorizedException(string message = "Authentication is required.") : base(message) { }
}

/// <summary>403 - the caller is authenticated but not allowed to perform the action.</summary>
public sealed class ForbiddenException : AppException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public ForbiddenException(string message = "You are not allowed to perform this action.")
        : base(message) { }
}

/// <summary>409 - the request violates a domain/business rule.</summary>
public sealed class BusinessRuleException : AppException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public BusinessRuleException(string message) : base(message) { }
}
