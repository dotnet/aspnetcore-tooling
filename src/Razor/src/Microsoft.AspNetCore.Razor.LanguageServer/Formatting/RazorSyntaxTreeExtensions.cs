﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal static class RazorSyntaxTreeExtensions
    {
        public static IReadOnlyList<FormattingSpan> GetFormattingSpans(this RazorSyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            var visitor = new FormattingVisitor(syntaxTree.Source);
            visitor.Visit(syntaxTree.Root);

            return visitor.FormattingSpans;
        }

        public static RazorDirectiveSyntax[] GetCodeBlockDirectives(this RazorSyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            var codeBlockDirectives = syntaxTree.Root
                .DescendantNodes(node => node is RazorDocumentSyntax || node is MarkupBlockSyntax || node is CSharpCodeBlockSyntax)
                .OfType<RazorDirectiveSyntax>()
                .Where(directive => directive.DirectiveDescriptor.Kind == DirectiveKind.CodeBlock)
                .ToArray();

            return codeBlockDirectives;
        }
    }
}
