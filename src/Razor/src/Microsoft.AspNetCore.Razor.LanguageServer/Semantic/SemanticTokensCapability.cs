﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class SemanticTokensCapability : DynamicCapability
    {
        public IReadOnlyDictionary<string, uint> TokenTypes => SemanticTokenLegend.TokenTypesLegend;

        public IReadOnlyDictionary<string, uint> TokenModifiers => SemanticTokenLegend.TokenModifiersLegend;
    }
}
