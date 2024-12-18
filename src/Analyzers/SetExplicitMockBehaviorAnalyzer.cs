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
        INamedTypeSymbol? mockBehaviorSymbol = knownSymbols.MockBehavior;
        if (mockBehaviorSymbol is null)
        {
            return;
        }

        // Look for the Mock.Of() method and provide it to Analyze to avoid looking it up multiple times.
        ImmutableArray<IMethodSymbol> ofMethods = knownSymbols.MockOf;

        ImmutableArray<INamedTypeSymbol> mockTypes =
            new INamedTypeSymbol?[] { knownSymbols.Mock1, knownSymbols.MockRepository }
            .WhereNotNull()
            .ToImmutableArray();

        context.RegisterOperationAction(
            context => AnalyzeNewObject(context, mockTypes, mockBehaviorSymbol),
            OperationKind.ObjectCreation);

        if (!ofMethods.IsEmpty)
        {
            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, ofMethods, mockBehaviorSymbol),
                OperationKind.Invocation);
        }
    }

    private static void AnalyzeNewObject(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> mockTypes, INamedTypeSymbol mockBehaviorSymbol)
    {
        if (context.Operation is not IObjectCreationOperation creationOperation)
        {
            return;
        }

        if (creationOperation.Type is not INamedTypeSymbol namedType)
        {
            return;
        }

        if (!namedType.IsInstanceOf(mockTypes))
        {
            return;
        }

        foreach (IArgumentOperation argument in creationOperation.Arguments)
        {
            if (argument.Value is IFieldReferenceOperation fieldReferenceOperation)
            {
                ISymbol field = fieldReferenceOperation.Member;
                if (field.ContainingType.IsInstanceOf(mockBehaviorSymbol) && IsExplicitBehavior(field.Name))
                {
                    return;
                }
            }
        }

        context.ReportDiagnostic(creationOperation.CreateDiagnostic(Rule));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableArray<IMethodSymbol> wellKnownOfMethods, INamedTypeSymbol mockBehaviorSymbol)
    {
        if (context.Operation is not IInvocationOperation invocationOperation)
        {
            return;
        }

        IMethodSymbol targetMethod = invocationOperation.TargetMethod;
        if (!targetMethod.IsInstanceOf(wellKnownOfMethods))
        {
            return;
        }

        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (argument.Value is IFieldReferenceOperation fieldReferenceOperation)
            {
                ISymbol field = fieldReferenceOperation.Member;
                if (field.ContainingType.IsInstanceOf(mockBehaviorSymbol) && IsExplicitBehavior(field.Name))
                {
                    return;
                }
            }
        }

        context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule));
    }

    private static bool IsExplicitBehavior(string symbolName)
    {
        return string.Equals(symbolName, "Loose", StringComparison.Ordinal) || string.Equals(symbolName, "Strict", StringComparison.Ordinal);
    }
}
