using System.Collections.Generic;
using chocolatey.infrastructure.app.attributes;
using chocolatey.infrastructure.app.configuration;

namespace chocolatey.infrastructure.diagnostics
{
    [MultiService]
    public interface IDiagnosticProvider
    {
        /// <summary>
        /// The identifier for this provider (e.g., "deps").
        /// Used as the subcommand for `choco diag`.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A short description used for help output.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Runs the diagnostic logic.
        /// </summary>
        /// <param name="configuration">The Chocolatey configuration object.</param>
        /// <returns>A list of diagnostic results.</returns>
        IEnumerable<DiagnosticResult> Run(ChocolateyConfiguration configuration);
    }
}
