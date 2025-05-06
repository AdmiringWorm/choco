using System;
using System.Collections.Generic;
using System.Linq;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.app.services;
using chocolatey.infrastructure.diagnostics;
using NuGet.Packaging;
using NuGet.Versioning;

namespace chocolatey.infrastructure.app.diagnostics
{
    public sealed class DependencyDiagnosticProvider : IDiagnosticProvider
    {
        private readonly IChocolateyPackageInformationService _informationService;
        private readonly INugetService _nugetService;

        public DependencyDiagnosticProvider(INugetService nugetService, IChocolateyPackageInformationService informationService)
        {
            _nugetService = nugetService;
            _informationService = informationService;
        }

        public string Name
        {
            get
            {
                return "deps";
            }
        }

        public string Description
        {
            get
            {
                return "Scans installed packages for missing dependencies, version constraint violations, and pinned conflicts.";
            }
        }

        public IEnumerable<DiagnosticResult> Run(ChocolateyConfiguration configuration)
        {
            var results = new Dictionary<(string Group, string Message), DiagnosticResult>();

            var listConfig = new ChocolateyConfiguration
            {
                CommandName = "list",
                RegularOutput = false,
                QuietOutput = true
            };
            listConfig.ListCommand.LocalOnly = true;

            var installedPackages = _nugetService.List(listConfig).Select(p => p.PackageMetadata).ToList();
            var packageLookup = installedPackages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
            var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in installedPackages)
            {
                var lowerPackageId = package.Id.ToLowerInvariant();

                foreach (var dependency in package.DependencyGroups.SelectMany(g => g.Packages))
                {
                    var lowerDependencyId = dependency.Id.ToLowerInvariant();

                    if (!dependents.ContainsKey(lowerDependencyId))
                    {
                        dependents[lowerDependencyId] = new List<string>();
                    }

                    dependents[lowerDependencyId].Add(lowerPackageId);

                    if (!packageLookup.TryGetValue(lowerDependencyId, out var resolved))
                    {
                        AddResult(results, DiagnosticStatus.Error, "Missing Dependencies",
                            $"'{lowerDependencyId}' required by '{lowerPackageId}' is not installed.", BuildInstallSuggestion(lowerDependencyId, dependency.VersionRange));
                        continue;
                    }

                    var lowerResolvedId = resolved.Id.ToLowerInvariant();

                    if (!dependency.VersionRange.Satisfies(resolved.Version))
                    {
                        AddResult(results, DiagnosticStatus.Error, "Version Constraint Failures",
                            $"'{lowerResolvedId}' version {resolved.Version} does not satisfy the version constraint '{dependency.VersionRange.PrettyPrint()}' declared by '{lowerPackageId}'.", BuildUpgradeSuggestion(lowerDependencyId, dependency.VersionRange, resolved));

                        var pinned = _informationService.Get(resolved)?.IsPinned == true;
                        if (pinned)
                        {
                            AddResult(results, DiagnosticStatus.Warning, "Pinned Packages",
                                $"'{lowerResolvedId}' is pinned and may block a compatible update required by '{lowerPackageId}'.", $"Run 'choco pin remove --name={lowerResolvedId}' to unpin the package.");
                        }
                    }
                }
            }

            if (results.Count == 0)
            {
                AddResult(results, DiagnosticStatus.Success, "Dependency Status", "No dependency issues were found.");
            }

            if (!PrintDependencyTree(installedPackages, packageLookup, dependents))
            {
                AddResult(results, DiagnosticStatus.Suggestion, "Actions", "Run `choco diag deps --fix` to attempt automatic resolution of these issues.");
            }

            return results.Values;
        }

        private string BuildInstallSuggestion(string id, VersionRange versionRange)
        {
            var versionHint = GetExactVersionString(versionRange);

            var installCommand = versionHint is null && versionRange.HasUpperBound
                ? $"choco install {id} --version=<required_version>"
                : (versionHint is null
                  ? $"choco install {id}"
                  : $"choco install {id} --version={versionHint}");

            return $"Run '{installCommand}' to install the missing dependency.";
        }

        private string BuildUpgradeSuggestion(string id, VersionRange versionRange, IPackageMetadata resolved)
        {
            var versionHint = GetExactVersionString(versionRange);
            var allowDowngrade = versionRange.MaxVersion < resolved.Version
                ? " --allow-downgrade"
                : string.Empty;

            var upgradeCommand = versionHint is null && versionRange.HasUpperBound
                ? $"choco upgrade {id} --version=<required_version>{allowDowngrade}"
                : (versionHint is null
                  ? $"choco upgrade {id}"
                  : $"choco upgrade {id} --version={versionHint + allowDowngrade}");

            return $"Run '{upgradeCommand}' to upgrade or downgrade the package to a compatible version.";
        }

