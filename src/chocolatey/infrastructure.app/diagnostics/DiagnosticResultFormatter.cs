using System.Collections.Generic;
using System.Linq;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.diagnostics;
using chocolatey.infrastructure.logging;

namespace chocolatey.infrastructure.app.diagnostics
{
    public static class DiagnosticResultFormatter
    {
        public static void Print(IEnumerable<DiagnosticResult> results, ChocolateyConfiguration configuration)
        {
            if (results is null || !results.Any())
            {
                LogHeader(configuration, "No diagnostic results to display.");
                return;
            }

            var grouped = results
                .GroupBy(r => r.Status)
                .OrderBy(g => GetSeverityOrder(g.Key));

            foreach (var statusGroup in grouped)
            {
                foreach (var group in statusGroup.GroupBy(r => r.GroupName))
                {
                    var heading = GetHeading(statusGroup.Key, group.Key);

                    LogHeader(configuration, heading, statusGroup.Key);

                    foreach (var result in group)
                    {
                        if (configuration.RegularOutput)
                        {
                            var icon = GetStatusIcon(statusGroup.Key);
                            LogStatus(result.Status, "- {0} {1}", icon, result.Message);
                        }
                        else
                        {
                            "chocolatey".Log().Info(
                                "{0}|{1}|{2}",
                                statusGroup.Key.ToString().ToUpperInvariant(),
                                group.Key,
                                result.Message);
                        }
                    }
                }
            }
        }

        private static string GetHeading(DiagnosticStatus status, string groupName)
        {
            return $"{status.ToString().ToUpperInvariant()}: {groupName}";
        }

        private static int GetSeverityOrder(DiagnosticStatus status)
        {
            switch (status)
            {
                case DiagnosticStatus.Error:
                    return 1;

                case DiagnosticStatus.Warning:
                    return 2;

                case DiagnosticStatus.Suggestion:
                    return 3;

                case DiagnosticStatus.Success:
                    return 4;

                default:
                    return 99;
            }
        }

        private static string GetStatusIcon(DiagnosticStatus status)
        {
            switch (status)
            {
                case DiagnosticStatus.Error:
                    return "❌";

                case DiagnosticStatus.Warning:
                    return "⚠️";

                case DiagnosticStatus.Suggestion:
                    return "💡";

                case DiagnosticStatus.Success:
                    return "✅";

                default:
                    return "•";
            }
        }

        private static void LogStatus(DiagnosticStatus status, string message, params object[] formatting)
        {
            switch (status)
            {
                case DiagnosticStatus.Error:
                    "chocolatey".Log().Error(message, formatting);
                    break;

                case DiagnosticStatus.Warning:
                    "chocolatey".Log().Warn(message, formatting);
                    break;

                case DiagnosticStatus.Suggestion:
                case DiagnosticStatus.Success:
                    "chocolatey".Log().Info(message, formatting);
                    break;
            }
        }

        private static void LogHeader(ChocolateyConfiguration configuration, string message, DiagnosticStatus status = DiagnosticStatus.Success)
        {
            var loggerType = ChocolateyLoggers.Important;

            if (!configuration.RegularOutput)
            {
                loggerType = ChocolateyLoggers.LogFileOnly;
            }

            "chocolatey".Log().Info(loggerType, "");

            switch (status)
            {
                case DiagnosticStatus.Error:
                    "chocolatey".Log().Error(loggerType, message);
                    break;

                case DiagnosticStatus.Warning:
                case DiagnosticStatus.Suggestion:
                    "chocolatey".Log().Warn(loggerType, message);
                    break;

                case DiagnosticStatus.Success:
                    "chocolatey".Log().Info(loggerType, message);
                    break;
            }
        }
    }
}
