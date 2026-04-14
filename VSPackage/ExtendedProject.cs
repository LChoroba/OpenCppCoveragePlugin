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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenCppCoverage.VSPackage
{
    class ExtendedProject
    {        
        //---------------------------------------------------------------------
        public ExtendedProject(Project project, DynamicVCProject vcProject)
        {
            this.project_ = project;
            this.vcProject_ = vcProject;
        }
        
        //---------------------------------------------------------------------
        public string UniqueName
        {
            get
            {
                try
                {
                    return project_.UniqueName;
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        //---------------------------------------------------------------------
        public string Path
        {
            get
            {
                try
                {
                    return project_.FullName;
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }

        //---------------------------------------------------------------------
        public List<DynamicVCConfiguration> Configurations
        {
            get
            {
                return vcProject_.Configurations;
            }
        }

        //---------------------------------------------------------------------
        public List<DynamicVCFile> Files
        {
            get
            {
                return vcProject_.Files;
            }
        }

        readonly Project project_;
        readonly DynamicVCProject vcProject_;
    }
}
