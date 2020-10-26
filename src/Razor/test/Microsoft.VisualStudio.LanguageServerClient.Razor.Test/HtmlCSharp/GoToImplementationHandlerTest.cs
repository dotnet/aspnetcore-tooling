﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class GoToImplementationHandlerTest
    {
        public GoToImplementationHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");
        }

        private Uri Uri { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var implementationHandler = new GoToImplementationHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider);
            var implementationRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await implementationHandler.HandleRequestAsync(implementationRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var implementationHandler = new GoToImplementationHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider);
            var implementationRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await implementationHandler.HandleRequestAsync(implementationRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
        {
            // Arrange
            var invokedLSPRequest = false;
            var invokedRemapRequest = false;
            var expectedLocation = GetLocation(5, 5, 5, 5, Uri);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var virtualHtmlUri = new Uri("C:/path/to/file.razor__virtual.html");
            var htmlLocation = GetLocation(100, 100, 100, 100, virtualHtmlUri);
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, Location[]>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TextDocumentPositionParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, TextDocumentPositionParams, CancellationToken>((method, serverContentType, implementationParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentImplementationName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLSPContentTypeName, serverContentType);
                    invokedLSPRequest = true;
                })
                .Returns(Task.FromResult(new[] { htmlLocation }));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider
                .Setup(d => d.RemapLocationsAsync(It.IsAny<Location[]>(), It.IsAny<CancellationToken>()))
                .Callback<Location[], CancellationToken>((locations, token) =>
                {
                    Assert.Equal(htmlLocation, locations[0]);
                    invokedRemapRequest = true;
                })
                .Returns(Task.FromResult(Array.Empty<Location>()));

            var implementationHandler = new GoToImplementationHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object);
            var implementationRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await implementationHandler.HandleRequestAsync(implementationRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedLSPRequest);
            Assert.True(invokedRemapRequest);

            // Actual remapping behavior is tested elsewhere.
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var invokedLSPRequest = false;
            var invokedRemapRequest = false;
            var expectedLocation = GetLocation(5, 5, 5, 5, Uri);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var csharpLocation = GetLocation(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, Location[]>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TextDocumentPositionParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, TextDocumentPositionParams, CancellationToken>((method, serverContentType, implementationParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentImplementationName, method);
                    Assert.Equal(RazorLSPConstants.CSharpContentTypeName, serverContentType);
                    invokedLSPRequest = true;
                })
                .Returns(Task.FromResult(new[] { csharpLocation }));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider
                .Setup(d => d.RemapLocationsAsync(It.IsAny<Location[]>(), It.IsAny<CancellationToken>()))
                .Callback<Location[], CancellationToken>((locations, token) =>
                {
                    Assert.Equal(csharpLocation, locations[0]);
                    invokedRemapRequest = true;
                })
                .Returns(Task.FromResult(Array.Empty<Location>()));

            var implementationHandler = new GoToImplementationHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object);
            var implementationRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await implementationHandler.HandleRequestAsync(implementationRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedLSPRequest);
            Assert.True(invokedRemapRequest);

            // Actual remapping behavior is tested elsewhere.
        }

        private Location GetLocation(int startLine, int startCharacter, int endLine, int endCharacter, Uri uri)
        {
            var location = new Location()
            {
                Uri = uri,
                Range = new Range()
                {
                    Start = new Position(startLine, startCharacter),
                    End = new Position(endLine, endCharacter)
                }
            };

            return location;
        }
    }
}
