// OpenCppCoverage is an open source code coverage for C++.
// Copyright (C) 2014 OpenCppCoverage
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using EnvDTE;
using EnvDTE80;
using Microsoft.Build.Evaluation;
using Microsoft.CSharp.RuntimeBinder;
using OpenCppCoverage.VSPackage.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenCppCoverage.VSPackage.Settings
{
    class StartUpProjectSettingsBuilder: IStartUpProjectSettingsBuilder
    {
        //---------------------------------------------------------------------
        public StartUpProjectSettingsBuilder(
            DTE2 dte,
            IConfigurationManager configurationManager)
        {
            this.dte = dte;
            this.configurationManager = configurationManager;
        }

        //---------------------------------------------------------------------
        public StartUpProjectSettings ComputeSettings(ProjectSelectionKind kind)
        {
            try
            {
                var solution = dte?.Solution as Solution2;
                var solutionBuild = solution?.SolutionBuild as SolutionBuild2;
                var activeConfiguration = solutionBuild?.ActiveConfiguration as SolutionConfiguration2;

                if (activeConfiguration != null)
                {
                    var settings = ComputeOptionalSettings(activeConfiguration, kind);

                    if (settings != null)
                        return settings;
                }
            }
            catch (COMException)
            {
            }
            catch (RuntimeBinderException)
            {
            }
            catch (InvalidCastException)
            {
            }

            return CreateEmptySettings();
        }

        //---------------------------------------------------------------------
        StartUpProjectSettings ComputeOptionalSettings(
            SolutionConfiguration2 activeConfiguration,
            ProjectSelectionKind kind)
        {
            var solution = dte?.Solution as Solution2;
            if (solution == null)
                return null;

            var projects = GetProjects(solution);
            ExtendedProject project = null;

            switch (kind)
            {
                case ProjectSelectionKind.StartUpProject:
                    project = GetOptionalStartupProject(solution, projects);
                break;
                case ProjectSelectionKind.SelectedProject:
                    project = GetOptionalSelectedProject(projects);
                break;
            }

            if (project == null) 
                return null;

            return ComputeOptionalSettings(activeConfiguration, projects, project);
        }

        //---------------------------------------------------------------------
        StartUpProjectSettings ComputeOptionalSettings(
            SolutionConfiguration2 activeConfiguration,
            List<ExtendedProject> projects,
            ExtendedProject project)
        {
            var startupConfiguration = this.configurationManager.GetConfiguration(
                activeConfiguration, project);
            var debugSettings = startupConfiguration.DebugSettings;

            var rawWorkingDirectory = debugSettings.WorkingDirectory;
            var rawArguments = debugSettings.CommandArguments;
            var rawCommand = debugSettings.Command;
            var primaryOutput = startupConfiguration.PrimaryOutput;
            var targetPath = startupConfiguration.Evaluate("$(TargetPath)");
            var outDir = startupConfiguration.Evaluate("$(OutDir)");
            var targetName = startupConfiguration.Evaluate("$(TargetName)");
            var targetExt = startupConfiguration.Evaluate("$(TargetExt)");
            var targetDir = startupConfiguration.Evaluate("$(TargetDir)");
            var msbuildCommand = ResolveProgramToRunFromMsBuild(
                project.Path,
                activeConfiguration.Name,
                activeConfiguration.PlatformName);
            var debugSettingsCommand = startupConfiguration.Evaluate(rawCommand);
            var composedCommand = startupConfiguration.Evaluate("$(OutDir)$(TargetName)$(TargetExt)");

            var evaluatedCommand = FirstNonEmpty(
                debugSettingsCommand,
                primaryOutput,
                targetPath,
                composedCommand,
                msbuildCommand);

            var evaluatedWorkingDirectory = FirstNonEmpty(
                startupConfiguration.Evaluate(rawWorkingDirectory),
                targetDir,
                GetDirectoryNameSafe(evaluatedCommand),
                GetDirectoryNameSafe(project.Path));

            var evaluatedArguments = FirstNonEmpty(
                startupConfiguration.Evaluate(rawArguments),
                string.Empty);

            if (string.IsNullOrWhiteSpace(evaluatedCommand))
            {
                var diagnostics = new StringBuilder();
                diagnostics.AppendLine("Command resolution diagnostics");
                diagnostics.AppendLine($"ProjectName: {project.UniqueName ?? "<null>"}");
                diagnostics.AppendLine($"SolutionConfigurationName: {this.configurationManager.GetSolutionConfigurationName(activeConfiguration) ?? "<null>"}");
                diagnostics.AppendLine($"raw Command: {rawCommand ?? "<null>"}");
                diagnostics.AppendLine($"raw Arguments: {rawArguments ?? "<null>"}");
                diagnostics.AppendLine($"raw WorkingDirectory: {rawWorkingDirectory ?? "<null>"}");
                diagnostics.AppendLine($"PrimaryOutput: {primaryOutput ?? "<null>"}");
                diagnostics.AppendLine($"$(TargetPath): {targetPath ?? "<null>"}");
                diagnostics.AppendLine($"$(OutDir): {outDir ?? "<null>"}");
                diagnostics.AppendLine($"$(TargetName): {targetName ?? "<null>"}");
                diagnostics.AppendLine($"$(TargetExt): {targetExt ?? "<null>"}");
                diagnostics.AppendLine($"$(TargetDir): {targetDir ?? "<null>"}");
                throw new VSPackageException(diagnostics.ToString());
            }

            return new StartUpProjectSettings
            {
                WorkingDir = evaluatedWorkingDirectory,
                Arguments = evaluatedArguments,
                Command = evaluatedCommand,
                SolutionConfigurationName = this.configurationManager.GetSolutionConfigurationName(activeConfiguration),
                ProjectName = project.UniqueName,
                ProjectPath = project.Path,
                CppProjects = BuildStableCppProjects(project, evaluatedCommand, primaryOutput),
                IsOptimizedBuildEnabled = false,
                EnvironmentVariables = new List<KeyValuePair<string, string>>()
            };
        }

        //---------------------------------------------------------------------
        static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        //---------------------------------------------------------------------
        static string GetDirectoryNameSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetDirectoryName(path);
            }
            catch
            {
                return null;
            }
        }

        //---------------------------------------------------------------------
        string ResolveProgramToRunFromMsBuild(
            string projectPath,
            string configurationName,
            string platformName)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return null;

            ProjectCollection projectCollection = null;
            Microsoft.Build.Evaluation.Project msbuildProject = null;

            try
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Configuration", string.IsNullOrWhiteSpace(configurationName) ? "Debug" : configurationName },
                    { "Platform", string.IsNullOrWhiteSpace(platformName) ? "x64" : platformName },
                    { "VisualStudioVersion", GetVisualStudioVersion() }
                };

                var solutionDirectory = GetSolutionDirectory();
                if (!string.IsNullOrWhiteSpace(solutionDirectory))
                    globalProperties["SolutionDir"] = EnsureTrailingSlash(solutionDirectory);

                projectCollection = new ProjectCollection(globalProperties);
                msbuildProject = projectCollection.LoadProject(projectPath);

                var targetPath = msbuildProject.GetPropertyValue("TargetPath");
                if (!string.IsNullOrWhiteSpace(targetPath))
                    return NormalizeEvaluatedPath(projectPath, targetPath);

                var outDir = msbuildProject.GetPropertyValue("OutDir");
                var targetName = msbuildProject.GetPropertyValue("TargetName");
                var targetExt = msbuildProject.GetPropertyValue("TargetExt");
                if (!string.IsNullOrWhiteSpace(outDir) && !string.IsNullOrWhiteSpace(targetName))
                    return NormalizeEvaluatedPath(projectPath, Path.Combine(outDir, targetName + targetExt));
            }
            catch
            {
            }
            finally
            {
                if (msbuildProject != null)
                    projectCollection?.UnloadProject(msbuildProject);
                projectCollection?.Dispose();
            }

            return null;
        }

        //---------------------------------------------------------------------
        string GetVisualStudioVersion()
        {
            try
            {
                var version = dte?.Version;
                return string.IsNullOrWhiteSpace(version) ? "18.0" : version;
            }
            catch
            {
                return "18.0";
            }
        }

        //---------------------------------------------------------------------
        string GetSolutionDirectory()
        {
            try
            {
                var solutionPath = dte?.Solution?.FullName;
                if (string.IsNullOrWhiteSpace(solutionPath))
                    return null;

                return Path.GetDirectoryName(solutionPath);
            }
            catch
            {
                return null;
            }
        }

        //---------------------------------------------------------------------
        static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
                return path;

            return path + "\\";
        }

        //---------------------------------------------------------------------
        static string NormalizeEvaluatedPath(string projectPath, string evaluatedPath)
        {
            if (string.IsNullOrWhiteSpace(evaluatedPath))
                return null;

            try
            {
                if (Path.IsPathRooted(evaluatedPath))
                    return Path.GetFullPath(evaluatedPath);

                var projectDirectory = Path.GetDirectoryName(projectPath);
                if (string.IsNullOrWhiteSpace(projectDirectory))
                    return null;

                return Path.GetFullPath(Path.Combine(projectDirectory, evaluatedPath));
            }
            catch
            {
                return null;
            }
        }

        //---------------------------------------------------------------------
        static IEnumerable<StartUpProjectSettings.CppProject> BuildStableCppProjects(
            ExtendedProject project,
            string evaluatedCommand,
            string primaryOutput)
        {
            if (project == null)
                return new List<StartUpProjectSettings.CppProject>();

            var modulePath = FirstNonEmpty(primaryOutput, evaluatedCommand);
            var sourcePaths = BuildStableSourcePaths(project.Path).ToList();

            if (string.IsNullOrWhiteSpace(modulePath) || !sourcePaths.Any())
                return new List<StartUpProjectSettings.CppProject>();

            return new[]
            {
                new StartUpProjectSettings.CppProject
                {
                    ModulePath = modulePath,
                    SourcePaths = sourcePaths,
                    Path = project.UniqueName
                }
            };
        }

        //---------------------------------------------------------------------
        static IEnumerable<string> BuildStableSourcePaths(string projectPath)
        {
            var sourcePaths = new List<string>();

            if (string.IsNullOrWhiteSpace(projectPath))
                return sourcePaths;

            try
            {
                var projectDirectory = Path.GetDirectoryName(projectPath);
                if (!string.IsNullOrWhiteSpace(projectDirectory) && Directory.Exists(projectDirectory))
                    sourcePaths.Add(projectDirectory);
            }
            catch
            {
            }

            return sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        static IEnumerable<KeyValuePair<string, string>> 
            GetEnvironmentVariables(DynamicVCConfiguration configuration)
        {
            var environmentVariables = new List<KeyValuePair<string, string>>();
            string environmentStr = configuration.Evaluate("$(LocalDebuggerEnvironment)");

            if (string.IsNullOrEmpty(environmentStr))
                return environmentVariables;

            foreach (var str in environmentStr.Split('\n'))
            {
                var equalIndex = str.IndexOf('=');
                if (equalIndex != -1 && equalIndex != str.Length - 1)
                {
                    var key = str.Substring(0, equalIndex);
                    var value = str.Substring(equalIndex + 1);

                    environmentVariables.Add(new KeyValuePair<string, string>(key, value));
                }
            }
            return environmentVariables;
        }

        //---------------------------------------------------------------------
        List<ExtendedProject> GetProjects(Solution2 solution)
        {
            var projects = new List<ExtendedProject>();

            if (solution?.Projects == null)
                return projects;

            foreach (EnvDTE.Project project in solution.Projects)
            {
                if (project != null)
                    projects.AddRange(CreateExtendedProjectsFor(project));
            }

            return projects;
        }

        //---------------------------------------------------------------------
        List<ExtendedProject> CreateExtendedProjectsFor(EnvDTE.Project project)
        {
            var projects = new List<ExtendedProject>();

            if (project == null)
                return projects;

            try
            {
                if (project.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
                {
                    if (project.ProjectItems == null)
                        return projects;

                    foreach (EnvDTE.ProjectItem projectItem in project.ProjectItems)
                    {
                        var subProject = projectItem?.SubProject;
                        if (subProject != null)
                            projects.AddRange(CreateExtendedProjectsFor(subProject));
                    }
                }
                else
                {
                    dynamic projectObject = project.Object;

                    try
                    {
                        if (projectObject != null && projectObject.Kind == "VCProject")
                            projects.Add(new ExtendedProject(project, new DynamicVCProject(projectObject)));
                    }
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }
            catch (COMException)
            {
            }
            catch (RuntimeBinderException)
            {
            }
            catch (InvalidCastException)
            {
            }

            return projects;
        }

        //---------------------------------------------------------------------
        ExtendedProject GetOptionalStartupProject(
            Solution2 solution,
            List<ExtendedProject> projects)
        {
            object[] startupProjectsNames = null;
            try
            {
                startupProjectsNames = solution?.SolutionBuild?.StartupProjects as object[];
            }
            catch (COMException)
            {
            }

            if (startupProjectsNames == null)
                return null;

            var startupProjectsSet = new HashSet<String>();
            foreach (String projectName in startupProjectsNames)
                startupProjectsSet.Add(projectName);

            return projects.Where(p => startupProjectsSet.Contains(p.UniqueName)).FirstOrDefault();
        }

        //---------------------------------------------------------------------
        ExtendedProject GetOptionalSelectedProject(List<ExtendedProject> projects)
        {
            Array activeSolutionProjects = null;
            try
            {
                activeSolutionProjects = this.dte?.ActiveSolutionProjects as Array;
            }
            catch (COMException)
            {
            }

            if (activeSolutionProjects == null)
                return null;

            var selectedProjects = activeSolutionProjects.Cast<EnvDTE.Project>().Where(project => project != null);
            
            if (selectedProjects.Count() != 1)
                return null;

            string projectName;
            try
            {
                projectName = selectedProjects.First().UniqueName;
            }
            catch (COMException)
            {
                return null;
            }

            return projects.Where(p => p.UniqueName == projectName).FirstOrDefault();
        }

        //---------------------------------------------------------------------
        static IEnumerable<StartUpProjectSettings.CppProject> BuildCppProject(
            SolutionConfiguration2 activeConfiguration,
            IConfigurationManager configurationManager,
            List<ExtendedProject> projects)
        {
            var cppProjects = new List<StartUpProjectSettings.CppProject>();

            foreach (var project in projects)
            {
                try
                {
                    var configuration = configurationManager.FindConfiguration(activeConfiguration, project);

                    if (configuration != null)
                    {
                        var sourcePaths = PathHelper.ComputeCommonFolders(project.Files
                            .Select(f => f.FullPath)
                            .Where(path => !string.IsNullOrWhiteSpace(path)));

                        var cppProject = new StartUpProjectSettings.CppProject()
                        {
                            ModulePath = configuration.PrimaryOutput,
                            SourcePaths = sourcePaths,
                            Path = project.UniqueName
                        };
                        cppProjects.Add(cppProject);
                    }
                }
                catch
                {
                }
            }

            return cppProjects;
        }

        readonly DTE2 dte;
        readonly IConfigurationManager configurationManager;

        //---------------------------------------------------------------------
        static StartUpProjectSettings CreateEmptySettings()
        {
            return new StartUpProjectSettings
            {
                CppProjects = new List<StartUpProjectSettings.CppProject>(),
                EnvironmentVariables = new List<KeyValuePair<string, string>>()
            };
        }
    }
}
