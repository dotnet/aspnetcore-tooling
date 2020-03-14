﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public abstract class RazorLogger
    {
        public abstract void LogError(string message);

        public abstract void LogWarning(string message);

        public abstract void LogVerbose(string message);
    }
}
