﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal static class RazorLanguageKindExtensions
    {
        public static string ToContainedLanguageContentType(this RazorLanguageKind razorLanguageKind) =>
            razorLanguageKind == RazorLanguageKind.CSharp ? RazorLSPConstants.CSharpContentTypeName : RazorLSPConstants.HtmlLSPContentTypeName;

        public static string ToContainedLanguageServerName(this RazorLanguageKind razorLanguageKind)
        {
            return razorLanguageKind switch
            {
                RazorLanguageKind.CSharp => RazorLSPConstants.RazorCSharpLanguageServerName,
                RazorLanguageKind.Html => RazorLSPConstants.HtmlLanguageServerName,
                RazorLanguageKind.Razor => RazorLSPConstants.RazorLanguageServerName,
                _ => throw new NotImplementedException("A RazorLanguageKind did not have a corresponding ClientName"),
            };
        }
    }
}
