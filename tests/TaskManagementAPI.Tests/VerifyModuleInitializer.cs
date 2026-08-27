using System.Runtime.CompilerServices;
using TaskManagementAPI.Common;
using VerifyTests;

namespace TaskManagementAPI.Tests;

internal static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Volatile fields that would change every run.
        VerifierSettings.IgnoreMember<ErrorResponse>(e => e.CorrelationId);
        VerifierSettings.IgnoreMember<ErrorResponse>(e => e.Timestamp);
    }
}
