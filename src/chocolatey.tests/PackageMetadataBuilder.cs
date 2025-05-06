using Chocolatey.NuGet.Frameworks;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace chocolatey.tests
{
    internal static class PackageMetadataBuilder
    {
        public static IPackageMetadata Build(string id, IEnumerable<PackageDependency> dependencies = default)
        {
            Mock<IPackageMetadata> mock = Create(id, dependencies);

            return mock.Object;
        }

        public static Mock<IPackageMetadata> Create(string id, IEnumerable<PackageDependency> dependencies)
        {
            var mock = new Mock<IPackageMetadata>();
            mock.Setup(m => m.Id).Returns(id);
            mock.Setup(m => m.DependencyGroups).Returns(new[]
            {
                new PackageDependencyGroup(
                    NuGetFramework.AnyFramework,
                    dependencies ?? Enumerable.Empty<PackageDependency>())
            });
            return mock;
        }
    }
}