using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExcludeFromCodeCoverageRoslynAnalyzer
{
    public class IgnoreTestsSyntaxRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node.BaseList != null &&
                node.BaseList.Types.Any(tp => tp.Type?.ToString().StartsWith("DefaultDataGridConfigurationBase") == true &&
                !HasAttribute(node, "ExcludeFromCodeCoverage")))
            {
                var newAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage"));
                var syntaxList = SyntaxFactory.SeparatedList<AttributeSyntax>();
                syntaxList = syntaxList.Add(newAttribute);

                var attributes = SyntaxFactory.AttributeList(syntaxList)
                    .WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia("\n    "))
                    .WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia("    "));

                // save comments
                var trivia = node.GetLeadingTrivia();

                // remove comments
                node = node.WithoutLeadingTrivia();

                // add attribute lists
                node = node.AddAttributeLists(attributes);

                // restore comments to make them first
                node = node.WithLeadingTrivia(trivia);
            }

            return node;
        }

        private bool HasAttribute(ClassDeclarationSyntax classDeclaration, string name)
        {
            return classDeclaration.AttributeLists.Any(list => list.Attributes.Any(attr => attr.Name.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)));
        }
    }
}