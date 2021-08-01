﻿namespace Kari.Generator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using ConsoleAppFramework;
    using Microsoft.Build.Locator;
    using Microsoft.Build.Logging;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.Extensions.Hosting;

    public class KariCompiler : ConsoleAppBase
    {
        private static async Task Main(string[] args)
        {
            var instance = MSBuildLocator.RegisterDefaults();
            AssemblyLoadContext.Default.Resolving += (assemblyLoadContext, assemblyName) =>
            {
                var path = Path.Combine(instance.MSBuildPath, assemblyName.Name + ".dll");
                Console.WriteLine(path);
                if (File.Exists(path))
                {
                    return assemblyLoadContext.LoadFromAssemblyPath(path);
                }

                return null;
            };

            await Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => logging.ReplaceToSimpleConsole())
                .RunConsoleAppFrameworkAsync<KariCompiler>(args);
        }

        public async Task RunAsync(
            [Option("Input path to MSBuild project file or to the directory containing source files.")] 
            string input,
            [Option("The suffix added to each subproject (or the root project) indicating the output folder.")] 
            string generatedName = "Generated",
            [Option("Conditional compiler symbols, split with ','. Ignored if a project file is specified for input.")] 
            string? conditionalSymbol = null,
            [Option("Set input namespace root name.")]
            string rootNamespace = "",
            [Option("Whether the attributes should be written to output.")]
            bool writeAttributes = true,
            [Option("Delete all cs files in the output folder.")]
            bool clearOutputFolder = false,
            [Option("Plugins folder or full paths to individual plugin dlls separated by ','.")]
            string? pluginsLocations = null,
            [Option("Plugin names to be used for code analysis and generation separated by ','. All plugins are used by default.")]
            string? pluginNames = null)
        {
            if (generatedName == "" && clearOutputFolder)
            {
                
            }

            Workspace? workspace = null;
            try
            {
                Compilation compilation;
                if (Directory.Exists(input))
                {
                    string[]? conditionalSymbols = conditionalSymbol?.Split(',');
                    compilation = await PseudoCompilation.CreateFromDirectoryAsync(input, generatedName, conditionalSymbols, this.Context.CancellationToken);
                }
                else
                {
                    (workspace, compilation) = await this.OpenMSBuildProjectAsync(input, this.Context.CancellationToken);

                    if (rootNamespace == "" && compilation.AssemblyName != null)
                    {
                        rootNamespace = compilation.AssemblyName;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await Console.Error.WriteLineAsync("Canceled");
                throw;
            }
            finally
            {
                workspace?.Dispose();
            }
        }

        private static string NormalizeDirectorySeparators(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

        private async Task<(Workspace, Compilation)> OpenMSBuildProjectAsync(string projectPath, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            try
            {
                var logger = new ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity.Quiet);
                var project = await workspace.OpenProjectAsync(projectPath, logger, null, cancellationToken);
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation is null)
                {
                    throw new NotSupportedException("The project does not support creating Compilation.");
                }

                return (workspace, compilation);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }
    }
}
