﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class AddUsingsCodeActionParams
    {
        public DocumentUri Uri { get; set; }
        public string Namespace { get; set; }
        public bool EnforceCodeActionInvokedInComponent { get; set; }
    }
}
