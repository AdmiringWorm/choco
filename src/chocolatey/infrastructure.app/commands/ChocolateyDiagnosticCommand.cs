using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using chocolatey.infrastructure.app.attributes;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.app.diagnostics;
using chocolatey.infrastructure.commandline;
using chocolatey.infrastructure.commands;
using chocolatey.infrastructure.diagnostics;

namespace chocolatey.infrastructure.app.commands
{
    [CommandFor("diagnostic", "Runs diagnostic", Version = "2.5.0")]
    [CommandFor("diag", "Runs diagnostic (alias)", Version = "2.5.0")]
    public class ChocolateyDiagnosticCommand : ChocolateyCommandBase, ICommand
    {
        private readonly IEnumerable<IDiagnosticProvider> _providers;

        public ChocolateyDiagnosticCommand(IEnumerable<IDiagnosticProvider> providers)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        public void ConfigureArgumentParser(OptionSet optionSet, ChocolateyConfiguration configuration)
        {
        }

        public void ParseAdditionalArguments(IList<string> unparsedArguments, ChocolateyConfiguration configuration)
        {
            if (unparsedArguments.Count > 0)
            {
                configuration.DiagnosticCommand.ProviderName = unparsedArguments[0];
                unparsedArguments.RemoveAt(0);
            }
        }

        protected override IEnumerable<string> GetCommandExamples(CommandForAttribute[] attributes, ChocolateyConfiguration configuration)
        {
            yield return "choco diag";

            foreach (var provider in _providers)
            {
                yield return "choco diag " + provider.Name.ToLowerInvariant();
            }
        }

        protected override string GetCommandExampleDescription(ChocolateyConfiguration configuration)
        {
            var sb = new StringBuilder(base.GetCommandExampleDescription(configuration));

            sb.AppendLine().AppendLine("Available Providers:");

            foreach (var provider in _providers)
            {
                sb.AppendFormat("- {0}: {1}", provider.Name, provider.Description).AppendLine();
            }

            return sb.ToString();
        }

        public void Validate(ChocolateyConfiguration configuration)
        {
            if (!string.IsNullOrEmpty(configuration.DiagnosticCommand.ProviderName)
                && !_providers.Any(p => configuration.DiagnosticCommand.ProviderName.IsEqualTo(p.Name)))
            {
                throw new ApplicationException("The diagnostic provider '{0}' does not exist. Run choco diagnostic --help to see available providers.".FormatWith(configuration.DiagnosticCommand.ProviderName));
            }
        }

        public void DryRun(ChocolateyConfiguration configuration)
        {
            this.Log().Info("[noop] Would perform diagnostics using the providers:");

            if (string.IsNullOrEmpty(configuration.DiagnosticCommand.ProviderName))
            {
                this.Log().Info(" - {0}", configuration.DiagnosticCommand.ProviderName);
            }
            else
            {
                foreach (var provider in _providers)
                {
                    this.Log().Info(" - {0}", provider.Name.ToLowerInvariant());
                }
            }
        }

        public void Run(ChocolateyConfiguration config)
        {
            var results = new List<DiagnosticResult>();

            if (!string.IsNullOrWhiteSpace(config.DiagnosticCommand.ProviderName))
            {
                var provider = _providers.First(p => p.Name == config.DiagnosticCommand.ProviderName);
                results.AddRange(provider.Run(config));
            }
            else
            {
                foreach (var provider in _providers)
                {
                    results.AddRange(provider.Run(config));
                }
            }

            DiagnosticResultFormatter.Print(results, config);
        }

        public bool MayRequireAdminAccess()
        {
            return false;
        }

#pragma warning disable IDE0022, IDE1006
        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void configure_argument_parser(OptionSet optionSet, ChocolateyConfiguration configuration)
        {
            ConfigureArgumentParser(optionSet, configuration);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void handle_additional_argument_parsing(IList<string> unparsedArguments, ChocolateyConfiguration configuration)
        {
            ParseAdditionalArguments(unparsedArguments, configuration);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void handle_validation(ChocolateyConfiguration configuration)
        {
            Validate(configuration);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void help_message(ChocolateyConfiguration configuration)
        {
            HelpMessage(configuration);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void noop(ChocolateyConfiguration configuration)
        {
            DryRun(configuration);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public void run(ChocolateyConfiguration config)
        {
            Run(config);
        }

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public bool may_require_admin_access()
        {
            return MayRequireAdminAccess();
        }
#pragma warning restore IDE0022, IDE1006
    }
}
