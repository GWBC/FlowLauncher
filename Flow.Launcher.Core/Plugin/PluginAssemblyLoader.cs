using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyName assemblyName;

        private readonly String dependencyPath;

        internal PluginAssemblyLoader(string assemblyFilePath)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));
            dependencyPath = Path.GetDirectoryName(assemblyFilePath) ?? "";
        }

        internal Assembly LoadAssemblyAndDependencies()
        {
           return Load(assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            //查看是否已经加载
            var existAssembly = Default.Assemblies.FirstOrDefault(x => {
                return x.FullName == assemblyName.FullName;
            });
            if (existAssembly != null)
            {
                return existAssembly;
            }

            //尝试从默认程序集中加载DLL
            try
            {
                existAssembly = Default.LoadFromAssemblyName(assemblyName);
                if (existAssembly != null)
                {
                    return existAssembly;
                }
            }
            catch
            {

            }

            //尝试从主DLL路径下加载DLL
            var assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath == null)
            {                
                assemblyPath = Path.Join(dependencyPath, assemblyName.Name + ".dll");
                if (!assemblyPath.FileExists())
                {
                    assemblyPath = null;
                }
            }
           
            return LoadFromAssemblyPath(assemblyPath);
        }
        
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrEmpty(path))
            {
                return LoadUnmanagedDllFromPath(path);
            }

            return IntPtr.Zero;
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, Type type)
        {
            var allTypes = assembly.ExportedTypes;
            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Any(t => t == type));
        }
    }
}
