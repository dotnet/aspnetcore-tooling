﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class UpdateBufferRequest
    {
        public long HostDocumentVersion { get; set; }

        public string HostDocumentFilePath { get; set; }

        public IReadOnlyList<TextChange> Changes { get; set; }
    }
}
