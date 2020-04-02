/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { CompositeCodeActionTranslator } from './CodeActions/CompositeRazorCodeActionTranslator';
import { getRazorDocumentUri, isRazorCSharpFile } from './RazorConventions';
import { RazorLanguageServiceClient } from './RazorLanguageServiceClient';
import { RazorLogger } from './RazorLogger';
import { LanguageKind } from './RPC/LanguageKind';

// This interface should exactly match the `LanguageMiddleware` interface defined in Omnisharp.
// https://github.com/OmniSharp/omnisharp-vscode/blob/master/src/omnisharp/LanguageMiddlewareFeature.ts#L9-L16
interface LanguageMiddleware {

    language: string;

    remapWorkspaceEdit?(workspaceEdit: vscode.WorkspaceEdit, token: vscode.CancellationToken): vscode.ProviderResult<vscode.WorkspaceEdit>;

    remapLocations?(locations: vscode.Location[], token: vscode.CancellationToken): vscode.ProviderResult<vscode.Location[]>;
}

export class RazorCSharpLanguageMiddleware implements LanguageMiddleware {
    public readonly language = 'Razor';

    constructor(
        private readonly serviceClient: RazorLanguageServiceClient,
        private readonly logger: RazorLogger,
        private readonly compositeCodeActionTranslator: CompositeCodeActionTranslator) {}

    public async remapWorkspaceEdit(workspaceEdit: vscode.WorkspaceEdit, token: vscode.CancellationToken) {
        const map = new Map<vscode.Uri, vscode.TextEdit[]>();

        // The returned edits will be for the projected C# documents. We now need to re-map that to the original document.
        for (const entry of workspaceEdit.entries()) {
            const uri = entry[0];
            const edits = entry[1];

            if (!isRazorCSharpFile(uri)) {
                // This edit happens outside of a Razor document. Let the edit go through as is.
                map.set(uri, edits);
                continue;
            }

            // We're now working with a Razor CSharp document.
            const documentUri = getRazorDocumentUri(uri);

            // Re-map each edit to its position in the original Razor document.
            for (const edit of edits) {
                const remappedResponse = await this.serviceClient.mapToDocumentRange(
                    LanguageKind.CSharp,
                    edit.range,
                    documentUri);

                if (!remappedResponse || !remappedResponse.range) {
                    // This is kind of wrong. Workspace edits commonly consist of a bunch of different edits which
                    // don't make sense individually. If we try to introspect on them individually there won't be
                    // enough context to do anything intelligent. But we also need to know if the edit can just be handled by mapToDocumentRange.
                    // We're not solving that now, because we're going to have to change how we handle CodeAction edits from a per-action model
                    // to a symantic model anyway, but I needed to call it out here so we remember.
                    const [codeActionUri, codeActionEdit] = this.tryApplyingCodeActions(uri, edit);

                    if (codeActionUri && codeActionEdit) {
                        this.addElementToDictionary(map, codeActionUri, codeActionEdit);
                    } else {
                        // Something went wrong when re-mapping to the original document. Ignore this edit.
                        this.logger.logWarning(`Unable to remap file ${uri.path} at ${edit.range}.`);
                        continue;
                    }
                } else {
                    // Similar to above, this is kind of wrong. We're manually trimming whitespace and adjusting
                    // for multi-line edits that're provided by O#. Right now, we do not support multi-line edits
                    // (ex. refactoring code actions) however there are certain supported edits which O# is automatically
                    // formatting for us (ex. FullyQualifiedNamespace) into multiple lines, when it should span a single line.
                    // This is due to how we render our virtual cs files, with fewer levels of indentation to facilitate
                    // appropriate error reporting (if we had additional tabs, then the error squigly would appear offset).
                    //
                    // The ideal solution for this would do something like:
                    // https://github.com/dotnet/aspnetcore-tooling/blob/master/src/Razor/src/Microsoft.AspNetCore.Razor.LanguageServer/Formatting/DefaultRazorFormattingService.cs#L264
                    // however we're going to hold off on that for now as it isn't immediately necessary and we don't
                    // (currently) support any other kind of multi-line edits.

                    const newText = edit.newText.trim();
                    let remappedRange = remappedResponse.range;

                    // The starting and ending range may be equal in the case when we have other items on the same line. Ex:
                    // Render|Tree apple
                    // where `|` is the cursor. We want to ensure we dont't overwrite `apple` in this case with our edit.
                    if (newText !== edit.newText && !remappedResponse.range.start.isEqual(remappedResponse.range.end)) {
                        const end = new vscode.Position(remappedResponse.range.start.line, remappedResponse.range.start.character + newText.length);
                        remappedRange = new vscode.Range(remappedResponse.range.start, end);
                    }

                    this.logger.logVerbose(
                        `Re-mapping text ${newText} at ${edit.range} in ${uri.path} to ${remappedRange} in ${documentUri.path}`);

                    const newEdit = new vscode.TextEdit(remappedRange, newText);
                    this.addElementToDictionary(map, documentUri, newEdit);
                }
            }
        }
        const result = this.mapToTextEdit(map);

        return result;
    }

    public async remapLocations(locations: vscode.Location[], token: vscode.CancellationToken) {
        const result: vscode.Location[] = [];

        for (const location of locations) {
            if (!isRazorCSharpFile(location.uri)) {
                // This location exists outside of a Razor document. Leave it unchanged.
                result.push(location);
                continue;
            }

            // We're now working with a Razor CSharp document.
            const documentUri = getRazorDocumentUri(location.uri);
            const remappedResponse = await this.serviceClient.mapToDocumentRange(
                LanguageKind.CSharp,
                location.range,
                documentUri);

            if (!remappedResponse || !remappedResponse.range) {
                // Something went wrong when re-mapping to the original document. Ignore this location.
                this.logger.logWarning(`Unable to remap file ${location.uri.path} at ${location.range}.`);
                continue;
            }

            const newLocation = new vscode.Location(documentUri, remappedResponse.range);
            result.push(newLocation);

            this.logger.logVerbose(
                `Re-mapping location ${location.range} in ${location.uri.path} to ${remappedResponse.range} in ${documentUri.path}`);
        }

        return result;
    }

    private mapToTextEdit(map: Map<vscode.Uri, vscode.TextEdit[]>): vscode.WorkspaceEdit {
        const result = new vscode.WorkspaceEdit();
        map.forEach((value, key) => {
            result.set(key, value);
        });

        return result;
    }

    private addElementToDictionary(map: Map<vscode.Uri, vscode.TextEdit[]>, uri: vscode.Uri, edit: vscode.TextEdit) {
        let mapArray: vscode.TextEdit[] | undefined;

        if (map.has(uri)) {
            mapArray = map.get(uri);
            if (mapArray) {
                mapArray.push(edit);
            }
        } else {
            const editArray = new Array<vscode.TextEdit>();
            editArray.push(edit);
            map.set(uri, editArray);
        }
    }

    private tryApplyingCodeActions(uri: vscode.Uri, edit: vscode.TextEdit): [ vscode.Uri?, vscode.TextEdit?] {
        return this.compositeCodeActionTranslator.applyEdit(uri, edit);
    }
}
