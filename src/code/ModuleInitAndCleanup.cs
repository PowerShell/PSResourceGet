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
        private static readonly Dictionary<string, AssemblyName> s_dependencies;
        private static readonly AssemblyLoadContextProxy s_proxy;

        private static readonly HashSet<string> NetFrameworkLoadFromPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.Buffers",
            "System.ClientModel",
            "System.Numerics.Vectors",
            "System.Threading.Tasks.Extensions",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Memory",
            "System.Diagnostics.DiagnosticSource",
            "System.Text.Json",
            "System.Security.Cryptography.ProtectedData"
        };

        static UnsafeAssemblyHandler()
        {
            s_self = Assembly.GetExecutingAssembly();
            s_dependencyFolder = Path.Combine(Path.GetDirectoryName(s_self.Location), "dependencies");
            s_dependencies = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);
            s_proxy = AssemblyLoadContextProxy.CreateLoadContext("psresourceget-load-context");

            foreach (string filePath in Directory.EnumerateFiles(s_dependencyFolder, "*.dll"))
            {
                AssemblyName dependency = AssemblyName.GetAssemblyName(filePath);
                s_dependencies[dependency.Name] = dependency;
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
            // Sometimes the requesting assembly can be one of the NuGet.* dlls, in such case, we check if the
            // requesting assembly is part of our pre-defined dependencies list.
            return requestingAssembly is not null
                ? (requestingAssembly == s_self || IsBundledDependency(requestingAssembly.GetName())) && IsCompatibleDependency(assemblyName)
                : IsCompatibleDependency(assemblyName);
        }

        private static bool IsBundledDependency(AssemblyName assemblyName)
        {
            return s_dependencies.TryGetValue(assemblyName.Name, out AssemblyName dependency)
                && string.Equals(dependency.FullName, assemblyName.FullName, StringComparison.OrdinalIgnoreCase);
        }

        // Dependencies are often compiled against older versions of shared assemblies than the ones bundled
        // with this module (e.g. Azure.Core references System.Text.Json 10.0.0.3 while 10.0.0.13 is bundled).
        // Without binding redirects, accept any request for the same assembly whose version is not newer than the bundled one.
        private static bool IsCompatibleDependency(AssemblyName requested)
        {
            if (!s_dependencies.TryGetValue(requested.Name, out AssemblyName bundled))
            {
                return false;
            }

            if (requested.Version is not null && requested.Version > bundled.Version)
            {
                return false;
            }

            if (!string.Equals(requested.CultureName ?? string.Empty, bundled.CultureName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            byte[] requestedToken = requested.GetPublicKeyToken();
            if (requestedToken is null || requestedToken.Length == 0)
            {
                return true;
            }

            byte[] bundledToken = bundled.GetPublicKeyToken();
            if (bundledToken is null || bundledToken.Length != requestedToken.Length)
            {
                return false;
            }

            for (int i = 0; i < requestedToken.Length; i++)
            {
                if (requestedToken[i] != bundledToken[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static Assembly HandleAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requiredAssembly = new AssemblyName(args.Name);
            string requiredAssemblyName = requiredAssembly.Name;

            // If on .NET framework load specific assemblies from dependency folder
            if (s_proxy is null
                && NetFrameworkLoadFromPath.Contains(requiredAssemblyName))
            {
                var netFxDepDllPath = Path.Combine(s_dependencyFolder, $"{requiredAssemblyName}.dll");
                return Assembly.LoadFrom(netFxDepDllPath);
            }

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
