// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    /// <summary>
    /// This class will progamatically clear all stored output state on its <see cref="DocumentSnapshot"/> to prevent committed memory issues.
    /// Today this class is used to enable FAR for Razor files which result in every Razor file in a project being generated behind the scenes.
    /// </summary>
    internal class GeneratedDocumentTextLoader : TextLoader
    {
        private readonly DocumentSnapshot _document;
        private readonly string _filePath;
        private readonly VersionStamp _version;

        public GeneratedDocumentTextLoader(DocumentSnapshot document, string filePath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _document = document;
            _filePath = filePath;
            _version = VersionStamp.Create();
        }

        public override async Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            var output = await _document.GetGeneratedOutputAsync().ConfigureAwait(false);

            // After generating output for our DocumentSnapshot we clear the stored state to allow for the GC to clean up any unused SyntaxTree's/IR documents.
            // If another system needs to re-compute the generated output they can do so it just wont be cached.
            _document.ClearStoredState();

            // Providing an encoding here is important for debuggability. Without this edit-and-continue
            // won't work for projects with Razor files.
            return TextAndVersion.Create(SourceText.From(output.GetCSharpDocument().GeneratedCode, Encoding.UTF8), _version, _filePath);
        }
    }
}
