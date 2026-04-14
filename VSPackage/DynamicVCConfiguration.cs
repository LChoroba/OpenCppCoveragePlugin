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

using System;

namespace OpenCppCoverage.VSPackage
{
    class DynamicVCConfiguration
    {
        //---------------------------------------------------------------------
        public DynamicVCConfiguration(dynamic configuration)
        {
            this.configuration_ = configuration;
            if (TryGetValue(() => configuration_.DebugSettings, out var debugSettings) && debugSettings != null)
                this.DebugSettings = new DynamicVCDebugSettings(debugSettings);
            else
                this.DebugSettings = new DynamicVCDebugSettings(null);

            var compilerTool = GetTool(configuration, "VCCLCompilerTool");
            if (compilerTool != null)
                this.OptionalVCCLCompilerTool = new DynamicVCCLCompilerTool(compilerTool);
        }

        //---------------------------------------------------------------------
        static dynamic GetTool(dynamic configuration, string toolKindToFind)
        {
            object tools;
            if (!TryGetValue<object>(() => configuration.Tools, out tools) || tools == null)
                return null;

            foreach (dynamic tool in (dynamic)tools)
            {
                string toolKind = null;
                if (tool != null && TryGetValue<string>(() => tool.ToolKind, out toolKind) && toolKind == toolKindToFind)
                    return tool;
            }

            return null;
        }

        //---------------------------------------------------------------------
        public string ConfigurationName
        {
            get
            {
                string value;
                return TryGetValue<string>(() => configuration_.ConfigurationName, out value) ? value : null;
            }
        }

        //---------------------------------------------------------------------
        public string PlatformName
        {
            get
            {
                string value;
                return TryGetValue<string>(() => configuration_.Platform.Name, out value) ? value : null;
            }
        }

        //---------------------------------------------------------------------
        public string Evaluate(string str)
        {
            string value;
            return TryGetValue<string>(() => configuration_.Evaluate(str), out value) ? value : str;
        }

        //---------------------------------------------------------------------
        public DynamicVCDebugSettings DebugSettings { get; }

        //---------------------------------------------------------------------
        public DynamicVCCLCompilerTool OptionalVCCLCompilerTool { get; }

        //---------------------------------------------------------------------
        public string PrimaryOutput
        {
            get
            {
                string value;
                return TryGetValue<string>(() => configuration_.PrimaryOutput, out value) ? value : null;
            }
        }

        readonly dynamic configuration_;

        //---------------------------------------------------------------------
        static bool TryGetValue<T>(Func<T> getter, out T value)
        {
            try
            {
                value = getter();
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }
    }
}
