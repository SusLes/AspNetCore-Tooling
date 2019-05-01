﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultDocumentSnapshotTest : WorkspaceTestBase
    {
        public DefaultDocumentSnapshotTest()
        {
            SourceText = SourceText.From("<p>Hello World</p>");
            Version = VersionStamp.Create();

            // Create a new HostDocument to avoid mutating the code container
            ComponentCshtmlHostDocument = new HostDocument(TestProjectData.SomeProjectCshtmlComponentFile5);
            ComponentHostDocument = new HostDocument(TestProjectData.SomeProjectComponentFile1);
            LegacyHostDocument = new HostDocument(TestProjectData.SomeProjectFile1);
            NestedComponentHostDocument = new HostDocument(TestProjectData.SomeProjectNestedComponentFile3);

            var projectState = ProjectState.Create(Workspace.Services, TestProjectData.SomeProject);
            var project = new DefaultProjectSnapshot(projectState);

            var textAndVersion = TextAndVersion.Create(SourceText, Version);

            var documentState = DocumentState.Create(Workspace.Services, LegacyHostDocument, () => Task.FromResult(textAndVersion));
            LegacyDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, ComponentHostDocument, () => Task.FromResult(textAndVersion));
            ComponentDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, ComponentCshtmlHostDocument, () => Task.FromResult(textAndVersion));
            ComponentCshtmlDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, NestedComponentHostDocument, () => Task.FromResult(textAndVersion));
            NestedComponentDocument = new DefaultDocumentSnapshot(project, documentState);
        }

        private SourceText SourceText { get; }

        private VersionStamp Version { get; }

        private HostDocument ComponentHostDocument { get; }

        private HostDocument ComponentCshtmlHostDocument { get; }

        private HostDocument LegacyHostDocument { get; }

        private DefaultDocumentSnapshot ComponentDocument { get; }

        private DefaultDocumentSnapshot ComponentCshtmlDocument { get; }

        private DefaultDocumentSnapshot LegacyDocument { get; }

        private HostDocument NestedComponentHostDocument { get; }

        private DefaultDocumentSnapshot NestedComponentDocument { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            services.Add(new TestTagHelperResolver());
        }

        [Fact]
        public async Task ClearStoredState_OnRegenerationMaintainsOutputVersion()
        {
            // Arrange
            var initialOutputVersion = await LegacyDocument.GetGeneratedOutputVersionAsync();
            LegacyDocument.ClearStoredState();

            // Act
            var regeneratedOutputVersion = await LegacyDocument.GetGeneratedOutputVersionAsync();

            // Assert
            Assert.Equal(initialOutputVersion, regeneratedOutputVersion);
        }

        [Fact]
        public async Task ClearStoredState_RemovesCachedOutput()
        {
            // Arrange
            await LegacyDocument.GetGeneratedOutputAsync();

            // Act
            LegacyDocument.ClearStoredState();

            // Assert
            Assert.False(LegacyDocument.TryGetGeneratedOutput(out _));
            Assert.False(LegacyDocument.TryGetGeneratedOutputVersionAsync(out _));
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_SetsHostDocumentOutput()
        {
            // Act
            await LegacyDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(LegacyHostDocument.GeneratedCodeContainer.Output);
            Assert.Same(SourceText, LegacyHostDocument.GeneratedCodeContainer.Source);
        }

        // This is a sanity test that we invoke component codegen for components. It's a little fragile but
        // necessary.

        [Fact]
        public async Task GetGeneratedOutputAsync_CshtmlComponent_ContainsComponentImports()
        {
            // Act
            await ComponentCshtmlDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(ComponentCshtmlHostDocument.GeneratedCodeContainer.Output);
            Assert.Contains("using Microsoft.AspNetCore.Components", ComponentCshtmlHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
        }
        [Fact]
        public async Task GetGeneratedOutputAsync_Component()
        {
            // Act
            await ComponentDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(ComponentHostDocument.GeneratedCodeContainer.Output);
            Assert.Contains("ComponentBase", ComponentHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_NestedComponentDocument_SetsCorrectNamespaceAndClassName()
        {
            // Act
            await NestedComponentDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(NestedComponentHostDocument.GeneratedCodeContainer.Output);
            Assert.Contains("ComponentBase", NestedComponentHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
            Assert.Contains("namespace SomeProject.Nested", NestedComponentHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
            Assert.Contains("class File3", NestedComponentHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
        }

        // This is a sanity test that we invoke legacy codegen for .cshtml files. It's a little fragile but
        // necessary.
        [Fact]
        public async Task GetGeneratedOutputAsync_Legacy()
        {
            // Act
            await LegacyDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(LegacyHostDocument.GeneratedCodeContainer.Output);
            Assert.Contains("Template", LegacyHostDocument.GeneratedCodeContainer.Output.GeneratedCode);
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_SetsOutputWhenDocumentIsNewer()
        {
            // Arrange
            var newSourceText = SourceText.From("NEW!");
            var newDocumentState = LegacyDocument.State.WithText(newSourceText, Version.GetNewerVersion());
            var newDocument = new DefaultDocumentSnapshot(LegacyDocument.ProjectInternal, newDocumentState);

            // Force the output to be the new output
            await LegacyDocument.GetGeneratedOutputAsync();

            // Act
            await newDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(LegacyHostDocument.GeneratedCodeContainer.Output);
            Assert.Same(newSourceText, LegacyHostDocument.GeneratedCodeContainer.Source);
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_OnlySetsOutputIfDocumentNewer()
        {
            // Arrange
            var newSourceText = SourceText.From("NEW!");
            var newDocumentState = LegacyDocument.State.WithText(newSourceText, Version.GetNewerVersion());
            var newDocument = new DefaultDocumentSnapshot(LegacyDocument.ProjectInternal, newDocumentState);

            // Force the output to be the new output
            await newDocument.GetGeneratedOutputAsync();

            // Act
            await LegacyDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.NotNull(LegacyHostDocument.GeneratedCodeContainer.Output);
            Assert.Same(newSourceText, LegacyHostDocument.GeneratedCodeContainer.Source);
        }
    }
}
