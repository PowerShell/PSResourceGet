// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using Microsoft.PowerShell;
using System;
using System.Collections.ObjectModel;
using System.Management.Automation;

namespace benchmarks
{
    public class BenchmarksV2LocalRepo
    {
        System.Management.Automation.PowerShell pwsh;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Setting up the PowerShell runspace
            var defaultSS = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            defaultSS.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            pwsh = System.Management.Automation.PowerShell.Create(defaultSS);

            // Using PSGet v3 in order to save the Az modules and its dependencies
            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 3.0.14 -Force");
            pwsh.AddScript("New-Item TestRepo -ItemType Directory");
            pwsh.AddScript("Save-PSResource -Name Az -Repository PSGallery -AsNupkg -TrustRepository -Path .\\TestRepo");
			
            // Now import the PSGet module version we want to test and register a local repo
            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 2.2.5 -Force");
            pwsh.AddScript("Register-PSRepository -Name LocalRepo -SourceLocation .\\TestRepo");

            pwsh.Invoke();
        }
        
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            pwsh.Dispose();
        }

        [Benchmark]
        public void FindAzModuleV3()
        {
            pwsh.AddScript("Find-Module -Name Az -Repository LocalRepo");
            pwsh.Invoke();
        }

        [Benchmark]
        public void FindAzModuleAndDependenciesV3()
        {
            pwsh.AddScript("Find-Module -Name Az -IncludeDependencies -Repository LocalRepo");
            pwsh.Invoke();
        }

        [Benchmark]
        public void InstallAzModuleAndDependenciesV3()
        {
            pwsh.AddScript("Install-Module -Name Az -Repository LocalRepo -Force");
            pwsh.Invoke();
        }
    }
}
