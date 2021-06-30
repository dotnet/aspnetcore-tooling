﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    public class EditorDocumentManagerBaseTest : ForegroundDispatcherTestBase
    {
        public EditorDocumentManagerBaseTest()
        {

            Manager = new TestEditorDocumentManager(Dispatcher, new JoinableTaskContext());
        }

        private TestEditorDocumentManager Manager { get; }

        public string Project1 => TestProjectData.SomeProject.FilePath;

        public string Project2 => TestProjectData.AnotherProject.FilePath;

        public string File1 => TestProjectData.SomeProjectFile1.FilePath;

        public string File2 => TestProjectData.AnotherProjectFile2.FilePath;

        public TestTextBuffer TextBuffer => new TestTextBuffer(new StringTextSnapshot("HI"));

        [ForegroundFact]
        public async Task GetOrCreateDocument_CreatesAndCachesDocument()
        {
            // Arrange
            var expected = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Act
            Manager.TryGetDocument(new DocumentKey(Project1, File1), out var actual);

            // Assert
            Assert.Same(expected, actual);
        }

        [ForegroundFact]
        public async Task GetOrCreateDocument_NoOp()
        {
            // Arrange
            var expected = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Act
            var actual = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Assert
            Assert.Same(expected, actual);
        }

        [ForegroundFact]
        public async Task GetOrCreateDocument_SameFile_MulipleProjects()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Act
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project2, File1), null, null, null, null);

            // Assert
            Assert.NotSame(document1, document2);
        }

        [ForegroundFact]
        public async Task GetOrCreateDocument_MulipleFiles_SameProject()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Act
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File2), null, null, null, null);

            // Assert
            Assert.NotSame(document1, document2);
        }

        [ForegroundFact]
        public async Task GetOrCreateDocument_WithBuffer_AttachesBuffer()
        {
            // Arrange
            Manager.Buffers.Add(File1, TextBuffer);

            // Act
            var document = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);

            // Assert
            Assert.True(document.IsOpenInEditor);
            Assert.NotNull(document.EditorTextBuffer);

            Assert.Same(document, Assert.Single(Manager.Opened));
            Assert.Empty(Manager.Closed);
        }

        [ForegroundFact]
        public async Task TryGetMatchingDocuments_MultipleDocuments()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project2, File1), null, null, null, null);

            // Act
            Manager.TryGetMatchingDocuments(File1, out var documents);

            // Assert
            Assert.Collection(
                documents.OrderBy(d => d.ProjectFilePath),
                d => Assert.Same(document2, d),
                d => Assert.Same(document1, d));
        }

        [ForegroundFact]
        public async Task RemoveDocument_MultipleDocuments_RemovesOne()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project2, File1), null, null, null, null);

            // Act
            Manager.RemoveDocument(document1);

            // Assert
            Manager.TryGetMatchingDocuments(File1, out var documents);
            Assert.Collection(
                documents.OrderBy(d => d.ProjectFilePath),
                d => Assert.Same(document2, d));
        }

        [ForegroundFact]
        public async Task DocumentOpened_MultipleDocuments_OpensAll()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project2, File1), null, null, null, null);

            // Act
            Manager.DocumentOpened(File1, TextBuffer);

            // Assert
            Assert.Collection(
                Manager.Opened.OrderBy(d => d.ProjectFilePath),
                d => Assert.Same(document2, d),
                d => Assert.Same(document1, d));
        }

        [ForegroundFact]
        public async Task DocumentOpened_MultipleDocuments_ClosesAll()
        {
            // Arrange
            var document1 = await Manager.GetOrCreateDocument(new DocumentKey(Project1, File1), null, null, null, null);
            var document2 = await Manager.GetOrCreateDocument(new DocumentKey(Project2, File1), null, null, null, null);
            Manager.DocumentOpened(File1, TextBuffer);

            // Act
            Manager.DocumentClosed(File1);

            // Assert
            Assert.Collection(
                Manager.Closed.OrderBy(d => d.ProjectFilePath),
                d => Assert.Same(document2, d),
                d => Assert.Same(document1, d));
        }

        private class TestEditorDocumentManager : EditorDocumentManagerBase
        {
            public TestEditorDocumentManager(ForegroundDispatcher foregroundDispatcher, JoinableTaskContext joinableTaskContext) 
                : base(foregroundDispatcher, joinableTaskContext, new DefaultFileChangeTrackerFactory())
            {
            }

            public List<EditorDocument> Opened { get; } = new List<EditorDocument>();

            public List<EditorDocument> Closed { get; } = new List<EditorDocument>();

            public Dictionary<string, ITextBuffer> Buffers { get; } = new Dictionary<string, ITextBuffer>();

            public new void DocumentOpened(string filePath, ITextBuffer textBuffer)
            {
                base.DocumentOpened(filePath, textBuffer);
            }

            public new void DocumentClosed(string filePath)
            {
                base.DocumentClosed(filePath);
            }

            protected override Task<ITextBuffer> GetTextBufferForOpenDocument(string filePath)
            {
                Buffers.TryGetValue(filePath, out var buffer);
                return Task.FromResult(buffer);
            }

            protected override void OnDocumentOpened(EditorDocument document)
            {
                Opened.Add(document);
            }

            protected override void OnDocumentClosed(EditorDocument document)
            {
                Closed.Add(document);
            }
        }
    }
}
