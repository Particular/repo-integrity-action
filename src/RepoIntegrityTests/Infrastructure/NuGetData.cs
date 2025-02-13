namespace RepoIntegrityTests.Infrastructure;

using System.Collections.Concurrent;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

public static class NuGetData
{
    static readonly ConcurrentDictionary<string, IPackageSearchMetadata> packageInfo = [];
    static readonly PackageMetadataResource packageMetadata;
    static readonly SourceCacheContext cache = new();

    static NuGetData()
    {
        var nugetRepo = new SourceRepository(new PackageSource("https://api.nuget.org/v3/index.json"), NuGet.Protocol.Core.Types.Repository.Provider.GetCoreV3());
        packageMetadata = nugetRepo.GetResource<PackageMetadataResource>();
    }

    public static async Task<IPackageSearchMetadata> GetPackageInfo(string packageId)
    {
        if (!packageInfo.TryGetValue(packageId, out var info))
        {
            var allVersions = await packageMetadata.GetMetadataAsync(packageId, false, false, cache, NuGet.Common.NullLogger.Instance, CancellationToken.None);
            info = allVersions.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            packageInfo.TryAdd(packageId, info);
        }

        return info;
    }
}