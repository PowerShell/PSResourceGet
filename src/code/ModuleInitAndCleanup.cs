using System;
using System.Management.Automation;
using System.IO;
using System.Reflection;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
#if NET472
    public class UnsafeAssemblyHandler : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        private static readonly string s_asmLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += HandleAssemblyResolve;
        }

        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= HandleAssemblyResolve;
        }

        private static Assembly HandleAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requiredAssembly = new AssemblyName(args.Name);

            string possibleAssembly = Path.Combine(s_asmLocation, $"{requiredAssembly.Name}.dll");

            AssemblyName bundledAssembly = null;

            try
            {
                bundledAssembly = AssemblyName.GetAssemblyName(possibleAssembly);
            }
            catch
            {
                return null;
            }

            if (bundledAssembly.Version < requiredAssembly.Version)
            {
                return null;
            }

            return Assembly.LoadFrom(possibleAssembly);
        }
    }
#endif
}