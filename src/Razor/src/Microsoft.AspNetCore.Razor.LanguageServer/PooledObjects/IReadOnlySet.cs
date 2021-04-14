﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.PooledObjects
{
    internal interface IReadOnlySet<T>
    {
        int Count { get; }
        bool Contains(T item);
    }
}
