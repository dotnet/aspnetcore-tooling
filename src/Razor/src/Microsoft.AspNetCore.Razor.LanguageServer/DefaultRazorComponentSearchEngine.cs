﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorComponentSearchEngine : RazorComponentSearchEngine
    {
        private readonly ProjectSnapshotManager _projectSnapshotManager;

        public DefaultRazorComponentSearchEngine(ProjectSnapshotManager projectSnapshotManager)
        {
            _projectSnapshotManager = projectSnapshotManager ?? throw new ArgumentNullException(nameof(projectSnapshotManager));
        }

        /// <summary>Search for a component in a project based on its tag name and fully qualified name.</summary>
        /// <remarks>
        /// This method makes several assumptions about the nature of components. First, it assumes that a component
        /// a given name `Name` will be located in a file `Name.razor`. Second, it assumes that the namespace the
        /// component is present in has the same name as the assembly its corresponding tag helper is loaded from.
        /// Implicitly, this method inherits any assumptions made by TrySplitNamespaceAndType.
        /// </remarks>
        /// <param name="tagHelper">A TagHelperDescriptor to find the corresponding Razor component for.</param>
        /// <returns>The corresponding DocumentSnapshot if found, null otherwise.</returns>
        public override async Task<DocumentSnapshot> TryLocateComponentAsync(TagHelperDescriptor tagHelper)
        {
            if (tagHelper is null)
            {
                return null;
            }

            DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(tagHelper.Name, out var namespaceSpan, out var typeSpan);
            var namespaceName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(namespaceSpan, tagHelper.Name);
            var typeName = DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.GetTextSpanContent(typeSpan, tagHelper.Name);

            foreach (var project in _projectSnapshotManager.Projects)
            {
                if (!project.FilePath.EndsWith($"{tagHelper.AssemblyName}.csproj", FilePathComparison.Instance))
                {
                    continue;
                }

                foreach (var path in project.DocumentFilePaths)
                {
                    // Get document and code document
                    var documentSnapshot = project.GetDocument(path);

                    // Rule out if not Razor component with correct name
                    if (!IsPathCandidateForComponent(documentSnapshot, typeName))
                    {
                        continue;
                    }

                    var razorCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                    if (razorCodeDocument is null)
                    {
                        continue;
                    }

                    // Make sure we have the right namespace of the fully qualified name
                    if (!ComponentNamespaceMatchesFullyQualifiedName(razorCodeDocument, namespaceName))
                    {
                        continue;
                    }
                    return documentSnapshot;
                }
            }
            return null;
        }

        public static bool IsPathCandidateForComponent(DocumentSnapshot documentSnapshot, string typeName)
        {
            if (documentSnapshot.FileKind != FileKinds.Component)
            {
                return false;
            }
            var fileName = Path.GetFileNameWithoutExtension(documentSnapshot.FilePath);
            return fileName.Equals(typeName, FilePathComparison.Instance);
        }

        public static bool ComponentNamespaceMatchesFullyQualifiedName(RazorCodeDocument razorCodeDocument, string namespaceName)
        {
            var namespaceNode = (NamespaceDeclarationIntermediateNode)razorCodeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .First(n => n is NamespaceDeclarationIntermediateNode);

            return namespaceNode.Content.Equals(namespaceName, StringComparison.Ordinal);
        }
    }
}
