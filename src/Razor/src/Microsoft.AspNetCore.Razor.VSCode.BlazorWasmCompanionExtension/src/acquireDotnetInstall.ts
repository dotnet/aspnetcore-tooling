/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';

interface IDotnetAcquireResult {
    dotnetPath: string;
}

export async function acquireDotnetInstall(outputChannel: vscode.OutputChannel): Promise<string> {
    const version = "5.0.0";
    const requestingExtensionId = "ms-blazorwasm-companion";

    try {
        const dotnetResult = await vscode.commands.executeCommand<IDotnetAcquireResult>('dotnet.acquire', { version, requestingExtensionId });
        const dotnetPath = dotnetResult?.dotnetPath;
        if (!dotnetPath) {
            throw new Error("Install step returned an undefined path.");
        }
        return dotnetPath;
    } catch (err) {
        const message = err.msg;
        outputChannel.appendLine(`This extension requires .NET Core to run but we were unable to install it due to the following error:`);
        outputChannel.appendLine(message);
        throw err;
    }
}