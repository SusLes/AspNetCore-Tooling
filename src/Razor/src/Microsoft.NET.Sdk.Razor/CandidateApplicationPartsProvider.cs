#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;


namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class CandidateApplicationPartsProvider
    {
        internal static readonly HashSet<string> MvcAssemblies = new HashSet<string>(StringComparer.Ordinal)
        {
            "Microsoft.AspNetCore.Mvc.Core",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Mvc.Abstractions",
            "Microsoft.AspNetCore.Mvc.Razor",
            "Microsoft.AspNetCore.Mvc.RazorPages",
            "Microsoft.AspNetCore.Mvc.TagHelpers",
            "Microsoft.AspNetCore.Mvc.ViewFeatures",
            "Microsoft.AspNetCore.Mvc.NewtonsoftJson",
        };

        private readonly Dictionary<string, AssemblyItem> _lookup = new Dictionary<string, AssemblyItem>(StringComparer.Ordinal);

        public CandidateApplicationPartsProvider(List<ResolveReferenceItem> resolveReferenceResult)
        {
            foreach (var item in resolveReferenceResult)
            {
                var assemblyName = item.AssemblyName.Name;
                var assemblyItem = new AssemblyItem(item);
                _lookup[assemblyName] = assemblyItem;
            }
        }

        public (bool applicationReferencesMvc, IList<ResolveReferenceItem>) GetApplicationParts()
        {
            var applicationParts = new List<ResolveReferenceItem>();
            bool applicationReferencesMvc = false;
            foreach (var item in _lookup)
            {
                var classification = Resolve(item.Value);
                if (classification == DependencyClassification.ReferencesMvc)
                {
                    applicationParts.Add(item.Value.ResolveReferenceItem);
                    applicationReferencesMvc = true;
                }
                else if (classification == DependencyClassification.MvcReference)
                {
                    applicationReferencesMvc = true;
                }
            }

            return (applicationReferencesMvc, applicationParts);
        }

        private DependencyClassification Resolve(AssemblyItem assemblyItem)
        {
            if (assemblyItem.DependencyClassification != DependencyClassification.Unknown)
            {
                return assemblyItem.DependencyClassification;
            }

            if (assemblyItem.ResolveReferenceItem == null)
            {
                // We encountered a dependency that isn't part of this assembly's dependency set. We'll see if it happens to an MVC assembly.
                // This might be useful in scenarios where the app does not have a framework reference at the entry point,
                // but the transitive dependency does. Think a Microsoft.NET.Sdk project that hosts a Microsoft.NET.Sdk.Web app.
                assemblyItem.DependencyClassification = MvcAssemblies.Contains(assemblyItem.Name) ?
                    DependencyClassification.MvcReference :
                    DependencyClassification.DoesNotReferenceMvc;

                return assemblyItem.DependencyClassification;
            }

            if (assemblyItem.ResolveReferenceItem.IsSystemReference)
            {
                // We do not allow transitive references to MVC via a framework reference to count. 
                // e.g. depending on Microsoft.AspNetCore.SomeThingNewThatDependsOnMvc would not result in an assembly being treated as
                // referencing MVC.

                assemblyItem.DependencyClassification = MvcAssemblies.Contains(assemblyItem.Name) ?
                    DependencyClassification.MvcReference :
                    DependencyClassification.DoesNotReferenceMvc;

                return assemblyItem.DependencyClassification;
            }

            assemblyItem.References = CalculateReferences(assemblyItem.ResolveReferenceItem.Path);

            var dependencyClassification = DependencyClassification.DoesNotReferenceMvc;
            foreach (var referenceItem in assemblyItem.References)
            {
                var classification = Resolve(referenceItem);
                if (classification == DependencyClassification.MvcReference || classification == DependencyClassification.ReferencesMvc)
                {
                    dependencyClassification = DependencyClassification.ReferencesMvc;
                    break;
                }
            }

            assemblyItem.DependencyClassification = dependencyClassification;
            return dependencyClassification;
        }

        private IReadOnlyList<AssemblyItem> CalculateReferences(string file)
        {
            try
            {
                using var peReader = new PEReader(File.OpenRead(file));
                if (!peReader.HasMetadata)
                {
                    return Array.Empty<AssemblyItem>(); // not a managed assembly
                }

                var metadataReader = peReader.GetMetadataReader();

                var assemblyItems = new List<AssemblyItem>();
                foreach (var handle in metadataReader.AssemblyReferences)
                {
                    var reference = metadataReader.GetAssemblyReference(handle);
                    var referenceName = metadataReader.GetString(reference.Name);

                    if (_lookup.TryGetValue(referenceName, out var assemblyItem))
                    {
                        assemblyItems.Add(assemblyItem);
                    }
                    else
                    {
                        // A dependency references an item that isn't referenced by this project.
                        // We'll construct an item for so that we can calculate the classification based on it's name.
                        assemblyItems.Add(new AssemblyItem(referenceName));
                    }
                }

                return assemblyItems;
            }
            catch (BadImageFormatException)
            {
                // not a PE file, or invalid metadata
            }

            return Array.Empty<AssemblyItem>(); // not a managed assembly
        }

        private enum DependencyClassification
        {
            Unknown,
            DoesNotReferenceMvc,
            ReferencesMvc,
            MvcReference,
        }

        private class AssemblyItem
        {
            public AssemblyItem(ResolveReferenceItem resolveReferenceItem)
                : this(resolveReferenceItem.AssemblyName.Name)
            {
                ResolveReferenceItem = resolveReferenceItem;
            }

            public AssemblyItem(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public ResolveReferenceItem ResolveReferenceItem { get; }

            public DependencyClassification DependencyClassification { get; set; }

            public IReadOnlyList<AssemblyItem> References { get; set; }
        }
    }
}

#endif
