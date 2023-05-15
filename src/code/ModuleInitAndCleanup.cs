using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.IO;
using System.Reflection;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    public class UnsafeAssemblyHandler : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        private static readonly Assembly s_self;
        private static readonly string s_dependencyFolder;
        private static readonly HashSet<string> s_dependencies;
        private static readonly AssemblyLoadContextProxy s_proxy;

        static UnsafeAssemblyHandler()
        {
            s_self = Assembly.GetExecutingAssembly();
            s_dependencyFolder = Path.Combine(Path.GetDirectoryName(s_self.Location), "dependencies");
            s_dependencies = new HashSet<string>(StringComparer.Ordinal);
            s_proxy = AssemblyLoadContextProxy.CreateLoadContext("powershellget-load-context");

            foreach (string filePath in Directory.EnumerateFiles(s_dependencyFolder, "*.dll"))
            {
                s_dependencies.Add(AssemblyName.GetAssemblyName(filePath).FullName);
            }
        }

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += HandleAssemblyResolve;
        }

        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= HandleAssemblyResolve;
        }

        private static bool IsAssemblyMatching(AssemblyName assemblyName, Assembly requestingAssembly)
        {
            // The requesting assembly is always available in .NET, but could be null in .NET Framework.
            // - When the requesting assembly is available, we check whether the loading request came from this
            //   module, so as to make sure we only act on the request from this module.
            // - When the requesting assembly is not available, we just have to depend on the assembly name only.
            return requestingAssembly is not null
                ? requestingAssembly == s_self && s_dependencies.Contains(assemblyName.FullName)
                : s_dependencies.Contains(assemblyName.FullName);
        }

        private static Assembly HandleAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requiredAssembly = new AssemblyName(args.Name);

            if (IsAssemblyMatching(requiredAssembly, args.RequestingAssembly))
            {
                string possibleAssembly = Path.Combine(s_dependencyFolder, $"{requiredAssembly.Name}.dll");

                if (File.Exists(possibleAssembly))
                {
                    // - In .NET, load the assembly into the custom assembly load context.
                    // - In .NET Framework, assembly conflict is not a problem, so we load the assembly
                    //   by 'Assembly.LoadFrom', the same as what powershell.exe would do.
                    return s_proxy is not null
                        ? s_proxy.LoadFromAssemblyPath(possibleAssembly)
                        : Assembly.LoadFrom(possibleAssembly);
                }
            }

            return null;
        }
    }

    internal class AssemblyLoadContextProxy
    {
        private readonly object _customContext;
        private readonly MethodInfo _loadFromAssemblyPath;

        private AssemblyLoadContextProxy(Type alc, string loadContextName)
        {
            var ctor = alc.GetConstructor(new[] { typeof(string), typeof(bool) });
            _loadFromAssemblyPath = alc.GetMethod("LoadFromAssemblyPath", new[] { typeof(string) });
            _customContext = ctor.Invoke(new object[] { loadContextName, false });
        }

        internal Assembly LoadFromAssemblyPath(string assemblyPath)
        {
            return (Assembly) _loadFromAssemblyPath.Invoke(_customContext, new[] { assemblyPath });
        }

        internal static AssemblyLoadContextProxy CreateLoadContext(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var alc = typeof(object).Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext");

            return alc is not null
                ? new AssemblyLoadContextProxy(alc, name)
                : null;
        }
    }
}
