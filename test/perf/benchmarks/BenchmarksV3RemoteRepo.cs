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
    public class BenchmarksV3RemoteRepo
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
            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 3.0.14 -Force");
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
            pwsh.Commands.Clear();
            pwsh.AddScript("Find-PSResource -Name Az -Repository PSGallery");
            pwsh.Invoke();
        }

        [Benchmark]
        public void FindAzModuleAndDependenciesV3()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Find-PSResource -Name Az -IncludeDependencies -Repository PSGallery");
            pwsh.Invoke();
        }

        [Benchmark]
        public void InstallAzModuleV3()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Install-PSResource -Name Az -Repository PSGallery -TrustRepository -SkipDependencyCheck -Reinstall");
            pwsh.Invoke();
        }

        [Benchmark]
        public void InstallAzModuleAndDependenciesV3()
        {
            pwsh.Commands.Clear();
            pwsh.AddScript("Install-PSResource -Name Az -Repository PSGallery -TrustRepository -Reinstall");
            pwsh.Invoke();
        }
    }
}
