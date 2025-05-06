namespace chocolatey.infrastructure.diagnostics
{

    public sealed class DiagnosticResult
    {
        /// <summary>
        /// The severity of the result: Error, Warning, Suggestion, Success.
        /// </summary>
        public DiagnosticStatus Status { get; set; }

        /// <summary>
        /// The logical group this result belongs to (e.g., "Missing Dependencies").
        /// Used as the section header for output.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// The message to be displayed to the user.
        /// </summary>
        public string Message { get; set; }
    }
}

