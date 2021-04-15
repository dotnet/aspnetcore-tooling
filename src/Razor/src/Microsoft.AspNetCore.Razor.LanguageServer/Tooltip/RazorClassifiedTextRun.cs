﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    /// <summary>
    /// Equivalent to VS' ClassifiedTextRun. The class has been adapted here so we
    /// can use it for LSP serialization since we don't have access to the VS version.
    /// Refer to original class for additional details.
    /// </summary>
    internal sealed class RazorClassifiedTextRun
    {
        public RazorClassifiedTextRun(string classificationTypeName, string text)
            : this(classificationTypeName, text, RazorClassifiedTextRunStyle.Plain)
        {
        }

        public RazorClassifiedTextRun(string classificationTypeName, string text, RazorClassifiedTextRunStyle style)
        {
            ClassificationTypeName = classificationTypeName
                ?? throw new ArgumentNullException(nameof(classificationTypeName));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Style = style;
        }

        public RazorClassifiedTextRun(string classificationTypeName, string text, RazorClassifiedTextRunStyle style, string markerTagType)
        {
            ClassificationTypeName = classificationTypeName
                ?? throw new ArgumentNullException(nameof(classificationTypeName));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            MarkerTagType = markerTagType;
            Style = style;
        }

        public RazorClassifiedTextRun(
            string classificationTypeName,
            string text,
            Action navigationAction,
            string tooltip = null,
            RazorClassifiedTextRunStyle style = RazorClassifiedTextRunStyle.Plain)
        {
            ClassificationTypeName = classificationTypeName
                ?? throw new ArgumentNullException(nameof(classificationTypeName));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Style = style;

            NavigationAction = navigationAction ?? throw new ArgumentNullException(nameof(navigationAction));
            Tooltip = tooltip;
        }

        public string ClassificationTypeName { get; }

        public string Text { get; }

        public string MarkerTagType { get; }

        public RazorClassifiedTextRunStyle Style { get; }

        public string Tooltip { get; }

        public Action NavigationAction { get; }
    }
}
