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

using OpenCppCoverage.VSPackage.Settings;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OpenCppCoverage.VSPackage
{
    class OpenCppCoverageRunner
    {
        readonly OutputWindowWriter outputWindowWriter;
        readonly OpenCppCoverageCmdLine openCppCoverageCmdLine;

        //---------------------------------------------------------------------
        public OpenCppCoverageRunner(
            OutputWindowWriter outputWindowWriter, 
            OpenCppCoverageCmdLine openCppCoverageCmdLine)
        {
            this.outputWindowWriter = outputWindowWriter;
            this.openCppCoverageCmdLine = openCppCoverageCmdLine;
        }

        //---------------------------------------------------------------------
        public Task RunCodeCoverageAsync(MainSettings settings)
        {
            var basicSettings = settings.BasicSettings;
            var fileName = GetOpenCppCoveragePath(basicSettings.ProgramToRun);
            var arguments = this.openCppCoverageCmdLine.Build(settings);
            var logPath = settings.MiscellaneousSettings != null
                && settings.MiscellaneousSettings.EnableDiagnosticProcessLog
                    ? BuildLogPath(basicSettings.ProgramToRun)
                    : null;

            this.outputWindowWriter.WriteLine("Run:");
            this.outputWindowWriter.WriteLine(string.Format(@"""{0}"" {1}",
                fileName, arguments));
            WriteBasicSettings(basicSettings);
            if (!string.IsNullOrWhiteSpace(logPath))
                this.outputWindowWriter.WriteLine("Log written to " + logPath);

            // Run in a new thread to not block UI thread.
            return Task.Run(() =>
            {
                StreamWriter logWriter = null;
                using (var process = new Process())
                {
                    var startInfo = process.StartInfo;
                    startInfo.FileName = fileName;
                    startInfo.Arguments = arguments;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = !settings.DisplayProgramOutput;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.StandardOutputEncoding = Encoding.UTF8;
                    startInfo.StandardErrorEncoding = Encoding.UTF8;

                    var environmentVariables = startInfo.EnvironmentVariables;
                    foreach (var environment in basicSettings.EnvironmentVariables)
                        environmentVariables[environment.Key] = environment.Value;

                    if (!String.IsNullOrEmpty(basicSettings.WorkingDirectory))
                        startInfo.WorkingDirectory = basicSettings.WorkingDirectory;

                    if (!string.IsNullOrWhiteSpace(logPath))
                    {
                        logWriter = TryCreateLogWriter(logPath);
                        if (logWriter != null)
                        {
                            logWriter.WriteLine(DateTime.Now.ToString("O"));
                            logWriter.WriteLine("Command:");
                            logWriter.WriteLine(string.Format(@"""{0}"" {1}", fileName, arguments));
                            logWriter.WriteLine();
                            logWriter.Flush();
                        }
                    }

                    process.Start();

                    process.OutputDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null)
                            return;

                        this.outputWindowWriter.WriteLine(eventArgs.Data);
                        logWriter?.WriteLine(eventArgs.Data);
                        logWriter?.Flush();
                    };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (eventArgs.Data == null)
                            return;

                        this.outputWindowWriter.WriteLine(eventArgs.Data);
                        logWriter?.WriteLine(eventArgs.Data);
                        logWriter?.Flush();
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    logWriter?.WriteLine($"ExitCode: {process.ExitCode}");
                    logWriter?.Flush();
                }

                logWriter?.Dispose();
            });
        }

        //---------------------------------------------------------------------
        void WriteBasicSettings(BasicSettings settings)
        {
            this.outputWindowWriter.WriteLine("Program to run: " + settings.ProgramToRun);
            this.outputWindowWriter.WriteLine("Working directory: " + settings.WorkingDirectory);
            WriteValues("Sources", settings.SourcePaths);
            WriteValues("Modules", settings.ModulePaths);
        }

        //---------------------------------------------------------------------
        void WriteValues(string label, System.Collections.Generic.IEnumerable<string> values)
        {
            this.outputWindowWriter.WriteLine(label + ":");
            var hasValue = false;
            if (values != null)
            {
                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    hasValue = true;
                    this.outputWindowWriter.WriteLine("  " + value);
                }
            }

            if (!hasValue)
                this.outputWindowWriter.WriteLine("  <none>");
        }

        //---------------------------------------------------------------------
        StreamWriter TryCreateLogWriter(string logPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                return new StreamWriter(
                    new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                this.outputWindowWriter.WriteLine(
                    "Cannot write diagnostic process log: " + exception.Message);
                return null;
            }
        }

        //---------------------------------------------------------------------
        string GetOpenCppCoveragePath(string commandPath)
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyFolder = Path.GetDirectoryName(assemblyLocation);
            var openCppCovergeFolder = Environment.Is64BitOperatingSystem ? 
                                            "OpenCppCoverage-x64" : "OpenCppCoverage-x86";
            return Path.Combine(assemblyFolder, openCppCovergeFolder, "OpenCppCoverage.exe");
        }

        //---------------------------------------------------------------------
        static string BuildLogPath(string programToRun)
        {
            if (string.IsNullOrWhiteSpace(programToRun))
                return null;

            try
            {
                var programDirectory = Path.GetDirectoryName(programToRun);
                var programName = Path.GetFileNameWithoutExtension(programToRun);
                if (string.IsNullOrWhiteSpace(programDirectory) || string.IsNullOrWhiteSpace(programName))
                    return null;

                return Path.Combine(
                    programDirectory,
                    $"OpenCppCoverage-{programName}-{Guid.NewGuid():N}.log");
            }
            catch
            {
                return null;
            }
        }
    }
}
