﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using SemanticTokens = OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals.SemanticTokens;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(RazorLanguageServerCustomMessageTarget))]
    internal class DefaultRazorLanguageServerCustomMessageTarget : RazorLanguageServerCustomMessageTarget
    {
        private readonly TrackingLSPDocumentManager _documentManager;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly RazorUIContextManager _uiContextManager;
        private readonly IDisposable _razorReadyListener;

        private const string RazorReadyFeature = "Razor-Initialization";

        [ImportingConstructor]
        public DefaultRazorLanguageServerCustomMessageTarget(
            LSPDocumentManager documentManager,
            JoinableTaskContext joinableTaskContext,
            LSPRequestInvoker requestInvoker,
            RazorUIContextManager uIContextManager,
            IRazorAsynchronousOperationListenerProviderAccessor asyncOpListenerProvider) :
                this(
                    documentManager,
                    joinableTaskContext,
                    requestInvoker,
                    uIContextManager,
                    asyncOpListenerProvider.GetListener(RazorReadyFeature).BeginAsyncOperation(RazorReadyFeature))
        {
        }

        internal DefaultRazorLanguageServerCustomMessageTarget(
            LSPDocumentManager documentManager,
            JoinableTaskContext joinableTaskContext,
            LSPRequestInvoker requestInvoker,
            RazorUIContextManager uIContextManager,
            IDisposable razorReadyListener)
        {
            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (uIContextManager is null)
            {
                throw new ArgumentNullException(nameof(uIContextManager));
            }

            if (razorReadyListener is null)
            {
                throw new ArgumentNullException(nameof(razorReadyListener));
            }

            _documentManager = documentManager as TrackingLSPDocumentManager;

            if (_documentManager is null)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentException("The LSP document manager should be of type " + typeof(TrackingLSPDocumentManager).FullName, nameof(_documentManager));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            _joinableTaskFactory = joinableTaskContext.Factory;
            _requestInvoker = requestInvoker;
            _uiContextManager = uIContextManager;
            _razorReadyListener = razorReadyListener;
        }

        // Testing constructor
        internal DefaultRazorLanguageServerCustomMessageTarget(TrackingLSPDocumentManager documentManager)
        {
            _documentManager = documentManager;
        }

        public override async Task UpdateCSharpBufferAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            UpdateCSharpBuffer(request);
        }

        // Internal for testing
        internal void UpdateCSharpBuffer(UpdateBufferRequest request)
        {
            if (request == null || request.HostDocumentFilePath == null || request.HostDocumentVersion == null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value);
        }

        public override async Task UpdateHtmlBufferAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            UpdateHtmlBuffer(request);
        }

        // Internal for testing
        internal void UpdateHtmlBuffer(UpdateBufferRequest request)
        {
            if (request == null || request.HostDocumentFilePath == null || request.HostDocumentVersion == null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value);
        }

        public override async Task<RazorDocumentRangeFormattingResponse> RazorRangeFormattingAsync(RazorDocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            var response = new RazorDocumentRangeFormattingResponse() { Edits = Array.Empty<TextEdit>() };

            if (request.Kind == RazorLanguageKind.Razor)
            {
                return response;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync();

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return response;
            }

            string serverContentType;
            Uri projectedUri;
            if (request.Kind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDocument))
            {
                serverContentType = RazorLSPConstants.CSharpContentTypeName;
                projectedUri = csharpDocument.Uri;
            }
            else if (request.Kind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocument))
            {
                serverContentType = RazorLSPConstants.HtmlLSPContentTypeName;
                projectedUri = htmlDocument.Uri;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
                return response;
            }

            var formattingParams = new DocumentRangeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = projectedUri },
                Range = request.ProjectedRange,
                Options = request.Options
            };

            var edits = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentRangeFormattingParams, TextEdit[]>(
                Methods.TextDocumentRangeFormattingName,
                serverContentType,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            response.Edits = edits ?? Array.Empty<TextEdit>();

            return response;
        }

        public override async Task<VSCodeAction[]> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken)
        {
            if (codeActionParams is null)
            {
                throw new ArgumentNullException(nameof(codeActionParams));
            }

            if (!_documentManager.TryGetDocument(codeActionParams.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                return null;
            }

            codeActionParams.TextDocument.Uri = csharpDoc.Uri;

            var results = await _requestInvoker.ReinvokeRequestOnMultipleServersAsync<CodeActionParams, VSCodeAction[]>(
                Methods.TextDocumentCodeActionName,
                LanguageServerKind.CSharp.ToContentType(),
                SupportsCSharpCodeActions,
                codeActionParams,
                cancellationToken).ConfigureAwait(false);

            return results.SelectMany(l => l).ToArray();
        }

        public override async Task<VSCodeAction> ResolveCodeActionsAsync(VSCodeAction codeAction, CancellationToken cancellationToken)
        {
            if (codeAction is null)
            {
                throw new ArgumentNullException(nameof(codeAction));
            }

            var results = await _requestInvoker.ReinvokeRequestOnMultipleServersAsync<VSCodeAction, VSCodeAction>(
                MSLSPMethods.TextDocumentCodeActionResolveName,
                LanguageServerKind.CSharp.ToContentType(),
                SupportsCSharpCodeActions,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            return results.FirstOrDefault(c => c != null);
        }

        [Obsolete]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override async Task<ProvideSemanticTokensResponse> ProvideSemanticTokensAsync(SemanticTokensParams semanticTokensParams, CancellationToken cancellationToken)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            if (semanticTokensParams is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensParams));
            }

            if (!_documentManager.TryGetDocument(semanticTokensParams.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                return null;
            }

            semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;

            var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                LanguageServerConstants.LegacyRazorSemanticTokensEndpoint,
                LanguageServerKind.CSharp.ToContentType(),
                semanticTokensParams,
                cancellationToken).ConfigureAwait(false);

            var result = new ProvideSemanticTokensResponse(csharpResults, csharpDoc.HostDocumentSyncVersion);

            return result;
        }

        public override async Task RazorServerReadyAsync(CancellationToken cancellationToken)
        {
            // Doing both UIContext and BrokeredService while integrating
            await _uiContextManager.SetUIContextAsync(RazorLSPConstants.RazorActiveUIContextGuid, isActive: true, cancellationToken);
            _razorReadyListener.Dispose();
        }

        private static bool SupportsCSharpCodeActions(JToken token)
        {
            var serverCapabilities = token.ToObject<VSServerCapabilities>();

            var providesCodeActions = serverCapabilities?.CodeActionProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;

            var resolvesCodeActions = serverCapabilities?.CodeActionsResolveProvider == true;

            return providesCodeActions && resolvesCodeActions;
        }
    }
}
