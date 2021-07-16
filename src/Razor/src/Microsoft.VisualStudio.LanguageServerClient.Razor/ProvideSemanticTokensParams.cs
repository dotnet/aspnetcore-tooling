// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class ProvideSemanticTokensParams : SemanticTokensParams
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public long RequiredHostDocumentVersion { get; set; }
    }
}
