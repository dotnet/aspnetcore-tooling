// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    // Sets the FileName static variable.
    // Finds the test method name using reflection, and uses
    // that to find the expected input/output test files in the file system.
    [IntializeTestFile]

    // These tests must be run serially due to the test specific FileName static var.
    [Collection("FormattingTestSerialRuns")]
    public class FormattingTestBase : LanguageServerTestBase
    {
        private static readonly AsyncLocal<string> _fileName = new AsyncLocal<string>();

        public FormattingTestBase()
        {
            TestProjectPath = GetProjectDirectory();
        }

        public static string TestProjectPath { get; private set; }

        // Used by the test framework to set the 'base' name for test files.
        public static string FileName
        {
            get { return _fileName.Value; }
            set { _fileName.Value = value; }
        }

        protected async Task RunFormattingTestAsync(
            string input,
            string expected,
            int tabSize = 4,
            bool insertSpaces = true,
            string fileKind = null,
            IReadOnlyList<TagHelperDescriptor> tagHelpers = null)
        {
            // Arrange
            fileKind ??= FileKinds.Component;
            input = input.TrimStart('\r', '\n');
            expected = expected.TrimStart('\r', '\n');

            var start = input.IndexOf('|', StringComparison.Ordinal);
            var end = input.LastIndexOf('|');
            input = input.Replace("|", string.Empty, StringComparison.Ordinal);

            var source = SourceText.From(input);
            var span = TextSpan.FromBounds(start, end - 1);
            var range = span.AsRange(source);

            var path = "file:///path/to/document.razor";
            var uri = new Uri(path);
            var (codeDocument, documentSnapshot) = CreateCodeDocumentAndSnapshot(source, uri.AbsolutePath, tagHelpers, fileKind);
            var options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces,
            };

            var formattingService = CreateFormattingService(codeDocument);

            // Act
            var edits = await formattingService.FormatAsync(uri, documentSnapshot, range, options, CancellationToken.None);

            // Assert
            var edited = ApplyEdits(source, edits);
            var actual = edited.ToString();

#if GENERATE_BASELINES
            Assert.False(true, "GENERATE_BASELINES is set to true.");
#else
            Assert.Equal(expected, actual, ignoreLineEndingDifferences: true);
#endif
        }

        protected async Task RunOnTypeFormattingTestAsync(string input, string expected, string triggerCharacter, int tabSize = 4, bool insertSpaces = true, string fileKind = null)
        {
            // Arrange
            fileKind ??= FileKinds.Component;
            input = input.TrimStart('\r', '\n');
            expected = expected.TrimStart('\r', '\n');

            var beforeTrigger = input.IndexOf('|', StringComparison.Ordinal);
            var afterTrigger = input.LastIndexOf('|') - 1;
            input = input.Replace("|", string.Empty, StringComparison.Ordinal);

            var source = SourceText.From(input);

            var path = "file:///path/to/document.razor";
            var uri = new Uri(path);
            var (codeDocument, documentSnapshot) = CreateCodeDocumentAndSnapshot(source, uri.AbsolutePath, fileKind: fileKind);
            var options = new FormattingOptions()
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces,
            };

            var formattingService = CreateFormattingService(codeDocument);
            var (kind, projectedEdits) = GetFormattedEdits(codeDocument, expected, beforeTrigger);

            // Act
            var edits = await formattingService.ApplyFormattedEditsAsync(uri, documentSnapshot, kind, projectedEdits, options, CancellationToken.None);

            // Assert
            var edited = ApplyEdits(source, edits);
            var actual = edited.ToString();

#if GENERATE_BASELINES
            Assert.False(true, "GENERATE_BASELINES is set to true.");
#else
            Assert.Equal(expected, actual);