        private string GetExactVersionString(VersionRange versionRange)
        {
            if (versionRange.HasLowerAndUpperBounds &&
                versionRange.MinVersion == versionRange.MaxVersion &&
                versionRange.IsMinInclusive &&
                versionRange.IsMaxInclusive)
            {
                return versionRange.MinVersion.ToNormalizedStringChecked();
            }

            return null;
        }

        private bool PrintDependencyTree(List<IPackageMetadata> installedPackages, Dictionary<string, IPackageMetadata> lookup, Dictionary<string, List<string>> dependents)
        {
            var roots = installedPackages
                .Where(pkg => !dependents.ContainsKey(pkg.Id))
                .OrderBy(pkg => pkg.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var root in roots)
            {
                PrintPackageWithChildren(root, installedPackages, lookup, level: 0, isLast: true);
                this.Log().Info("");
            }

            // Print summary if any
            var missingCount = installedPackages
                .SelectMany(p => p.DependencyGroups.SelectMany(g => g.Packages))
                .Count(dep => !lookup.ContainsKey(dep.Id));

            var versionFailures = (from pkg in installedPackages
                                   from dep in pkg.DependencyGroups.SelectMany(g => g.Packages)
                                   where lookup.ContainsKey(dep.Id)
                                   let resolved = lookup[dep.Id]
                                   where !dep.VersionRange.Satisfies(resolved.Version)
                                   select dep).Count();

            if (missingCount > 0)
            {
                this.Log().Warn(" {0} dependencies missing.", missingCount);
            }
            else
            {
                this.Log().Info(" No dependencies missing.");
            }

            if (versionFailures > 0)
            {
                this.Log().Warn(" {0} dependencies failed version constraint.", versionFailures);
            }
            else
            {
                this.Log().Info(" No dependencies failed version constraint.");
            }

            return missingCount == 0 && versionFailures == 0;
        }

        private void PrintPackageWithChildren(
    IPackageMetadata pkg,
    List<IPackageMetadata> all,
    Dictionary<string, IPackageMetadata> lookup,
    int level,
    bool isLast,
    string prefixTracker = "")
        {
            if (level == 0)
            {
                var line = "- " + pkg.Id;

                if (pkg.Version != null)
                {
                    line += " (Installed v" + pkg.Version + ")";
                }

                var isIndependent = level == 0 && all.All(p => !p.DependencyGroups.SelectMany(g => g.Packages).Any(d => d.Id.IsEqualTo(pkg.Id)));

                this.Log().Info(line);
            }

            var children = pkg.DependencyGroups
                .SelectMany(g => g.Packages)
                .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var dep = children[i];
                var isLastChild = (i == children.Count - 1);
                var branch = isLastChild ? "└─ " : "├─ ";

                string newPrefix;

                if (level == 0)
                {
                    newPrefix = isLast ? "  " : "│ ";
                }
                else
                {
                    newPrefix = prefixTracker + (isLast ? "   " : "│  ");
                }

                if (!lookup.ContainsKey(dep.Id))
                {
                    this.Log().Info(newPrefix + branch + dep.Id + " (Missing)");
                }
                else
                {
                    var resolved = lookup[dep.Id];
                    var valid = dep.VersionRange.Satisfies(resolved.Version);
                    var label = valid
                        ? "(Installed v" + resolved.Version + ")"
                        : "(Failed Constraint " + dep.VersionRange.PrettyPrint() + ")";
                    this.Log().Info(newPrefix + branch + resolved.Id + " " + label);

                    PrintPackageWithChildren(resolved, all, lookup, level + 1, isLastChild, newPrefix);
                }
            }
        }

        private void AddResult(Dictionary<(string Group, string Message), DiagnosticResult> results,
            DiagnosticStatus status, string group, string message, string suggestion)
        {
            AddResult(results, status, group, message);
            AddResult(results, DiagnosticStatus.Suggestion, group, suggestion);
        }

        private void AddResult(Dictionary<(string Group, string Message), DiagnosticResult> results,
            DiagnosticStatus status, string group, string message)
        {
            var key = (group, message);
            if (!results.ContainsKey(key))
            {
                results[key] = new DiagnosticResult
                {
                    Status = status,
                    GroupName = group,
                    Message = message
                };
            }
        }
    }
}
