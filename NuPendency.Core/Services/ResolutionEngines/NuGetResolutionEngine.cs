using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NuGet;
using NuPendency.Commons.Interfaces;
using NuPendency.Core.Interfaces;
using NuPendency.Interfaces.Model;
using NuPendency.Interfaces.Services;
using Settings = NuPendency.Interfaces.Settings;

namespace NuPendency.Core.Services.ResolutionEngines
{
    internal class NuGetResolutionEngine : ResolutionEngineBase, INuGetResolutionEngine
    {
        private readonly IRepositoryService m_RepositoryService;
        private readonly IResolutionFactory m_ResolutionFactory;
        private readonly ISettingsManager<Settings> m_SettingsManager;

        public NuGetResolutionEngine(IRepositoryService repositoryService, ISettingsManager<Settings> settingsManager, IResolutionFactory resolutionFactory)
        {
            m_RepositoryService = repositoryService;
            m_SettingsManager = settingsManager;
            m_ResolutionFactory = resolutionFactory;
        }

        protected override async Task<PackageBase> DoResolve(ObservableCollection<PackageBase> packages, string packageId, int depth, CancellationToken token, FrameworkName targetFramework = null, IVersionSpec versionSpec = null)
        {
            var packageInfo = await m_RepositoryService.Find(packageId, versionSpec);
            if (packageInfo == null)
                return null;

            var package = packages.FirstOrDefault(pack => pack.PackageId == packageInfo.Id && pack.VersionInfo == packageInfo.Version);
            if (package != null)
                return package;

            if (depth > m_SettingsManager.Settings.MaxSearchDepth)
                return null;

            package = new NuGetPackage(packageInfo.Id, packageInfo.Version, await GetAvailableVersions(packageId))
            {
                Depth = depth
            };
            packages.Add(package);

            if (token.IsCancellationRequested)
                return null;

            foreach (var dependencySet in packageInfo.DependencySets.Where(set => TargetFrameworkMatch(targetFramework, set)))
            {
                foreach (var dependency in dependencySet.Dependencies)
                {
                    var resolutionEngine = m_ResolutionFactory.GetResolutionEngine(dependency.Id);
                    if (resolutionEngine == null)
                        continue;

                    var dependingPackage = await resolutionEngine.Resolve(packages, dependency.Id, depth + 1, token, targetFramework, dependency.VersionSpec) ??
                                           new MissingNuGetPackage(dependency.Id);
                    package.Dependencies.Add(dependingPackage);

                    if (token.IsCancellationRequested)
                        break;
                }

                if (token.IsCancellationRequested)
                    break;
            }
            return package;
        }

        private static bool TargetFrameworkMatch(FrameworkName targetFramework, PackageDependencySet set)
        {
            if (targetFramework == null)
                return true;
            return set.TargetFramework == targetFramework;
        }

        private async Task<Version[]> GetAvailableVersions(string packageId)
        {
            var packageVersions = await m_RepositoryService.FindAllVersions(packageId);
            return packageVersions.Select(pack => pack.Version.Version).ToArray();
        }
    }
}