// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class DiscoverApplicationParts : Task
    {
        public string CurrentAssembly { get; set; }

        public ITaskItem[] ResolvedReferences { get; set; }

        public string GeneratedFile { get; set; }

        public override bool Execute()
        {
            var candidateReferences = new List<ITaskItem>();
#if NETCOREAPP
            //while (!System.Diagnostics.Debugger.IsAttached)
            //{
            //    System.Threading.Thread.Sleep(300);
            //}

            var result = new List<ResolveReferenceItem>();
            foreach (var item in ResolvedReferences)
            {
                result.Add(new ResolveReferenceItem
                {
                    AssemblyName = new AssemblyName(item.GetMetadata("FusionName")),
                    IsSystemReference = item.GetMetadata("IsSystemReference") == "true",
                    Path = item.ItemSpec,
                });
            }

            var resolver = new CandidateApplicationPartsProvider(result);
            var (applicationReferencesMvc, parts) = resolver.GetApplicationParts();

            GenerateFile(applicationReferencesMvc, parts);
#endif
            return true;
        }

        private void GenerateFile(bool applicationReferencesMvc, IList<ResolveReferenceItem> parts)
        {
            var fileContent = new StringBuilder();
            
            if (applicationReferencesMvc)
            {
                fileContent.AppendLine(CurrentAssembly);
            }

            foreach (var part in parts)
            {
                fileContent.AppendLine(part.AssemblyName.Name);
            }

            File.WriteAllText(GeneratedFile, fileContent.ToString());
        }
    }

    public class ResolveReferenceItem
    {
        public string Path { get; set; }

        public bool IsSystemReference { get; set; }

        public AssemblyName AssemblyName { get; set; }
    }
}
