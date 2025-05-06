using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.app.nuget;
using chocolatey.infrastructure.app.services;
using chocolatey.infrastructure.validations;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace chocolatey.infrastructure.app.validations
{
    public class DependencyValidation : IValidation
    {
        private INugetService _nugetService;
        private readonly IChocolateyPackageInformationService _informationService;

        public DependencyValidation(INugetService nugetService, IChocolateyPackageInformationService informationService)
        {
            _nugetService = nugetService;
            _informationService = informationService;
        }

        public ICollection<ValidationResult> Validate(ChocolateyConfiguration config)
        {
            if (!config.CommandName.IsEqualTo("install") && !config.CommandName.IsEqualTo("upgrade"))
            {
                return Array.Empty<ValidationResult>();
            }

            var validationResults = new List<ValidationResult>();

            var listConfig = new ChocolateyConfiguration
            {
                CommandName = "list",
                RegularOutput = false,
                QuietOutput = true
            };
            listConfig.ListCommand.LocalOnly = true;

            var installedPackages = _nugetService.List(listConfig).Select(p => p.PackageMetadata).ToList();

            var installedPackageIds = installedPackages
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var exitCode = config.Features.StopOnFirstPackageFailure ? 1 : 0;
            var validationStatus = config.Features.StopOnFirstPackageFailure && !config.IgnoreDependencies
                ? ValidationStatus.Error
                : ValidationStatus.Warning;

            foreach (var package in installedPackages)
            {
                foreach (var dependency in package.DependencyGroups.SelectMany(group => group.Packages))
                {
                    if (IsInstallingPackage(dependency, config))
                    {
                        continue;
                    }

                    if (!installedPackageIds.TryGetValue(dependency.Id, out var resolved))
                    {
                        validationResults.Add(new ValidationResult
                        {
                            Status = validationStatus,
                            Message = $"Required dependency '{dependency.Id}' is missing. It was not found among the locally install packages.",
                            ExitCode = exitCode
                        });

                        validationResults.Add(BuildInstallSuggestionResult(dependency.Id, dependency.VersionRange));

                        continue;
                    }

                    if (!dependency.VersionRange.Satisfies(resolved.Version))
                    {
                        var versionText = dependency.VersionRange.PrettyPrint();
                        var resolvedVersion = resolved.Version.ToNormalizedStringChecked();

                        validationResults.Add(new ValidationResult
                        {
                            Status = validationStatus,
                            Message = $"Dependency '{dependency.Id}' does not satisfy the required version range. Required: {versionText}, found: {resolvedVersion}.",
                            ExitCode = exitCode
                        });

                        if (_informationService.Get(resolved)?.IsPinned == true)
                        {
                            validationResults.Add(new ValidationResult
                            {
                                Status = validationStatus,
                                Message = $"The package '{dependency.Id}' is pinned and may prevent installing a compatible version. " +
                                      $"Consider unpinning it using: choco pin remove --name={dependency.Id}",
                                ExitCode = exitCode
                            });
                        }

                        validationResults.Add(BuildUpgradeSuggestionResult(dependency.Id, dependency.VersionRange, resolved));
                    }
                }
            }

            if (validationResults.Count == 0)
            {
                validationResults.Add(new ValidationResult
                {
                    Status = ValidationStatus.Success,
                    ExitCode = 0,
                    Message = "No dependency issues were found."
                });
            }

            return validationResults;
        }

        private bool IsInstallingPackage(PackageDependency dependency, ChocolateyConfiguration config)
        {
            var packages = config.PackageNames.Split(' ', ';');

            if (packages.Length == 0)
            {
                packages = config.Input.Split(' ', ';');
            }

            return packages.Contains(dependency.Id, StringComparer.OrdinalIgnoreCase);
        }

        private ValidationResult BuildInstallSuggestionResult(string id, VersionRange versionRange)
        {
            var versionHint = GetExactVersionString(versionRange);
            var installCommand = versionHint is null && versionRange.HasUpperBound
                ? $"choco install {id} --version=<required_version>"
                : (versionHint is null
                  ? $"choco install {id}"
                  : $"choco install {id} --version={versionHint}");

            return new ValidationResult
            {
                Status = ValidationStatus.Suggestion,
                Message = $"Install missing dependency using: {installCommand}",
                ExitCode = 0
            };
        }

        private ValidationResult BuildUpgradeSuggestionResult(string id, VersionRange versionRange, IPackageMetadata resolved)
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

            return new ValidationResult
            {
                Status = ValidationStatus.Suggestion,
                Message = $"Upgrade or downgrade incompatible dependency using: {upgradeCommand}",
                ExitCode = 0
            };
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

        [Obsolete("Use Validate method instead. This overload method will be removed in v3.")]
        public ICollection<ValidationResult> validate(ChocolateyConfiguration config)
        {
            return Validate(config);
        }
    }
}
