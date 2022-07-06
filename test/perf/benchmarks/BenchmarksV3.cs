// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using System.Collections.ObjectModel;
using System.Management.Automation;
using Microsoft.PowerShell;

namespace benchmarks
{
    public class BenchmarksV3
    {
        System.Management.Automation.PowerShell pwsh;

        [IterationSetup]
        public void IterationSetup()
        {
            var defaultSS = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            defaultSS.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            pwsh = System.Management.Automation.PowerShell.Create(defaultSS);

            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 3.0.14 -Force");
            var results = pwsh.Invoke();
        }
        
        [IterationCleanup]
        public void IterationCleanup()
        {
            // Disposing logic
            pwsh.Dispose();
        }

        [Benchmark]
        public void FindAzModuleV3()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Find-PSResource -Name Az -Repository PSGallery");
            results = pwsh.Invoke();
        }

        [Benchmark]
        public void FindAzModuleAndDependenciesV3()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Find-PSResource -Name Az -IncludeDependencies -Repository PSGallery");
            results = pwsh.Invoke();
        }

        [Benchmark]
        public void InstallAzModuleV3()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Install-PSResource -Name Az -Repository PSGallery -TrustRepository -SkipDependencyCheck -Reinstall");
            results = pwsh.Invoke();
        }

        [Benchmark]
        public void InstallAzModuleAndDependenciesV3()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Install-PSResource -Name Az -Repository PSGallery -TrustRepository -Reinstall");
            results = pwsh.Invoke();
        }
    }
}
