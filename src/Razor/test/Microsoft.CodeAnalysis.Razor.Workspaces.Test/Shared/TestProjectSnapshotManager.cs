﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Moq;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class TestProjectSnapshotManager : DefaultProjectSnapshotManager
    {
        public TestProjectSnapshotManager(Workspace workspace)
            : base(CreateForegroundDispatcher(), Mock.Of<ErrorReporter>(MockBehavior.Strict), Enumerable.Empty<ProjectSnapshotChangeTrigger>(), workspace)
        {
        }

        public TestProjectSnapshotManager(ForegroundDispatcher foregroundDispatcher, Workspace workspace)
            : base(foregroundDispatcher, Mock.Of<ErrorReporter>(MockBehavior.Strict), Enumerable.Empty<ProjectSnapshotChangeTrigger>(), workspace)
        {
        }

        public bool AllowNotifyListeners { get; set; }

        private static ForegroundDispatcher CreateForegroundDispatcher()
        {
            var dispatcher = new Mock<ForegroundDispatcher>(MockBehavior.Strict);
            dispatcher.Setup(d => d.AssertForegroundThread(It.IsAny<string>())).Verifiable();
            return dispatcher.Object;
        }

        public DefaultProjectSnapshot GetSnapshot(HostProject hostProject)
        {
            return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
        }

        public DefaultProjectSnapshot GetSnapshot(Project workspaceProject)
        {
            return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == workspaceProject.FilePath);
        }

        protected override void NotifyListeners(ProjectChangeEventArgs e)
        {
            if (AllowNotifyListeners)
            {
                base.NotifyListeners(e);
            }
        }
    }
}
