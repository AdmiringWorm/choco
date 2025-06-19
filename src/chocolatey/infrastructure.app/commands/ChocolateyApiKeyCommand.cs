// Copyright © 2017 - 2025 Chocolatey Software, Inc
// Copyright © 2011 - 2017 RealDimensions Software, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
// You may obtain a copy of the License at
//
// 	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using chocolatey.infrastructure.app.attributes;
using chocolatey.infrastructure.app.domain;
using chocolatey.infrastructure.commandline;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.commands;
using chocolatey.infrastructure.configuration;
using chocolatey.infrastructure.app.services;

namespace chocolatey.infrastructure.app.commands
{
    [CommandFor("apikey", "Manages API keys for specific sources when pushing Chocolatey CLI packages.")]
    [CommandFor("setapikey", "Manages API keys for specific sources when pushing Chocolatey CLI packages. (alias for apikey)")]
    public class ChocolateyApiKeyCommand : ChocolateyCommandBase, ICommand
    {
        private readonly IChocolateyConfigSettingsService _configSettingsService;

        public ChocolateyApiKeyCommand(IChocolateyConfigSettingsService configSettingsService)
        {
            _configSettingsService = configSettingsService;
        }

        public virtual void ConfigureArgumentParser(OptionSet optionSet, ChocolateyConfiguration configuration)
        {
            configuration.Sources = null;

            optionSet
                .Add("s=|source=",
                     "[REQUIRED] – The package source the API key applies to.",
                     option => configuration.Sources = option.UnquoteSafe())
                .Add("k=|key=|apikey=|api-key=",
                     "The API key used to authenticate with the specified source. This key identifies you and allows you to push packages to that source. Depending on the source, the key may be a standard API token or a user credential formatted as `'username:password'`.",
                     option => configuration.ApiKeyCommand.Key = option.UnquoteSafe())
                ;
        }

        public virtual void ParseAdditionalArguments(IList<string> unparsedArguments, ChocolateyConfiguration configuration)
        {
            configuration.Input = string.Join(" ", unparsedArguments);

            if (unparsedArguments.Count > 1)
            {
                throw new ApplicationException("A single apikey command must be listed. Please see the help menu for those commands.");
            }

            var command = ApiKeyCommandType.Unknown;
            var unparsedCommand = unparsedArguments.DefaultIfEmpty(string.Empty).FirstOrDefault();

            if (!Enum.TryParse(unparsedCommand, true, out command) || command == ApiKeyCommandType.Unknown)
            {
                command = ApiKeyCommandType.List;

                if (!string.IsNullOrWhiteSpace(unparsedCommand))
                {
                    this.Log().Warn("Unknown command {0}. Setting to list.".FormatWith(unparsedCommand));
                }
                else if (!string.IsNullOrWhiteSpace(configuration.ApiKeyCommand.Key))
                {
                    this.Log().Warn("API key provided. Setting command to add.");
                    command = ApiKeyCommandType.Add;
                }
            }

            configuration.ApiKeyCommand.Command = command;
        }

        public virtual void Validate(ChocolateyConfiguration configuration)
        {
            switch (configuration.ApiKeyCommand.Command)
            {
                case ApiKeyCommandType.Remove:
                    if (string.IsNullOrWhiteSpace(configuration.Sources))
                    {
                        throw new ApplicationException("You must specify 'source' to remove an API key.");
                    }

                    break;
                case ApiKeyCommandType.Add:
                    if (string.IsNullOrWhiteSpace(configuration.Sources) || string.IsNullOrWhiteSpace(configuration.ApiKeyCommand.Key))
                    {
                        throw new ApplicationException("You must specify both 'source' and 'key' to set an API key.");
                    }

                    break;
            }
        }

        protected override string GetCommandName(CommandForAttribute attribute)
        {
            return "ApiKey";
        }

        protected override string GetCommandDescription(CommandForAttribute attribute, ChocolateyConfiguration configuration)
        {
            return @"**Lists existing API keys or sets an API key for a specific source.**

If no `--source` and `--api-key` are provided, the command displays all saved API  
keys.  
If both are provided, the API key is saved for that source so it doesn't need to  
be specified in future operations.";
        }

        protected override IEnumerable<string> GetCommandExamples(CommandForAttribute[] attributes, ChocolateyConfiguration configuration)
        {
            return new[]
            {
                "choco apikey",
                "choco apikey -s https://somewhere/out/there",
                "choco apikey list",
                "choco apikey list -s https://somewhere/out/there",
                "choco apikey add -s=\"https://somewhere/out/there/\" -k=\"value\"",
                "choco apikey add -s \"https://push.chocolatey.org/\" -k=\"123-123123-123\"",
                "choco apikey add -s \"http://internal_nexus\" -k=\"user:password\"",
                "choco apikey remove -s https://somewhere/out/there"
            };
        }

