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
    public class BenchmarksV2
    {
        System.Management.Automation.PowerShell pwsh;

        [IterationSetup]
        public void IterationSetup()
        {
            var defaultSS = System.Management.Automation.Runspaces.InitialSessionState.CreateDefault2();
            //defaultSS.ImportPSModule(new string[] { })
            defaultSS.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            pwsh = System.Management.Automation.PowerShell.Create(defaultSS);

           // pwsh.AddScript("\"PShome var: $pshome\"");
            pwsh.AddScript("Import-Module PowerShellGet -RequiredVersion 2.2.5 -Force");
            var results = pwsh.Invoke();
        }
		
        [IterationCleanup]
        public void IterationCleanup()
        {
            // Disposing logic
            pwsh.Dispose();
        }
		
        [Benchmark]
        public void FindAzModuleV2()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Find-Module -Name Az -Repository PSGallery");
            results = pwsh.Invoke();
        }

        [Benchmark]
        public void FindAzModuleAndDependenciesV2()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Find-Module -Name Az -IncludeDependencies -Repository PSGallery");
            results = pwsh.Invoke();
        }
		
        [Benchmark]
        public void InstallAzModuleAndDependenciesV2()
        {
            Collection<PSObject> results = null;

            pwsh.AddScript("Install-Module -Name Az -Repository PSGallery -Force");
            results = pwsh.Invoke();
        }
    }
}
