﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // The VSCode OmniSharp client starts the RazorServer before all of its handlers are registered
    // because of this we need to wait until everthing is initialized to make some client requests like
    // razor\serverReady. This class takes a TCS which will complete when everything is initialized
    // ensuring that no requests are sent before the client is ready.
    internal class ClientNotifierService
    {
        private TaskCompletionSource<bool> _initializedCompletionSource;
        private IClientLanguageServer _languageServer;

        public ClientNotifierService(IClientLanguageServer languageServer, TaskCompletionSource<bool> initializedCompletionSource)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (initializedCompletionSource is null)
            {
                throw new ArgumentNullException(nameof(initializedCompletionSource));
            }

            _languageServer = languageServer;
            _initializedCompletionSource = initializedCompletionSource;
        }

        public async Task<IResponseRouterReturns> SendRequestAsync(string method)
        {
            await _initializedCompletionSource.Task;

            return _languageServer.SendRequest(method);
        }

        public async Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
        {
            await _initializedCompletionSource.Task;

            return _languageServer.SendRequest<T>(method, @params);
        }
    }
}
