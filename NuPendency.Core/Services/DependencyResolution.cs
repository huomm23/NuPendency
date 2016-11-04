using Ninject.Parameters;
using NuGet;
using NuPendency.Commons.Interfaces;
using NuPendency.Core.Interfaces;
using NuPendency.Interfaces.Model;
using NuPendency.Interfaces.Services;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace NuPendency.Core.Services
{
    internal class DependencyResolution : IDependencyResolution
    {
        private readonly BehaviorSubject<bool> m_Active = new BehaviorSubject<bool>(false);
        private readonly IResolutionFactory m_ResolutionFactory;
        private CancellationToken m_CancellationToken;
        private CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        public DependencyResolution(IResolutionFactory resolutionFactory)
        {
            m_ResolutionFactory = resolutionFactory;
        }

        public IObservable<bool> IsActive => m_Active;

        public void Cancel()
        {
            m_CancellationTokenSource.Cancel();
        }

        public async Task<ResolutionResult> Find(string rootPackageName, FrameworkName targetFramework)
        {
            using (SetActivityFlag())
            {
                m_CancellationTokenSource = new CancellationTokenSource();
                m_CancellationToken = m_CancellationTokenSource.Token;

                var result = new ResolutionResult(rootPackageName, targetFramework);
                var rootPackageInfo = await Resolve(result.Packages, rootPackageName, 0, m_CancellationToken, targetFramework);
                result.RootPackageId = rootPackageInfo.Id;
                return result;
            }
        }

        public Task FindInto(ResolutionResult resultContainer)
        {
            return DoFindInto(resultContainer);
        }

        public Task FindInto(ResolutionResult resultContainer, Version version)
        {
            return DoFindInto(resultContainer, version);
        }

        private async Task DoFindInto(ResolutionResult resultContainer, Version version = null)
        {
            using (SetActivityFlag())
            {
                m_CancellationTokenSource = new CancellationTokenSource();
                m_CancellationToken = m_CancellationTokenSource.Token;

                var rootPackageInfo = await Resolve(resultContainer.Packages, resultContainer.RootPackageName, 0, m_CancellationToken, resultContainer.TargetFramework, version.ToVersionSpec());
                if (rootPackageInfo == null)
                    return;

                resultContainer.RootPackageId = rootPackageInfo.Id;
            }
        }

        private Task<NuGetPackage> Resolve(ObservableCollection<NuGetPackage> packages,
            string packageName,
            int depth,
            CancellationToken cancellationToken,
            FrameworkName targetFramework = null,
            IVersionSpec versionSpec = null)
        {
            var resolutionEngine = m_ResolutionFactory.GetResolutionEngine(packageName);
            return resolutionEngine.Resolve(packages, packageName, depth, cancellationToken, targetFramework,
                versionSpec);
        }

        private IDisposable SetActivityFlag()
        {
            m_Active.OnNext(true);
            return Disposable.Create(() => m_Active.OnNext(false));
        }
    }

    internal class ResolutionFactory : IResolutionFactory
    {
        private readonly IInstanceCreator m_InstanceCreator;

        public ResolutionFactory(IInstanceCreator instanceCreator)
        {
            m_InstanceCreator = instanceCreator;
        }

        public INuGetResolutionEngine GetResolutionEngine(string package)
        {
            return m_InstanceCreator.CreateInstance<INuGetResolutionEngine>(new ConstructorArgument[] { });
        }
    }
}