        protected override string GetCommandExampleDescription(ChocolateyConfiguration configuration)
        {
            return @"For the source location, this can be a folder, file share, or an HTTP(S) URL.
When using URLs, the push location may differ from the standard package source  
used for searching and installing packages. For example, the Chocolatey  
Community Repository uses `{0}` as the
package source URL, but the push URL is `{1}`
(`{2}` is now deprecated for pushing).

Refer to your repository's documentation to determine the correct push URL.

For the API key, this is typically a key provided by your source repository.
Some sources, like Nexus, may require a NuGet API key or a user credential
formatted as `'username:password'`. Please consult your repository’s
documentation.
For Nexus-specific guidance, see: <https://ch0.co/nexus2apikey>".FormatWith(
                ApplicationParameters.ChocolateyCommunityFeedSource,
                ApplicationParameters.ChocolateyCommunityFeedPushSource,
                ApplicationParameters.ChocolateyCommunityFeedPushSourceOld);
        }

        protected override IDictionary<string, string> GetAdditionalSections(
    CommandForAttribute attribute,
    ChocolateyConfiguration configuration)
        {
            var sections = new SortedDictionary<string, string>
            {
                {
                    "Connecting to Chocolatey.org (Community Package Repository)",
                    @"To save your API key for {0}, follow these steps:

1. Log in (or register, confirm your account, and then log in) to the Chocolatey
   Community Repository at `{1}`.
2. Navigate to `{1}account` and copy your API key.
3. Save the key using the following command:

    choco apikey add -k <your key here> -s {0}"
                .FormatWith(
                    ApplicationParameters.ChocolateyCommunityFeedPushSource,
                    ApplicationParameters.ChocolateyCommunityGalleryUrl)
                }
            };

            return sections;
        }


        protected override IEnumerable<ExitCodeDescription> GetEnhancedExitCodes(ChocolateyConfiguration configuration)
        {
            yield return new ExitCodeDescription("nothing to do, apikey already set", 2);
        }

        protected override string GetAdditionalExitCodeDescription()
        {
            return @"NOTE: Starting in v2.3.0, if the '{0}' feature is enabled,  
Chocolatey will return enhanced exit codes to support improved scripting and  
automation scenarios.
".FormatWith(ApplicationParameters.Features.UseEnhancedExitCodes);
        }

        public virtual void DryRun(ChocolateyConfiguration configuration)
        {
            _configSettingsService.DryRun(configuration);
        }

        public virtual void Run(ChocolateyConfiguration configuration)
        {
            switch (configuration.ApiKeyCommand.Command)
            {
                case ApiKeyCommandType.Remove:
                    _configSettingsService.RemoveApiKey(configuration);
                    break;
                case ApiKeyCommandType.Add:
                    _configSettingsService.SetApiKey(configuration);
                    break;
                default:
                    // This is required, since we only want to show the header if there are any ApiKeys
                    // to be shown, which will be when we are printing the first ApiKey
                    var hasHeaderRowBeenOutput = false;

                    _configSettingsService.GetApiKey(configuration, (key) =>
                    {
                        var authenticatedString = string.IsNullOrWhiteSpace(key.Key) ? string.Empty : "(Authenticated)";

                        if (configuration.RegularOutput)
                        {
                            this.Log().Info(() => "{0} - {1}".FormatWith(key.Source, authenticatedString));
                        }
                        else
                        {
                            if (configuration.IncludeHeaders && !hasHeaderRowBeenOutput)
                            {
                                OutputHelpers.LimitedOutput("Source", "ApiKey");
                                hasHeaderRowBeenOutput = true;
                            }

                            OutputHelpers.LimitedOutput(key.Source, authenticatedString);
                        }
                    });
                    break;
            }
        }

        public virtual bool MayRequireAdminAccess()
        {
            var config = Config.GetConfigurationSettings();
            if (config == null)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(config.ApiKeyCommand.Key);
        }

#pragma warning disable IDE0022, IDE1006
        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void configure_argument_parser(OptionSet optionSet, ChocolateyConfiguration configuration)
            => ConfigureArgumentParser(optionSet, configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void handle_additional_argument_parsing(IList<string> unparsedArguments, ChocolateyConfiguration configuration)
            => ParseAdditionalArguments(unparsedArguments, configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void handle_validation(ChocolateyConfiguration configuration)
            => Validate(configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void help_message(ChocolateyConfiguration configuration)
            => HelpMessage(configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void noop(ChocolateyConfiguration configuration)
            => DryRun(configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual void run(ChocolateyConfiguration configuration)
            => Run(configuration);

        [Obsolete("This overload is deprecated and will be removed in v3.")]
        public virtual bool may_require_admin_access()
            => MayRequireAdminAccess();
#pragma warning restore IDE0022, IDE1006
    }
}
