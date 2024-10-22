namespace Moq.Analyzers.Common;

#pragma warning disable ECS0200 // Consider using readonly instead of const for flexibility
#pragma warning disable ECS0600 // Avoid stringly-typed APIs

internal static class WellKnownTypeNames
{
    internal const string Moq = "Moq";
    internal const string MockName = "Mock";
    internal const string MockBehavior = "MockBehavior";
    internal const string MockFactory = "MockFactory";
    internal const string MoqMock = $"{Moq}.{MockName}";
    internal const string MoqMock1 = $"{MoqMock}`1";
    internal const string MoqBehavior = $"{Moq}.{MockBehavior}";
    internal const string MoqRepository = $"{Moq}.MockRepository";
    internal const string As = "As";
    internal const string Create = "Create";
    internal const string Of = "Of";
    internal const string Loose = "Loose";
    internal const string Strict = "Strict";
    internal const string Default = "Default";
}
