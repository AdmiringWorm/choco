using chocolatey.infrastructure.app;
using chocolatey.infrastructure.filesystem;
using NuGet.Packaging;
using NuGet.Resolver;
using NuGet.Versioning;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using chocolatey.infrastructure.logging;
using chocolatey.infrastructure.results;

public class DependencyValidationIssue
{
    public string PackageId { get; set; }
    public string IssueType { get; set; } // "Missing", "Conflict", "Pinned"
    public string Message { get; set; }
}

public static class DependencyValidator
{
    public static List<DependencyValidationIssue> ValidateResolverContext(
        PackageResolverContext context,
        IEnumerable<PackageResult> installedPackages,
        IFileSystem fileSystem,
        ILog logger)
    {
        var issues = new List<DependencyValidationIssue>();
        var available = context.AvailablePackages.ToList();
        var requiredIds = new HashSet<string>(context.RequiredPackageIds, StringComparer.OrdinalIgnoreCase);
        var availableIds = new HashSet<string>(available.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
        var installed = installedPackages.ToList();

        foreach (var reqId in requiredIds)
        {
            var availableForId = available.Where(p => p.Id.Equals(reqId, StringComparison.OrdinalIgnoreCase)).ToList();
            var installedPkg = installed.FirstOrDefault(p => p.Name.Equals(reqId, StringComparison.OrdinalIgnoreCase));

            if (!availableIds.Contains(reqId))
            {
                var msg = $"❌ Missing: Required package '{reqId}' not found in available sources or local cache.";
                logger.Warn(msg);
                issues.Add(new DependencyValidationIssue { PackageId = reqId, IssueType = "Missing", Message = msg });
                continue;
            }

            // Simulate version expectation from available info
            var expectedVersions = availableForId.Select(p => p.Version).Distinct().ToList();
            if (installedPkg != null && expectedVersions.Any())
            {
                var installedVersion = installedPkg.Version;
                var installedNuGetVersion = NuGetVersion.Parse(installedVersion);

                var satisfiesAny = expectedVersions.Any(v => v == installedNuGetVersion);

                if (!satisfiesAny)
                {
                    var msg = $"⚠️ Conflict: Installed '{reqId} v{installedVersion}' does not match any available required version [{string.Join(", ", expectedVersions)}].";
                    logger.Warn(msg);
                    issues.Add(new DependencyValidationIssue { PackageId = reqId, IssueType = "Conflict", Message = msg });
                }
            }

            // Pinned check
            var pinPath = Path.Combine(ApplicationParameters.PackagesLocation, reqId + ".pin");
            if (fileSystem.FileExists(pinPath))
            {
                var msg = $"🔒 Pinned: '{reqId}' is pinned. It may block upgrades or version alignment.";
                logger.Info(msg);
                issues.Add(new DependencyValidationIssue { PackageId = reqId, IssueType = "Pinned", Message = msg });
            }
        }

        return issues;
    }
}
