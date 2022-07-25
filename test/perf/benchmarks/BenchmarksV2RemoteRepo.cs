// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using Microsoft.PowerShell;
using System;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace Benchmarks
{
    public class BenchmarksV2RemoteRepo
    {
        System.Management.Automation.PowerShell pwsh;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Setting up the PowerShell runspace
            var defaultSS = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            defaultSS.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            pwsh = System.Management.Automation.PowerShell.Create(defaultSS);

            // Import the PSGet version we want to test
            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 2.2.5 -Force");
            pwsh.Invoke();
        }
		
        [GloblaCleanup]
        public void IterationCleanup()
        {
            pwsh.Dispose();
        }
		
        [Benchmark]
        public void FindAzModuleV2()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Find-Module -Name Az -Repository PSGallery");
            pwsh.Invoke();
        }

        [Benchmark]
        public void FindAzModuleAndDependenciesV2()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Find-Module -Name Az -IncludeDependencies -Repository PSGallery");
            pwsh.Invoke();
        }
		
        [Benchmark]
        public void InstallAzModuleAndDependenciesV2()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Install-Module -Name Az -Repository PSGallery -Force");
            pwsh.Invoke();
        }
    }
}
