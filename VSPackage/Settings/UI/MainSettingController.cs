// OpenCppCoverage is an open source code coverage for C++.
// Copyright (C) 2016 OpenCppCoverage
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

using GalaSoft.MvvmLight.Command;
using OpenCppCoverage.VSPackage.Helper;
using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenCppCoverage.VSPackage.Settings.UI
{
    //-------------------------------------------------------------------------
    class MainSettingController : PropertyChangedNotifier
    {
        readonly IOpenCppCoverageCmdLine openCppCoverageCmdLine;
        readonly ISettingsStorage settingsStorage;
        readonly Action<MainSettings> runCoverageAction;
        readonly IStartUpProjectSettingsBuilder startUpProjectSettingsBuilder;

        string selectedProjectPath;
        string solutionConfigurationName;
        bool displayProgramOutput;
        ProjectSelectionKind kind;

        //---------------------------------------------------------------------
        public MainSettingController(
            ISettingsStorage settingsStorage,
            IOpenCppCoverageCmdLine openCppCoverageCmdLine,
            IStartUpProjectSettingsBuilder startUpProjectSettingsBuilder,
            Action<MainSettings> runCoverageAction)
        {
            this.settingsStorage = settingsStorage;
            this.openCppCoverageCmdLine = openCppCoverageCmdLine;
            this.RunCoverageCommand = new RelayCommand(() => OnRunCoverageCommand());
            this.CloseCommand = new RelayCommand(() =>
            {
                this.CloseWindowEvent?.Invoke(this, EventArgs.Empty);
            });
            this.ResetToDefaultCommand = new RelayCommand(
                () => UpdateStartUpProject(ComputeStartUpProjectSettings(kind)));
            this.BasicSettingController = new BasicSettingController();
            this.FilterSettingController = new FilterSettingController();
            this.ImportExportSettingController = new ImportExportSettingController();
            this.MiscellaneousSettingController = new MiscellaneousSettingController();

            this.runCoverageAction = runCoverageAction;
            this.startUpProjectSettingsBuilder = startUpProjectSettingsBuilder;
        }

        //---------------------------------------------------------------------
        public void UpdateFields(ProjectSelectionKind kind, bool displayProgramOutput)
        {
            var settings = ComputeStartUpProjectSettings(kind);
            this.UpdateStartUpProject(settings);
            this.selectedProjectPath = settings.ProjectPath;
            this.displayProgramOutput = displayProgramOutput;
            this.solutionConfigurationName = settings.SolutionConfigurationName;
            this.kind = kind;

            UserInterfaceSettings uiSettings = null;
            try
            {
                uiSettings = this.settingsStorage.TryLoad(this.selectedProjectPath, this.solutionConfigurationName);
            }
            catch
            {
            }

            if (uiSettings != null)
            {
                this.BasicSettingController.UpdateSettings(uiSettings.BasicSettingController);
                this.FilterSettingController.UpdateSettings(uiSettings.FilterSettingController);
                this.ImportExportSettingController.UpdateSettings(uiSettings.ImportExportSettingController);
                this.MiscellaneousSettingController.UpdateSettings(uiSettings.MiscellaneousSettingController);
            }
        }

        //---------------------------------------------------------------------
        StartUpProjectSettings ComputeStartUpProjectSettings(ProjectSelectionKind kind)
        {
            return this.startUpProjectSettingsBuilder.ComputeSettings(kind);
        }

        //---------------------------------------------------------------------
        void UpdateStartUpProject(StartUpProjectSettings settings)
        {
            this.BasicSettingController.UpdateStartUpProject(settings);
            this.FilterSettingController.UpdateStartUpProject();
            this.ImportExportSettingController.UpdateStartUpProject();
            this.MiscellaneousSettingController.UpdateStartUpProject();
        }

        //---------------------------------------------------------------------
        public void SaveSettings()
        {
            var uiSettings = new UserInterfaceSettings
            {
                BasicSettingController = this.BasicSettingController.BuildJsonSettings(),
                FilterSettingController = this.FilterSettingController.Settings,
                ImportExportSettingController = this.ImportExportSettingController.Settings,
                MiscellaneousSettingController = this.MiscellaneousSettingController.Settings
            };
            this.settingsStorage.Save(this.selectedProjectPath, this.solutionConfigurationName, uiSettings);
        }

        //---------------------------------------------------------------------
        public MainSettings GetMainSettings()
        {
            var miscellaneousSettings = this.MiscellaneousSettingController.GetSettings();
            var importExportSettings = this.ImportExportSettingController.GetSettings();

            if (miscellaneousSettings.EnableDiagnosticBinaryExport)
                EnsureDiagnosticBinaryExport(importExportSettings);

            return new MainSettings
            {
                BasicSettings = this.BasicSettingController.GetSettings(),
                FilterSettings = this.FilterSettingController.GetSettings(),
                ImportExportSettings = importExportSettings,
                MiscellaneousSettings = miscellaneousSettings,
                DisplayProgramOutput = this.displayProgramOutput
            };
        }

        //---------------------------------------------------------------------
        public BasicSettingController BasicSettingController { get; }
        public FilterSettingController FilterSettingController { get; }
        public ImportExportSettingController ImportExportSettingController { get; }
        public MiscellaneousSettingController MiscellaneousSettingController { get; }

        //---------------------------------------------------------------------
        string commandLineText;
        public string CommandLineText
        {
            get { return this.commandLineText; }
            private set { this.SetField(ref this.commandLineText, value); }
        }

        //---------------------------------------------------------------------
        public static string CommandLineHeader = "Command line";

        public TabItem SelectedTab
        {
            set
            {
                if (value != null && (string)value.Header == CommandLineHeader)
                {
                    try
                    {
                        this.CommandLineText = this.openCppCoverageCmdLine.Build(this.GetMainSettings(), "\n");
                    } 
                    catch (Exception e)
                    {
                        this.CommandLineText = e.Message;
                    }
                }
            }
        }
        //---------------------------------------------------------------------
        void OnRunCoverageCommand()
        {
            if (this.runCoverageAction == null)
                throw new VSPackageException("Coverage runner is not available.");

            this.runCoverageAction(this.GetMainSettings());
        }

        //---------------------------------------------------------------------
        public EventHandler CloseWindowEvent;

        //---------------------------------------------------------------------
        public ICommand CloseCommand { get; }
        public ICommand RunCoverageCommand { get; }
        public ICommand ResetToDefaultCommand { get; }

        //---------------------------------------------------------------------
        void EnsureDiagnosticBinaryExport(ImportExportSettings settings)
        {
            if (settings == null)
                return;

            if (settings.Exports != null && settings.Exports.Any(export =>
                export != null
                && export.Type == ImportExportSettings.Type.Binary
                && !string.IsNullOrWhiteSpace(export.Path)))
            {
                return;
            }

            var defaultPath = BuildDefaultBinaryExportPath(this.selectedProjectPath);
            if (string.IsNullOrWhiteSpace(defaultPath))
                return;

            var exports = (settings.Exports ?? Enumerable.Empty<ImportExportSettings.Export>()).ToList();
            exports.Add(new ImportExportSettings.Export
            {
                Type = ImportExportSettings.Type.Binary,
                Path = defaultPath
            });
            settings.Exports = exports;
        }

        //---------------------------------------------------------------------
        static string BuildDefaultBinaryExportPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return null;

            try
            {
                var projectDirectory = Path.GetDirectoryName(projectPath);
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                if (string.IsNullOrWhiteSpace(projectDirectory) || string.IsNullOrWhiteSpace(projectName))
                    return null;

                return Path.Combine(projectDirectory, $"OpenCppCoverage-{projectName}.cov");
            }
            catch
            {
                return null;
            }
        }
    }
}
