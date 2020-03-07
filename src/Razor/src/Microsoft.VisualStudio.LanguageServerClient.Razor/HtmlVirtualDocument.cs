﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class HtmlVirtualDocument : VirtualDocument
    {
        private long? _hostDocumentSyncVersion;
        private HtmlVirtualDocumentSnapshot _currentSnapshot;

        public HtmlVirtualDocument(Uri uri, ITextBuffer textBuffer)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (textBuffer is null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            Uri = uri;
            TextBuffer = textBuffer;
            _currentSnapshot = UpdateSnapshot();
        }

        public override Uri Uri { get; }

        public override ITextBuffer TextBuffer { get; }

        public override long? HostDocumentSyncVersion => _hostDocumentSyncVersion;

        public override VirtualDocumentSnapshot CurrentSnapshot => _currentSnapshot;

        public override VirtualDocumentSnapshot Update(IReadOnlyList<TextChange> changes, long hostDocumentVersion)
        {
            if (changes is null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            _hostDocumentSyncVersion = hostDocumentVersion;

            if (changes.Count == 0)
            {
                _currentSnapshot = UpdateSnapshot();
                return _currentSnapshot;
            }

            var edit = TextBuffer.CreateEdit();
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];

                if (change.IsDelete())
                {
                    edit.Delete(change.Span.Start, change.Span.Length);
                }
                else if (change.IsReplace())
                {
                    edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
                }
                else if (change.IsInsert())
                {
                    edit.Insert(change.Span.Start, change.NewText);
                }
                else
                {
                    throw new InvalidOperationException("Unknown edit type when updating LSP HTML buffer.");
                }
            }

            edit.Apply();
            _currentSnapshot = UpdateSnapshot();

            return _currentSnapshot;
        }

        private HtmlVirtualDocumentSnapshot UpdateSnapshot() => new HtmlVirtualDocumentSnapshot(Uri, TextBuffer.CurrentSnapshot, HostDocumentSyncVersion);
    }
}
