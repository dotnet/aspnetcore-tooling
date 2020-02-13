/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    mvcWithComponentsRoot,
    pollUntil,
    waitForProjectReady,
} from './TestUtil';

let razorPath: string;
let razorDoc: vscode.TextDocument;
let razorEditor: vscode.TextEditor;

suite('CodeLens', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
    });

    beforeEach(async () => {
        razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        razorDoc = await vscode.workspace.openTextDocument(razorPath);
        razorEditor = await vscode.window.showTextDocument(razorDoc);
    });

    afterEach(async () => {
        await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
        await pollUntil(async () => {
            await vscode.commands.executeCommand('workbench.action.closeAllEditors');
            if (vscode.window.visibleTextEditors.length === 0) {
                return true;
            }

            return false;
        }, /* timeout */ 3000, /* pollInterval */ 500, true /* suppress timeout */);
    });

    test('Can provide CodeLens in .razor file', async () => {

        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

        const codeLenses = await GetCodeLenses(razorDoc.uri);

        assert.equal(codeLenses.length, 1);
        assert.equal(codeLenses[0].isResolved, false);
        assert.equal(codeLenses[0].command, undefined);
    });

    test('Can resolve CodeLens in .razor file', async () => {

        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

        // Second argument makes sure the CodeLens we expect is resolved.
        const codeLenses = await GetCodeLenses(razorDoc.uri, 100);

        assert.equal(codeLenses.length, 1);
        assert.equal(codeLenses[0].isResolved, true);
        assert.notEqual(codeLenses[0].command, undefined);
        assert.equal(codeLenses[0].command!.title, '1 reference');
    });

    async function GetCodeLenses(fileUri: vscode.Uri, resolvedItemCount?: number) {
        return await vscode.commands.executeCommand('vscode.executeCodeLensProvider', fileUri, resolvedItemCount) as vscode.CodeLens[];
    }
});
