using System;
using chocolatey.infrastructure.app.domain;
using FluentAssertions;

namespace chocolatey.tests
{
    public class EnumExtensionsTests
    {
        [Fact]
        public void GetDistinctNames_excludes_enum_aliases()
        {
            var names = EnumExtensions.GetDistinctNames<PackageOrder>();
            names.Should().NotContain(nameof(PackageOrder.DownloadCount))
                .And.NotContain(nameof(PackageOrder.Server))
                .And.Contain(nameof(PackageOrder.Popularity))
                .And.Contain(nameof(PackageOrder.Unsorted));

        }

        [Fact]
        public void GetDistinctNames_removes_aliases_and_sorts_alphabetically()
        {
            // Act
            var names = EnumExtensions.GetDistinctNames<PackageOrder>();

            // Assert
            names.Should()
                .NotContain("DownloadCount", "because it is an alias for 'Popularity'")
                .And.NotContain("Server", "because it is an alias for 'Unsorted'")
                .And.Contain(new[] { "Id", "Title", "Popularity", "LastPublished", "Unsorted" })
                .And.BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        }
    }
}
