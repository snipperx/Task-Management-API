using System.ComponentModel.DataAnnotations;

namespace TaskManagementAPI.Common;

/// <summary>Validates that a nullable DateTime, if supplied, is not in the past (date-level comparison, UTC).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class FutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is DateTime dt) return dt.Date >= DateTime.UtcNow.Date;
        return false;
    }

    public override string FormatErrorMessage(string name) => $"{name} cannot be in the past.";
}
