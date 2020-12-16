﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorDiagnosticsEndpoint :
        IRazorDiagnosticsHandler
    {
        private static readonly IReadOnlyCollection<string> DiagnosticsToIgnore = new HashSet<string>()
        {
            "RemoveUnnecessaryImportsFixable",
            "IDE0005_gen", // Using directive is unnecessary
        };

        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly RazorDocumentMappingService _documentMappingService;

        public RazorDiagnosticsEndpoint(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            DocumentVersionCache documentVersionCache,
            RazorDocumentMappingService documentMappingService)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver == null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (documentVersionCache == null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            if (documentMappingService == null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _documentMappingService = documentMappingService;
        }

        public async Task<RazorDiagnosticsResponse> Handle(RazorDiagnosticsParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var unmappedDiagnostics = request.Diagnostics;
            var filteredDiagnostics = unmappedDiagnostics.Where(d => !CanDiagnosticBeFiltered(d)).ToArray();
            if (!filteredDiagnostics.Any())
            {
                // No diagnostics left after filtering.
                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = Array.Empty<Diagnostic>()
                };
            }

            return await MapDiagnosticsAsync(request, filteredDiagnostics, cancellationToken).ConfigureAwait(false);

            // TODO; HTML filtering blocked on https://dev.azure.com/devdiv/DevDiv/_workitems/edit/1257401
            static bool CanDiagnosticBeFiltered(Diagnostic d) =>
                (DiagnosticsToIgnore.Contains(d.Code?.String) &&
                 d.Severity != DiagnosticSeverity.Error);
        }

        private async Task<RazorDiagnosticsResponse> MapDiagnosticsAsync(RazorDiagnosticsParams request, Diagnostic[] filteredDiagnostics, CancellationToken cancellationToken)
        {
            int? documentVersion = null;
            DocumentSnapshot documentSnapshot = null;
            await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.RazorDocumentUri.GetAbsoluteOrUNCPath(), out documentSnapshot);

                Debug.Assert(documentSnapshot != null, "Failed to get the document snapshot, could not map to document ranges.");

                if (documentSnapshot is null ||
                    !_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out documentVersion))
                {
                    documentVersion = null;
                }
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler);

            if (documentSnapshot is null)
            {
                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = null,
                    HostDocumentVersion = null
                };
            }

            if (request.Kind != RazorLanguageKind.CSharp)
            {
                // All other non-C# requests map directly to where they are in the document.
                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = filteredDiagnostics,
                    HostDocumentVersion = documentVersion,
                };
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            var mappedDiagnostics = new List<Diagnostic>();

            for (var i = 0; i < filteredDiagnostics.Length; i++)
            {
                var diagnostic = filteredDiagnostics.ElementAt(i);
                var projectedRange = diagnostic.Range;

                if (codeDocument.IsUnsupported() ||
                    !_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, projectedRange, MappingBehavior.Inclusive, out var originalRange))
                {
                    // Couldn't remap the range correctly.
                    // If this isn't an `Error` Severity Diagnostic we can discard it.
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                    {
                        continue;
                    }

                    // For `Error` Severity diagnostics we still show the diagnostics to
                    // the user, however we set the range to an undefined range to ensure
                    // clicking on the diagnostic doesn't cause errors.
                    originalRange = RangeExtensions.UndefinedRange;
                }

                diagnostic.Range = originalRange;
                mappedDiagnostics.Add(diagnostic);
            }

            return new RazorDiagnosticsResponse()
            {
                Diagnostics = mappedDiagnostics.ToArray(),
                HostDocumentVersion = documentVersion,
            };
        }
    }
}