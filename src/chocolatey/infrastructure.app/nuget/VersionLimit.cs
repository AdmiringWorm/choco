using System;
using NuGet.Versioning;

namespace chocolatey.infrastructure.app.nuget
{
    public sealed class VersionLimit
    {
        private readonly FloatRange _floatRange;
        private readonly VersionRange _versionRange;

        private VersionLimit(FloatRange floatRange, VersionRange versionRange)
        {
            _floatRange = floatRange;
            _versionRange = versionRange;
        }

        public bool HasPrerelease
        {
            get
            {
                return !string.IsNullOrEmpty(_floatRange?.OriginalReleasePrefix)
                    || !string.IsNullOrEmpty(_versionRange?.OriginalString);
            }
        }

        public static VersionLimit Create(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

            if (FloatRange.TryParse(version, out var floatRange))
            {
                return new VersionLimit(floatRange, null);
            }
            
            if (VersionRange.TryParse(version, allowFloating: false, out var versionRange))
            {
                return new VersionLimit(null, versionRange);
            }

            throw new ApplicationException("The passed version '{0}' is not a valid floating or version range.".FormatWith(version));
        }

        public bool Satisfies(NuGetVersion nuGetVersion)
        {
            if (!(_floatRange is null))
            {
                return _floatRange.Satisfies(nuGetVersion);
            }
            else if (!(_versionRange is null))
            {
                return _versionRange.Satisfies(nuGetVersion);
            }

            return true;
        }

        public override string ToString()
        {
            if (!(_floatRange is null))
            {
                return _floatRange.ToString();
            }
            else if (!(_versionRange is null))
            {
                return _versionRange.ToString();
            }
            else
            {
                return "(null)";
            }
        }
    }
}