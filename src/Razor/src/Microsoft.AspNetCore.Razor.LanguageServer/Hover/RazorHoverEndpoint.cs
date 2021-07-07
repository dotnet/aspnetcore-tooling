﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class RazorHoverEndpoint : IHoverHandler
    {
        private HoverCapability _capability;
        private readonly ILogger _logger;
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorHoverInfoService _hoverInfoService;
        private readonly ClientNotifierServiceBase _languageServer;

        public RazorHoverEndpoint(
            SingleThreadedDispatcher singleThreadedDispatcher,
            DocumentResolver documentResolver,
            RazorHoverInfoService hoverInfoService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (hoverInfoService is null)
            {
                throw new ArgumentNullException(nameof(hoverInfoService));
            }

            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _documentResolver = documentResolver;
            _hoverInfoService = hoverInfoService;
            _languageServer = languageServer;
            _logger = loggerFactory.CreateLogger<RazorHoverEndpoint>();
        }

        public async Task<HoverModel> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _singleThreadedDispatcher.DispatcherScheduler);

            if (document is null)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var sourceText = await document.GetTextAsync();
            var linePosition = new LinePosition((int)request.Position.Line, (int)request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Position.Line, (int)request.Position.Character);
            var clientCapabilities = _languageServer.ClientSettings.Capabilities;

            var hoverItem = _hoverInfoService.GetHoverInfo(codeDocument, location, clientCapabilities);

            _logger.LogTrace($"Found hover info items.");

            return hoverItem;
        }

        public void SetCapability(HoverCapability capability)
        {
            _capability = capability;
        }

        public HoverRegistrationOptions GetRegistrationOptions()
        {
            return new HoverRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }
    }
}
