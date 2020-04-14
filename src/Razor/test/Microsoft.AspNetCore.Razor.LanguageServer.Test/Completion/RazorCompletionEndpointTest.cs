﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionEndpointTest : LanguageServerTestBase
    {
        protected ILanguageServer LanguageServer
        {
            get
            {
                var initializeParams = new InitializeParams
                {
                    Capabilities = new ClientCapabilities
                    {
                        TextDocument = new TextDocumentClientCapabilities
                        {
                            Completion = new Supports<CompletionCapability>
                            {
                                Value = new CompletionCapability
                                {
                                    CompletionItem = new CompletionItemCapability
                                    {
                                        SnippetSupport = true
                                    }
                                }
                            }
                        }
                    }
                };

                var languageServer = new Mock<ILanguageServer>();
                languageServer.SetupGet(server => server.ClientSettings)
                    .Returns(initializeParams);

                return languageServer.Object;
            }
        }

        public RazorCompletionEndpointTest()
        {
            // Working around strong naming restriction.
            var tagHelperFactsService = new DefaultTagHelperFactsService();
            var completionProviders = new RazorCompletionItemProvider[]
            {
                new DirectiveCompletionItemProvider(),
                new DirectiveAttributeCompletionItemProvider(tagHelperFactsService),
                new DirectiveAttributeParameterCompletionItemProvider(tagHelperFactsService),
            };
            CompletionFactsService = new DefaultRazorCompletionFactsService(completionProviders);
            TagHelperCompletionService = Mock.Of<TagHelperCompletionService>(
                service => service.GetCompletionsAt(It.IsAny<SourceSpan>(), It.IsAny<RazorCodeDocument>()) == Array.Empty<CompletionItem>());
            TagHelperDescriptionFactory = Mock.Of<TagHelperDescriptionFactory>();
            EmptyDocumentResolver = Mock.Of<DocumentResolver>();
        }

        private RazorCompletionFactsService CompletionFactsService { get; }

        private readonly CompletionCapability DefaultCapability = new CompletionCapability
        {
            CompletionItem = new CompletionItemCapability {
                SnippetSupport = true
            }
        };

        private TagHelperCompletionService TagHelperCompletionService { get; }

        private TagHelperDescriptionFactory TagHelperDescriptionFactory { get; }

        private DocumentResolver EmptyDocumentResolver { get; }

        [Fact]
        public void TryConvert_Directive_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("testDisplay", "testInsert", RazorCompletionItemKind.Directive);
            var description = "Something";
            completionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription(description));
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            completionEndpoint.SetCapability(DefaultCapability);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Equal(description, converted.Detail);
            Assert.NotNull(converted.Documentation);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.Directive, convertedKind);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeTransition_ReturnsTrue()
        {
            // Arrange
            var completionItem = DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem;
            var description = completionItem.GetDirectiveCompletionDescription().Description;
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.True(converted.Preselect);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Equal(description, converted.Detail);
            Assert.NotNull(converted.Documentation);
            Assert.NotNull(converted.Command);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.Directive, convertedKind);
        }

        [Fact]
        public void TryConvert_MarkupTransition_ReturnsTrue()
        {
            // Arrange
            var completionItem = MarkupTransitionCompletionItemProvider.MarkupTransitionCompletionItem;
            var description = completionItem.GetMarkupTransitionCompletionDescription().Description;
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Equal(description, converted.Detail);
            Assert.NotNull(converted.Documentation);
            Assert.Equal(converted.CommitCharacters, completionItem.CommitCharacters);
        }

        [Fact]
        public void TryConvert_DirectiveAttribute_NoSnippet_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("@testDisplay...", "testInsert", RazorCompletionItemKind.DirectiveAttribute);
            completionItem.SetAttributeCompletionDescription(new AttributeCompletionDescription(new CodeAnalysis.Razor.Completion.AttributeDescriptionInfo[] {
                new CodeAnalysis.Razor.Completion.AttributeDescriptionInfo("System.String", "System.String", "@testDisplay", "This is docs")
            }));
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var capability = new CompletionCapability
            {
                CompletionItem = new CompletionItemCapability
                {
                    SnippetSupport = false
                }
            };
            completionEndpoint.SetCapability(capability);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.DirectiveAttribute, convertedKind);
        }

        [Fact]
        public void TryConvert_DirectiveAttribute_Snippet_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("@testDisplay...", "testInsert", RazorCompletionItemKind.DirectiveAttribute);
            completionItem.SetAttributeCompletionDescription(new AttributeCompletionDescription(new CodeAnalysis.Razor.Completion.AttributeDescriptionInfo[] {
                new CodeAnalysis.Razor.Completion.AttributeDescriptionInfo("System.String", "System.String", "@testDisplay", "This is docs")
            }));
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var capability = new CompletionCapability {
                CompletionItem = new CompletionItemCapability {
                    SnippetSupport = true
                }
            };
            completionEndpoint.SetCapability(capability);
            var expectedInsert = "testInsert$1=\"$2\"$0";

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(expectedInsert, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.DirectiveAttribute, convertedKind);
        }

        [Fact]
        public void TryConvert_DirectiveAttribute_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("@testDisplay", "testInsert", RazorCompletionItemKind.DirectiveAttribute);
            completionItem.SetAttributeCompletionDescription(new AttributeCompletionDescription(Array.Empty<CodeAnalysis.Razor.Completion.AttributeDescriptionInfo>()));
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.DirectiveAttribute, convertedKind);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeParameter_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.DirectiveAttributeParameter);
            completionItem.SetAttributeCompletionDescription(new AttributeCompletionDescription(Array.Empty<CodeAnalysis.Razor.Completion.AttributeDescriptionInfo>()));
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);

            // Act
            var result = completionEndpoint.TryConvert(completionItem, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
            Assert.True(converted.TryGetRazorCompletionKind(out var convertedKind));
            Assert.Equal(RazorCompletionItemKind.DirectiveAttributeParameter, convertedKind);
        }

        [Fact]
        public async Task Handle_DirectiveAttributeCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var markdown = "Some Markdown";
            descriptionFactory.Setup(factory => factory.TryCreateDescription(It.IsAny<AttributeCompletionDescription>(), out markdown))
                .Returns(true);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(new AttributeCompletionDescription(Array.Empty<CodeAnalysis.Razor.Completion.AttributeDescriptionInfo>()));
            completionItem.SetRazorCompletionKind(RazorCompletionItemKind.DirectiveAttribute);

            // Act
            var newCompletionItem = await completionEndpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_DirectiveAttributeParameterCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var markdown = "Some Markdown";
            descriptionFactory.Setup(factory => factory.TryCreateDescription(It.IsAny<AttributeCompletionDescription>(), out markdown))
                .Returns(true);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(new AttributeCompletionDescription(Array.Empty<CodeAnalysis.Razor.Completion.AttributeDescriptionInfo>()));
            completionItem.SetRazorCompletionKind(RazorCompletionItemKind.DirectiveAttributeParameter);

            // Act
            var newCompletionItem = await completionEndpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_TagHelperElementCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var markdown = "Some Markdown";
            descriptionFactory.Setup(factory => factory.TryCreateDescription(It.IsAny<ElementDescriptionInfo>(), out markdown))
                .Returns(true);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(ElementDescriptionInfo.Default);

            // Act
            var newCompletionItem = await completionEndpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_TagHelperAttributeCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var markdown = "Some Markdown";
            descriptionFactory.Setup(factory => factory.TryCreateDescription(It.IsAny<AttributeDescriptionInfo>(), out markdown))
                .Returns(true);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(AttributeDescriptionInfo.Default);

            // Act
            var newCompletionItem = await completionEndpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_NonTagHelperCompletion_Noops()
        {
            // Arrange
            var descriptionFactory = new Mock<TagHelperDescriptionFactory>();
            var markdown = "Some Markdown";
            descriptionFactory.Setup(factory => factory.TryCreateDescription(It.IsAny<ElementDescriptionInfo>(), out markdown))
                .Returns(true);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, descriptionFactory.Object, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();

            // Act
            var newCompletionItem = await completionEndpoint.Handle(completionItem, default);

            // Assert
            Assert.Null(newCompletionItem.Documentation);
        }

        [Fact]
        public void CanResolve_DirectiveAttributeCompletion_ReturnsTrue()
        {
            // Arrange
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetRazorCompletionKind(RazorCompletionItemKind.DirectiveAttribute);

            // Act
            var result = completionEndpoint.CanResolve(completionItem);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanResolve_DirectiveAttributeParameterCompletion_ReturnsTrue()
        {
            // Arrange
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetRazorCompletionKind(RazorCompletionItemKind.DirectiveAttributeParameter);

            // Act
            var result = completionEndpoint.CanResolve(completionItem);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanResolve_TagHelperElementCompletion_ReturnsTrue()
        {
            // Arrange
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(ElementDescriptionInfo.Default);

            // Act
            var result = completionEndpoint.CanResolve(completionItem);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanResolve_TagHelperAttributeCompletion_ReturnsTrue()
        {
            // Arrange
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();
            completionItem.SetDescriptionInfo(AttributeDescriptionInfo.Default);

            // Act
            var result = completionEndpoint.CanResolve(completionItem);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanResolve_NonTagHelperCompletion_ReturnsFalse()
        {
            // Arrange
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, EmptyDocumentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var completionItem = new CompletionItem();

            // Act
            var result = completionEndpoint.CanResolve(completionItem);

            // Assert
            Assert.False(result);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_Unsupported_NoCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument("@");
            codeDocument.SetUnsupported();
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, documentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1)
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Empty(completionList);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ResolvesDirectiveCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument("@");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, documentResolver, CompletionFactsService, TagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1)
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert

            // These are the default directives that don't need to be separately registered, they should always be part of the completion list.
            Assert.Contains(completionList, item => item.InsertText == "addTagHelper");
            Assert.Contains(completionList, item => item.InsertText == "removeTagHelper");
            Assert.Contains(completionList, item => item.InsertText == "tagHelperPrefix");
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ResolvesTagHelperElementCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var tagHelperCompletionItem = new CompletionItem()
            {
                InsertText = "Test"
            };
            var tagHelperCompletionService = Mock.Of<TagHelperCompletionService>(
                service => service.GetCompletionsAt(It.IsAny<SourceSpan>(), It.IsAny<RazorCodeDocument>()) == new[] { tagHelperCompletionItem });
            var codeDocument = CreateCodeDocument("<");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(Dispatcher, documentResolver, CompletionFactsService, tagHelperCompletionService, TagHelperDescriptionFactory, LanguageServer, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1)
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList, item => item.InsertText == "Test");
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText));
            var documentResolver = new Mock<DocumentResolver>();
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument);
            codeDocument.SetSyntaxTree(syntaxTree);
            var tagHelperDocumentContext = TagHelperDocumentContext.Create(prefix: string.Empty, Enumerable.Empty<TagHelperDescriptor>());
            codeDocument.SetTagHelperContext(tagHelperDocumentContext);
            return codeDocument;
        }
    }
}
