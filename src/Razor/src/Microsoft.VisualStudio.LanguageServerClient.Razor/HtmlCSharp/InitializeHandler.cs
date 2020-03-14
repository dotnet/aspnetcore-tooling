﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.InitializeName)]
    internal class InitializeHandler : IRequestHandler<InitializeParams, InitializeResult>
    {
        private static readonly InitializeResult InitializeResult = new InitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                CompletionProvider = new CompletionOptions()
                {
                    AllCommitCharacters = new[] { " ", ".", ";", ">", "=" }, // This is necessary to workaround a bug where the commit character in CompletionItem is not respected.
                    ResolveProvider = false,
                    TriggerCharacters = new[] { ".", "@", "<", "&", "\\", "/", "'", "\"", "=", ":" }
                },
            }
        };

        public Task<InitializeResult> HandleRequestAsync(InitializeParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
            => Task.FromResult(InitializeResult);
    }
}
