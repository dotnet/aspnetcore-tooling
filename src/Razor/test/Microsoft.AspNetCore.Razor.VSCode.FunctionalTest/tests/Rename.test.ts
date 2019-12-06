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

let csPath: string;
let cshtmlPath: string;
let razorPath: string;

suite('Rename', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
    });

    beforeEach(async () => {
        razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        cshtmlPath = path.join(mvcWithComponentsRoot, 'Views', 'Home', 'Index.cshtml');
        csPath = path.join(mvcWithComponentsRoot, 'Test.cs');
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

    test('Can rename symbol within .razor', async () => {
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        const expectedNewText = 'World';
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            razorDoc.uri,
            new vscode.Position(1, 2),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 1, 'Should only rename within the document.');
        const uri = entries[0][0];
        assert.equal(uri.path, razorDoc.uri.path);
        const edits = entries[0][1];
        assert.equal(edits.length, 2);
    });

    test('Can rename symbol within .cshtml', async () => {
        const cshtmlDoc = await vscode.workspace.openTextDocument(cshtmlPath);
        const cshtmlEditor = await vscode.window.showTextDocument(cshtmlDoc);
        const expectedNewText = 'World';
        const firstLine = new vscode.Position(0, 0);
        await cshtmlEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
        await cshtmlEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            cshtmlDoc.uri,
            new vscode.Position(1, 2),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 1, 'Should only rename within the document.');
        const uri = entries[0][0];
        assert.equal(uri.path, cshtmlDoc.uri.path);
        const edits = entries[0][1];
        assert.equal(edits.length, 2);
    });

    test('Rename symbol in .razor also changes .cs', async () => {
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        const expectedNewText = 'Oof';
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@Test.Bar\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            razorDoc.uri,
            new vscode.Position(0, 7),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 2, 'Should have renames in two documents.');

        // Razor file
        const uri1 = entries[0][0];
        assert.equal(uri1.path, vscode.Uri.file(csPath).path);
        const edits1 = entries[0][1];
        assert.equal(edits1.length, 1);

        // cs file
        const uri2 = entries[1][0];
        assert.equal(uri2.path, razorDoc.uri.path);
        const edits2 = entries[1][1];
        assert.equal(edits2.length, 1);
    });

    test('Rename symbol in .cs also changes .razor', async () => {
        const expectedNewText = 'Oof';
        const csDoc = await vscode.workspace.openTextDocument(csPath);
        const csEditor = await vscode.window.showTextDocument(csDoc);

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            csDoc.uri,
            new vscode.Position(4, 30), // Position `public static string F|oo { get; set; }`
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 2, 'Should have renames in two documents.');

        // Razor file
        const uri1 = entries[0][0];
        assert.equal(uri1.path, csDoc.uri.path);
        const edits1 = entries[0][1];
        assert.equal(edits1.length, 1);

        // cs file
        const uri2 = entries[1][0];
        assert.equal(uri2.path, vscode.Uri.file(razorPath).path);
        const edits2 = entries[1][1];
        assert.equal(edits2.length, 1);
    });
});
