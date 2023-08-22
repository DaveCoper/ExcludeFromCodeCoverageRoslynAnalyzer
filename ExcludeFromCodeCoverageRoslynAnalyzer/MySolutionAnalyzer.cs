using System.Text;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExcludeFromCodeCoverageRoslynAnalyzer
{
    public class MySolutionAnalyzer : BackgroundService
    {
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly ILogger<MySolutionAnalyzer> logger;

        public MySolutionAnalyzer(IHostApplicationLifetime applicationLifetime, ILogger<MySolutionAnalyzer> logger)
        {
            this.applicationLifetime = applicationLifetime;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cmdArgs = Environment.GetCommandLineArgs();

            try
            {
                await Analyze(cmdArgs[1], stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Something broke");
            }
        }

        private async Task Analyze(string slnPath, CancellationToken stoppingToken)
        {
            StringBuilder warnings = new StringBuilder();

            logger.LogInformation("Looking for msbuild.");

            MSBuildLocator.RegisterDefaults();
            using (var workspace = MSBuildWorkspace.Create())
            {
                workspace.WorkspaceFailed += OnWorkspaceFailed;
                workspace.WorkspaceChanged += OnWorkspaceChanged;

                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation($"Loading solution {slnPath}.");

                var currSolution = await workspace.OpenSolutionAsync(slnPath);
                bool projectHasChanges = false;

                foreach (var currProject in currSolution.Projects)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    if (!currProject.Name.Contains("Quality", StringComparison.Ordinal))
                    {
                        logger.LogInformation($"Skipping project {currProject.Name}.");
                        continue;
                    }

                    logger.LogInformation($"Processing project {currProject.Name}.");
                    foreach (var document in currProject.Documents)
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        var tree = await document.GetSyntaxTreeAsync();
                        if (tree == null)
                        {
                            logger.LogError("Failed to open syntax tree for document {0}", document.Name);
                            continue;
                        }

                        var root = tree.GetRoot();
                        var rewriter = new IgnoreTestsSyntaxRewriter();
                        var newRoot = rewriter.Visit(root);
                        var documentHasChanges = root != newRoot;
                        projectHasChanges |= documentHasChanges;

                        if (documentHasChanges)
                        {
                            var newDocument = document.WithSyntaxRoot(Formatter.Format(newRoot, workspace));
                            currSolution = currSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
                        }
                    }

                    if (projectHasChanges)
                    {
                        logger.LogInformation("Applying changes to sollution {0}", currSolution.Id.Id);
                        if (workspace.TryApplyChanges(currSolution))
                        {
                            logger.LogError("Failed to apply changes to solution {0}", currSolution.Id.Id);
                        }
                    }
                    else
                    {
                        logger.LogInformation("Sollution {0} has no changes", currSolution.Id.Id);
                    }
                }
            }

            applicationLifetime.StopApplication();
        }

        private void OnWorkspaceChanged(object? sender, WorkspaceChangeEventArgs e)
        {
            logger.LogInformation(e.ToString());
        }

        private void OnWorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e)
        {
            LogDiagnostic(e);
        }

        private void LogDiagnostic(WorkspaceDiagnosticEventArgs e)
        {
            switch (e.Diagnostic.Kind)
            {
                case WorkspaceDiagnosticKind.Warning:
                    logger.LogWarning(e.Diagnostic.Message);
                    break;

                case WorkspaceDiagnosticKind.Failure:
                    logger.LogError(e.Diagnostic.Message);
                    break;
            }
        }
    }
}