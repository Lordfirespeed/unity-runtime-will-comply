using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Build;

public static class Program
{
    static readonly PackageIdentity NetStandardRefPackageIdentity = new("netstandard.library.ref", new(2, 1, 0));
    static readonly HashSet<string> WhitelistedRefFileExtensions = [".dll"];
    // source: R.E.P.O. @ 2025-05-09, Unity 2022.3.21
    private static readonly HashSet<string> NetStandardAssemblyNamesWhichUnityRuntimeHas = [
        "System",
        "System.ComponentModel.Composition",
        "System.Core",
        "System.Data",
        "System.Drawing",
        "System.IO.Compression",
        "System.IO.Compression.FileSystem",
        "System.Net.Http",
        "System.Numerics",
        "System.Runtime.Serialization",
        "System.Transactions",
        "System.Xml",
        "System.Xml.Linq",
        "mscorlib",
        "netstandard",
    ];

    static readonly SourceCacheContext SourceCache = new();
    static readonly PackageSource Source = new("https://api.nuget.org/v3/index.json");
    static readonly SourceRepository SourceRepository = Repository.Factory.GetCoreV3(Source);
    static readonly PackageDownloadContext PackageDownloadContext = new(SourceCache);

    static DirectoryInfo SolutionDirectory { get; } = new(Environment.CurrentDirectory);
    private static DirectoryInfo TempDirectory { get; } = new(Path.Join(Path.GetTempPath(), $"unity-runtime-will-comply-{Path.GetRandomFileName()}"));

    public static async Task<int> Main(string[] args)
    {
        TempDirectory.Create();
        Console.WriteLine(TempDirectory.FullName);
        var result = await DownloadNuGetPackage(NetStandardRefPackageIdentity, CancellationToken.None);
        var itemsToExtract = (await FindRefItems(result.PackageReader, CancellationToken.None)).ToArray();
        var extractedFilePaths = await ExtractNuGetPackageItems(result.PackageReader, TempDirectory, itemsToExtract, CancellationToken.None);

        var allAssemblies = extractedFilePaths.Select(itemPath => new FileInfo(itemPath))
            .GroupBy(file => NetStandardAssemblyNamesWhichUnityRuntimeHas.Contains(Path.GetFileNameWithoutExtension(file.Name)))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var redundantAssemblies = allAssemblies[true];
        foreach (var file in redundantAssemblies) file.Delete();
        var usefulAssemblies = allAssemblies[false];

        var assemblyRewriteTasks = usefulAssemblies.Select(
            file => Task.Run(() => RewriteAssemblyAttributes(file))
        );
        await Task.WhenAll(assemblyRewriteTasks);

        return 0;
    }

    public static async ValueTask<DownloadResourceResult> DownloadNuGetPackage(PackageIdentity identity, CancellationToken token)
    {
        var pathContext = NuGetPathContext.Create(SolutionDirectory.FullName);
        var downloadResource = await SourceRepository.GetResourceAsync<DownloadResource>(token);

        var result = await downloadResource.GetDownloadResourceResultAsync(
            identity,
            PackageDownloadContext,
            pathContext.UserPackageFolder,
            NullLogger.Instance,
            token
        );

        if (result is null)
            throw new InvalidOperationException();

        return result;
    }

    public static async ValueTask<IEnumerable<string>> FindRefItems(PackageReaderBase packageReader, CancellationToken token)
    {
        var refItemsGroupPerFramework = await packageReader.GetItemsAsync(PackagingConstants.Folders.Ref, token);
        if (refItemsGroupPerFramework is null) throw new InvalidOperationException();
        var refItemsGroupForNearestFramework = NuGetFrameworkUtility.GetNearest(
            refItemsGroupPerFramework,
            NuGetFramework.Parse("netstandard2.1"),
            group => group.TargetFramework
        );
        if (refItemsGroupForNearestFramework is null) throw new InvalidOperationException();
        return refItemsGroupForNearestFramework.Items
            .Where(HasWhitelistedExtension);

        bool HasWhitelistedExtension(string item) => WhitelistedRefFileExtensions.Contains(Path.GetExtension(item));
    }

    public static async ValueTask<IEnumerable<string>> ExtractNuGetPackageItems(PackageReaderBase packageReader, DirectoryInfo destination, string[] items, CancellationToken token)
    {
        var packageFileExtractor = new PackageFileExtractor(items, XmlDocFileSaveMode.Skip);
        return await packageReader.CopyFilesAsync(
            destination.FullName,
            items,
            packageFileExtractor.ExtractPackageFile,
            NullLogger.Instance,
            token
        );
    }

    public static bool ShouldKeepAttribute(CustomAttribute attribute)
    {
        if (attribute.AttributeType.Name == "ReferenceAssemblyAttribute") return false;
        return true;
    }

    public static void RewriteAssemblyAttributes(FileInfo file)
    {
        AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(file.FullName);

        var rewrittenAttributes = assembly.CustomAttributes
            .Where(ShouldKeepAttribute)
            .ToArray();

        assembly.CustomAttributes.Clear();
        foreach (var attribute in rewrittenAttributes) assembly.CustomAttributes.Add(attribute);
        file.Delete();
        assembly.Write(file.FullName);
    }
}
