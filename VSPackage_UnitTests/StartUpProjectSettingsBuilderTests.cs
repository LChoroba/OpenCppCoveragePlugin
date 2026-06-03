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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCppCoverage.VSPackage.Settings;
using System.IO;
using System.Linq;

namespace VSPackage_UnitTests
{
    [TestClass]
    public class StartUpProjectSettingsBuilderTests
    {
        //---------------------------------------------------------------------
        [TestMethod]
        public void ResolveProgramToRunFromCMakeFindsExecutableNamedAfterSelectedSourceFile()
        {
            using (var folder = new TemporayPath())
            {
                var sourceDirectory = Path.Combine(
                    folder.Path,
                    "source",
                    "tests",
                    "sample-test");
                Directory.CreateDirectory(sourceDirectory);

                var selectedDocumentPath = Path.Combine(sourceDirectory, "sample-test.cpp");
                File.WriteAllText(selectedDocumentPath, string.Empty);

                var buildDirectory = Path.Combine(folder.Path, "out", "build", "release");
                Directory.CreateDirectory(buildDirectory);
                File.WriteAllText(Path.Combine(buildDirectory, "CMakeCache.txt"), string.Empty);
                File.WriteAllText(Path.Combine(buildDirectory, "build.ninja"), string.Empty);

                var expectedProgramPath = Path.Combine(buildDirectory, "sample-test.cpp.exe");
                File.WriteAllText(expectedProgramPath, string.Empty);

                var programPath = StartUpProjectSettingsBuilder.ResolveProgramToRunFromCMake(
                    null,
                    null,
                    selectedDocumentPath,
                    null);

                Assert.AreEqual(expectedProgramPath, programPath);
            }
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void ResolveProgramToRunFromCMakeFindsExecutableWithoutProject()
        {
            using (var folder = new TemporayPath())
            {
                var sourceDirectory = Path.Combine(folder.Path, "source", "tests", "sample-test");
                Directory.CreateDirectory(sourceDirectory);

                var selectedDocumentPath = Path.Combine(sourceDirectory, "sample-test.cpp");
                File.WriteAllText(selectedDocumentPath, string.Empty);

                var buildDirectory = Path.Combine(folder.Path, "build", "release");
                Directory.CreateDirectory(buildDirectory);
                File.WriteAllText(Path.Combine(buildDirectory, "CMakeCache.txt"), string.Empty);

                var expectedProgramPath = Path.Combine(buildDirectory, "sample-test.exe");
                File.WriteAllText(expectedProgramPath, string.Empty);

                var programPath = StartUpProjectSettingsBuilder.ResolveProgramToRunFromCMake(
                    null,
                    null,
                    selectedDocumentPath,
                    null);

                Assert.AreEqual(expectedProgramPath, programPath);
            }
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void ResolveProgramToRunFromCMakeFindsDebugPostfixExecutable()
        {
            using (var folder = new TemporayPath())
            {
                var sourceDirectory = Path.Combine(
                    folder.Path,
                    "workspace",
                    "component-a",
                    "tests",
                    "component-check");
                Directory.CreateDirectory(sourceDirectory);

                var selectedDocumentPath = Path.Combine(sourceDirectory, "component-check.cpp");
                File.WriteAllText(selectedDocumentPath, string.Empty);

                var buildDirectory = Path.Combine(folder.Path, "out", "m", "b", "debug");
                Directory.CreateDirectory(buildDirectory);
                File.WriteAllText(Path.Combine(buildDirectory, "CMakeCache.txt"), string.Empty);
                File.WriteAllText(Path.Combine(buildDirectory, "build.ninja"), string.Empty);

                var expectedProgramPath = Path.Combine(buildDirectory, "component-check.cpp_d.exe");
                File.WriteAllText(expectedProgramPath, string.Empty);

                var programPath = StartUpProjectSettingsBuilder.ResolveProgramToRunFromCMake(
                    null,
                    null,
                    selectedDocumentPath,
                    null);

                Assert.AreEqual(expectedProgramPath, programPath);
            }
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void BuildCMakeSourcePathsUsesCMakeCacheSourceDirectories()
        {
            using (var folder = new TemporayPath())
            {
                var testDirectory = Path.Combine(
                    folder.Path,
                    "workspace",
                    "component-a",
                    "tests",
                    "component-check");
                var productionSourceDirectory = Path.Combine(
                    folder.Path,
                    "workspace",
                    "component-b",
                    "source");
                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(productionSourceDirectory);

                var selectedDocumentPath = Path.Combine(testDirectory, "component-check.cpp");
                File.WriteAllText(selectedDocumentPath, string.Empty);
                File.WriteAllText(Path.Combine(productionSourceDirectory, "library-code.cpp"), string.Empty);

                var buildDirectory = Path.Combine(folder.Path, "out", "m", "b", "debug");
                Directory.CreateDirectory(buildDirectory);
                File.WriteAllText(
                    Path.Combine(buildDirectory, "CMakeCache.txt"),
                    "CMAKE_HOME_DIRECTORY:INTERNAL=" + folder.Path + "\r\n"
                    + "component_a_SOURCE_DIR:STATIC=" + testDirectory + "\r\n"
                    + "component_b_SOURCE_DIR:STATIC=" + productionSourceDirectory + "\r\n");

                var commandPath = Path.Combine(buildDirectory, "component-check.cpp_d.exe");
                File.WriteAllText(commandPath, string.Empty);

                var sourcePaths = StartUpProjectSettingsBuilder.BuildCMakeSourcePaths(
                    selectedDocumentPath,
                    commandPath).ToList();

                Assert.AreEqual(2, sourcePaths.Count);
                CollectionAssert.AreEqual(
                    new[] { testDirectory, productionSourceDirectory },
                    sourcePaths);
            }
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void BuildCMakeSourcePathsFallsBackToSelectedDocumentDirectory()
        {
            using (var folder = new TemporayPath())
            {
                var sourceDirectory = Path.Combine(folder.Path, "source", "tests", "sample-test");
                Directory.CreateDirectory(sourceDirectory);

                var selectedDocumentPath = Path.Combine(sourceDirectory, "sample-test.cpp");
                File.WriteAllText(selectedDocumentPath, string.Empty);

                var sourcePaths = StartUpProjectSettingsBuilder.BuildCMakeSourcePaths(
                    selectedDocumentPath,
                    Path.Combine(folder.Path, "build", "sample-test.exe")).ToList();

                Assert.AreEqual(1, sourcePaths.Count);
                Assert.AreEqual(sourceDirectory, sourcePaths[0]);
            }
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void IsLikelyOptimizedCMakeBuildReturnsTrueForReleaseBuildPath()
        {
            var programPath = Path.Combine("build", "release", "sample-test.exe");

            Assert.IsTrue(StartUpProjectSettingsBuilder.IsLikelyOptimizedCMakeBuild(programPath));
        }

        //---------------------------------------------------------------------
        [TestMethod]
        public void IsLikelyOptimizedCMakeBuildReturnsFalseForDebugBuildPath()
        {
            var programPath = Path.Combine("build", "debug", "sample-test.exe");

            Assert.IsFalse(StartUpProjectSettingsBuilder.IsLikelyOptimizedCMakeBuild(programPath));
        }
    }
}
