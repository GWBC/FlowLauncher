using System.Reflection;
using System.Runtime.InteropServices;

#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyDescription("Debug build, https://github.com/GWBC/FlowLauncher")]
#else
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyDescription("Release build, https://github.com/GWBC/FlowLauncher")]
#endif

[assembly: AssemblyCompany("Flow Launcher")]
[assembly: AssemblyProduct("Flow Launcher")]
[assembly: AssemblyCopyright("The MIT License (MIT)")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.1")]
[assembly: AssemblyFileVersion("2.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]
