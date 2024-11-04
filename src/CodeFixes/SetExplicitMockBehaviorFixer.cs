using System.Composition;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

namespace Moq.CodeFixes;

/// <summary>
/// Fixes for SetExplicitMockBehaviorAnalyzer (Moq1400).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SetExplicitMockBehaviorFixer))]
[Shared]
public class SetExplicitMockBehaviorFixer : CodeFixProvider
{
    private enum BehaviorType
    {
        Loose,
        Strict,
    }

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.SetExplicitMockBehavior);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode? nodeToFix = root?.FindNode(context.Span, getInnermostNodeForTie: true);

        if (nodeToFix is null)
        {
            return;
        }

        context.RegisterCodeFix(new SetExplicitMockBehaviorCodeAction("Set MockBehavior (Loose)", context.Document, nodeToFix, BehaviorType.Loose), context.Diagnostics);
        context.RegisterCodeFix(new SetExplicitMockBehaviorCodeAction("Set MockBehavior (Strict)", context.Document, nodeToFix, BehaviorType.Strict), context.Diagnostics);
    }

    private sealed class SetExplicitMockBehaviorCodeAction : CodeAction
    {
        private readonly Document _document;
        private readonly SyntaxNode _nodeToFix;
        private readonly BehaviorType _behaviorType;

        public SetExplicitMockBehaviorCodeAction(string title, Document document, SyntaxNode nodeToFix, BehaviorType behaviorType)
        {
            Title = title;
            _document = document;
            _nodeToFix = nodeToFix;
            _behaviorType = behaviorType;
        }

        public override string Title { get; }

        public override string? EquivalenceKey => Title;

        protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(_document, cancellationToken).ConfigureAwait(false);
            MoqKnownSymbols knownSymbols = new(editor.SemanticModel.Compilation);
            IOperation? operation = editor.SemanticModel.GetOperation(_nodeToFix, cancellationToken);

            INamedTypeSymbol? mockBehaviorType = knownSymbols.MockBehavior;

            if (mockBehaviorType is null)
            {
                return _document;
            }

            string behaviorTypeName = _behaviorType switch
            {
                BehaviorType.Loose => knownSymbols.MockBehaviorLoose!.ToString(),
                BehaviorType.Strict => knownSymbols.MockBehaviorStrict!.ToString(),
                _ => throw new InvalidOperationException(),
            };

            SyntaxNode argument = editor.Generator.Argument(
                editor.Generator.MemberAccessExpression(
                    editor.Generator.TypeExpression(knownSymbols.MockBehavior!, addImport: true),
                    editor.Generator.IdentifierName(behaviorTypeName)));

            SyntaxNode newNode = editor.Generator.InsertArguments(_nodeToFix, 0, argument);

            editor.ReplaceNode(_nodeToFix, newNode.WithAdditionalAnnotations(Simplifier.Annotation));
            return editor.GetChangedDocument();
        }
    }
}

internal static class SyntaxGeneratorExtensions
{
    public static SyntaxNode InsertArguments(this SyntaxGenerator generator, SyntaxNode syntax, int index, params SyntaxNode[] items)
    {
        if (syntax is InvocationExpressionSyntax invocation)
        {
            if (items.Any(item => item is not ArgumentSyntax))
            {
                throw new ArgumentException("Must all be of type ArgumentSyntax", nameof(items));
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

            arguments = arguments.InsertRange(index, items.OfType<ArgumentSyntax>());

            syntax = syntax.ReplaceNode(invocation.ArgumentList, invocation.ArgumentList.WithArguments(arguments));

            return syntax;
        }

        throw new ArgumentException($"Must be of type {nameof(InvocationExpressionSyntax)}", nameof(syntax));
    }
}
