namespace RepoIntegrityTests.Infrastructure;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

public static class NuGetData
{
    static readonly Dictionary<string, IPackageSearchMetadata> packageInfo = [];
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
            var allVersions = await packageMetadata.GetMetadataAsync(packageId, false, false, cache, NuGet.Common.NullLogger.Instance, default);
            var latest = allVersions.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            packageInfo[packageId] = info = latest;
        }

        return info;
    }
}