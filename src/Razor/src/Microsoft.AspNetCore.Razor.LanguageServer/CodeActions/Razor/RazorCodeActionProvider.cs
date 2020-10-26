﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal abstract class RazorCodeActionProvider
    {
        public abstract Task<IReadOnlyList<CodeAction>> ProvideAsync(
            RazorCodeActionContext context,
            CancellationToken cancellationToken);
    }
}
