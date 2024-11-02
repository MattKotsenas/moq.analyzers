﻿using Microsoft.CodeAnalysis.Operations;

namespace Moq.Analyzers;

/// <summary>
/// Mock should explicitly specify a behavior and not rely on the default.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SetExplicitMockBehaviorAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Moq: Explicitly choose a mock behavior";
    private static readonly LocalizableString Message = "Explicitly choose a mocking behavior instead of relying on the default (Loose) behavior";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.SetExplicitMockBehavior,
        Title,
        Message,
        DiagnosticCategory.Moq,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/moq.analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.SetExplicitMockBehavior}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(RegisterCompilationStartAction);
    }

    private static void RegisterCompilationStartAction(CompilationStartAnalysisContext context)
    {
        MoqKnownSymbols knownSymbols = new(context.Compilation);

        // Ensure Moq is referenced in the compilation
        if (!knownSymbols.IsMockReferenced())
        {
            return;
        }

        // Look for the MockBehavior type and provide it to Analyze to avoid looking it up multiple times.
        if (knownSymbols.MockBehavior is null)
        {
            return;
        }

        context.RegisterOperationAction(context => AnalyzeObjectCreation(context, knownSymbols), OperationKind.ObjectCreation);

        context.RegisterOperationAction(context => AnalyzeInvocation(context, knownSymbols), OperationKind.Invocation);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, MoqKnownSymbols knownSymbols)
    {
        if (context.Operation is not IObjectCreationOperation creation)
        {
            return;
        }

        if (creation.Type is null ||
            creation.Constructor is null ||
            !(creation.Type.IsInstanceOf(knownSymbols.Mock1) || creation.Type.IsInstanceOf(knownSymbols.MockRepository)))
        {
            // We could expand this check to include any method that accepts a MockBehavior parameter.
            // Leaving it narrowly scoped for now to avoid false positives and potential performance problems.
            return;
        }

        AnalyzeCore(context, creation.Constructor, creation.Arguments, knownSymbols);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, MoqKnownSymbols knownSymbols)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        if (!invocation.TargetMethod.IsInstanceOf(knownSymbols.MockOf, out IMethodSymbol? match))
        {
            // We could expand this check to include any method that accepts a MockBehavior parameter.
            // Leaving it narrowly scoped for now to avoid false positives and potential performance problems.
            return;
        }

        AnalyzeCore(context, match, invocation.Arguments, knownSymbols);
    }

    private static void AnalyzeCore(OperationAnalysisContext context, IMethodSymbol target, ImmutableArray<IArgumentOperation> arguments, MoqKnownSymbols knownSymbols)
    {
        // Check if the target method has a parameter of type MockBehavior
        IParameterSymbol? mockParameter = target.Parameters.DefaultIfNotSingle(parameter => parameter.Type.IsInstanceOf(knownSymbols.MockBehavior));

        // If the target method doesn't have a MockBehavior parameter, check if there's an overload that does
        if (mockParameter is null && target.TryGetOverloadWithParameterOfType(knownSymbols.MockBehavior!, out _, cancellationToken: context.CancellationToken))
        {
            // Using a constructor that doesn't accept a MockBehavior parameter
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(Rule));
            return;
        }

        // The operation specifies a MockBehavior; is it MockBehavior.Default?
        IArgumentOperation? mockArgument = arguments.DefaultIfNotSingle(argument => argument.Parameter.IsInstanceOf(mockParameter));
        if (mockArgument?.DescendantsAndSelf().OfType<IMemberReferenceOperation>().Any(argument => argument.Member.IsInstanceOf(knownSymbols.MockBehaviorDefault)) == true)
        {
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(Rule));
        }
    }
}
