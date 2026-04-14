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

using OpenCppCoverage.VSPackage.Helper;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace OpenCppCoverage.VSPackage.Settings.UI
{
    //-------------------------------------------------------------------------
    class FilterSettingController: PropertyChangedNotifier
    {
        static readonly string[] DefaultExcludedSourcePatterns =
        {
            "*Microsoft Visual Studio*",
            "*Windows Kits*",
            "*3rdparty*",
            "*external*",
            "*extern*",
            "*packages*",
            "*vcpkg_installed*",
            "*boost*",
            "*vctools*",
            "*MSVC*",
            "*VC\\Tools\\MSVC*"
        };

        static readonly string[] DefaultExcludedModulePatterns =
        {
            "*System32*",
            "*WinSxS*",
            "*vcruntime*",
            "*msvcp*",
            "*ucrtbase*"
        };

        //---------------------------------------------------------------------
        public class SettingsData
        {
            public SettingsData()
            {
                this.AdditionalSourcePatterns = new ObservableCollection<BindableString>();
                this.AdditionalModulePatterns = new ObservableCollection<BindableString>();
                this.ExcludedSourcePatterns = new ObservableCollection<BindableString>();
                this.ExcludedModulePatterns = new ObservableCollection<BindableString>();
                this.UnifiedDiffs = new ObservableCollection<FilterSettings.UnifiedDiff>();
            }

            public ObservableCollection<BindableString> AdditionalSourcePatterns { get; }
            public ObservableCollection<BindableString> AdditionalModulePatterns { get; }
            public ObservableCollection<BindableString> ExcludedSourcePatterns { get; }
            public ObservableCollection<BindableString> ExcludedModulePatterns { get; }
            public ObservableCollection<FilterSettings.UnifiedDiff> UnifiedDiffs { get; }
        }


        //---------------------------------------------------------------------
        public FilterSettingController()
        {
            this.Settings = new SettingsData();
        }

        //---------------------------------------------------------------------
        SettingsData settings;
        public SettingsData Settings
        {
            get { return this.settings; }
            private set { this.SetField(ref this.settings, value); }
        }

        //---------------------------------------------------------------------
        public void UpdateStartUpProject()
        {
            this.Settings.AdditionalSourcePatterns.Clear();
            this.Settings.AdditionalModulePatterns.Clear();
            this.Settings.ExcludedSourcePatterns.Clear();
            this.Settings.ExcludedModulePatterns.Clear();
            this.Settings.UnifiedDiffs.Clear();

            AddPatterns(this.Settings.ExcludedSourcePatterns, DefaultExcludedSourcePatterns);
            AddPatterns(this.Settings.ExcludedModulePatterns, DefaultExcludedModulePatterns);
        }

        //---------------------------------------------------------------------
        public void UpdateSettings(SettingsData settings)
        {
            if (settings == null)
                return;

            MergePatterns(this.Settings.AdditionalSourcePatterns, settings.AdditionalSourcePatterns);
            MergePatterns(this.Settings.AdditionalModulePatterns, settings.AdditionalModulePatterns);
            MergePatterns(this.Settings.ExcludedSourcePatterns, settings.ExcludedSourcePatterns);
            MergePatterns(this.Settings.ExcludedModulePatterns, settings.ExcludedModulePatterns);

            foreach (var unifiedDiff in settings.UnifiedDiffs ?? new ObservableCollection<FilterSettings.UnifiedDiff>())
                this.Settings.UnifiedDiffs.Add(unifiedDiff);
        }

        //---------------------------------------------------------------------
        public FilterSettings GetSettings()
        {
            return new FilterSettings
            {
                AdditionalSourcePaths = this.Settings.AdditionalSourcePatterns.ToStringList(),
                AdditionalModulePaths = this.Settings.AdditionalModulePatterns.ToStringList(),
                ExcludedSourcePaths = this.Settings.ExcludedSourcePatterns.ToStringList(),
                ExcludedModulePaths = this.Settings.ExcludedModulePatterns.ToStringList(),
                UnifiedDiffs = this.Settings.UnifiedDiffs
            };
        }

        //---------------------------------------------------------------------
        static void AddPatterns(
            ObservableCollection<BindableString> target,
            IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns.Where(p => !string.IsNullOrWhiteSpace(p)))
                target.Add(new BindableString(pattern));
        }

        //---------------------------------------------------------------------
        static void MergePatterns(
            ObservableCollection<BindableString> target,
            IEnumerable<BindableString> source)
        {
            foreach (var value in source ?? Enumerable.Empty<BindableString>())
            {
                var pattern = value?.Value;
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                if (!target.Any(existing => string.Equals(existing.Value, pattern)))
                    target.Add(new BindableString(pattern));
            }
        }
    }
}
