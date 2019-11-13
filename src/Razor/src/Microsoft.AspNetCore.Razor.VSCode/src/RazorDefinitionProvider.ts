/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { virtualCsSuffix, virtualHtmlSuffix } from './RazorDocumentFactory';
import { RazorLanguageFeatureBase } from './RazorLanguageFeatureBase';
import { LanguageKind } from './RPC/LanguageKind';

export class RazorDefinitionProvider
    extends RazorLanguageFeatureBase
    implements vscode.DefinitionProvider {

    public async provideDefinition(
        document: vscode.TextDocument, position: vscode.Position,
        token: vscode.CancellationToken) {

        const projection = await this.getProjection(document, position, token);
        if (!projection) {
            return;
        }

        let definitions = await vscode.commands.executeCommand<vscode.Definition>(
            'vscode.executeDefinitionProvider',
            projection.uri,
            projection.position) as vscode.Location[];

        if ( projection.languageKind === LanguageKind.CSharp) {
            // C# knows about line pragma, if we're getting a direction to a virtual c# document
            // that means the piece we're trying to navigate to does not have a representation in the
            // top level file.
            definitions = (definitions as vscode.Location[]).filter(definition => {
                return !(definition.uri.path.endsWith(virtualCsSuffix));
            });
        }

        if (projection.languageKind === LanguageKind.Html) {
            definitions.forEach(definition => {
                // Because the line pragmas for html are generated referencing the projected document
                // we need to trim them to reference the correct one.
                // This works without further modification for now because the projected html document
                // is actually identical to the original CSHTML (nothing is trimmed) but if that ever
                // gets trimmed we'll have to do something smarter here.
                const uriPath = definition.uri.fsPath || definition.uri.path;
                const path = uriPath.replace(virtualHtmlSuffix, '');
                definition.uri = vscode.Uri.file(path);
            });
        }

        return definitions;
    }
}