#endif
        }

        private (RazorLanguageKind, TextEdit[]) GetFormattedEdits(RazorCodeDocument codeDocument, string expected, int positionBeforeTriggerChar)
        {
            var mappingService = new DefaultRazorDocumentMappingService();
            var languageKind = mappingService.GetLanguageKind(codeDocument, positionBeforeTriggerChar);

            var expectedText = SourceText.From(expected);
            var (expectedCodeDocument, _) = CreateCodeDocumentAndSnapshot(expectedText, codeDocument.Source.FilePath, fileKind: codeDocument.GetFileKind());

            var edits = Array.Empty<TextEdit>();

            if (languageKind == RazorLanguageKind.CSharp)
            {
                var beforeCSharpText = SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode);
                var afterCSharpText = SourceText.From(expectedCodeDocument.GetCSharpDocument().GeneratedCode);
                edits = SourceTextDiffer.GetMinimalTextChanges(beforeCSharpText, afterCSharpText, lineDiffOnly: false).Select(c => c.AsTextEdit(beforeCSharpText)).ToArray();
            }
            else if (languageKind == RazorLanguageKind.Html)
            {
                var beforeHtmlText = SourceText.From(codeDocument.GetHtmlDocument().GeneratedHtml);
                var afterHtmlText = SourceText.From(expectedCodeDocument.GetHtmlDocument().GeneratedHtml);
                edits = SourceTextDiffer.GetMinimalTextChanges(beforeHtmlText, afterHtmlText, lineDiffOnly: false).Select(c => c.AsTextEdit(beforeHtmlText)).ToArray();
            }

            return (languageKind, edits);
        }

        private RazorFormattingService CreateFormattingService(RazorCodeDocument codeDocument)
        {
            var mappingService = new DefaultRazorDocumentMappingService();

            var client = new FormattingLanguageServerClient(TestProjectPath, FileName);
            client.AddCodeDocument(codeDocument);
            var passes = new List<IFormattingPass>()
            {
                new HtmlFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new CSharpFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new CSharpOnTypeFormattingPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new FormattingDiagnosticValidationPass(mappingService, FilePathNormalizer, client, LoggerFactory),
                new FormattingContentValidationPass(mappingService, FilePathNormalizer, client, LoggerFactory),
            };

            return new DefaultRazorFormattingService(passes, LoggerFactory);
        }

        private SourceText ApplyEdits(SourceText source, TextEdit[] edits)
        {
            var changes = edits.Select(e => e.AsTextChange(source));
            return source.WithChanges(changes);
        }

        private static (RazorCodeDocument, DocumentSnapshot) CreateCodeDocumentAndSnapshot(SourceText text, string path, IReadOnlyList<TagHelperDescriptor> tagHelpers = null, string fileKind = default)
        {
            fileKind ??= FileKinds.Component;
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            var sourceDocument = text.GetRazorSourceDocument(path, path);
            var projectEngine = RazorProjectEngine.Create(builder => { builder.SetRootNamespace("Test"); });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, Array.Empty<RazorSourceDocument>(), tagHelpers);

            var documentSnapshot = new Mock<DocumentSnapshot>();
            documentSnapshot.Setup(d => d.GetGeneratedOutputAsync()).Returns(Task.FromResult(codeDocument));
            documentSnapshot.Setup(d => d.Project.GetProjectEngine()).Returns(projectEngine);
            documentSnapshot.Setup(d => d.FilePath).Returns(path);
            documentSnapshot.Setup(d => d.TargetPath).Returns(path);
            documentSnapshot.Setup(d => d.Project.TagHelpers).Returns(tagHelpers);
            documentSnapshot.Setup(d => d.FileKind).Returns(fileKind);

            return (codeDocument, documentSnapshot.Object);
        }

        private static string GetProjectDirectory()
        {
            var repoRoot = SearchUp(AppContext.BaseDirectory, "global.json");
            if (repoRoot == null)
            {
                repoRoot = AppContext.BaseDirectory;
            }

            var assemblyName = typeof(FormattingTestBase).Assembly.GetName().Name;
            var projectDirectory = Path.Combine(repoRoot, "src", "Razor", "test", assemblyName);

            return projectDirectory;
        }

        private static string SearchUp(string baseDirectory, string fileName)
        {
            var directoryInfo = new DirectoryInfo(baseDirectory);
            do
            {
                var fileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, fileName));
                if (fileInfo.Exists)
                {
                    return fileInfo.DirectoryName;
                }
                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            return null;
        }
    }
}
