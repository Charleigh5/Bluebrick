using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("BlueBrick.UI.Tests")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle(
#if LAB_BUILD
    "BlueBrick Lab"
#else
    "BlueBrick"
#endif
)]
[assembly: AssemblyDescription(
#if LAB_BUILD
    "SolidWorks Lab Addon for ViraInsight Engineering"
#else
    "SolidWorks Addon for ViraInsight Engineering"
#endif
)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("ViraInsight LLC")]
[assembly: AssemblyProduct(
#if LAB_BUILD
    "BlueBrick Lab"
#else
    "BlueBrick"
#endif
)]
[assembly: AssemblyCopyright("Copyright © 2025 ViraInsight LLC")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid(
#if LAB_BUILD
    "3b3ed8dd-8d9c-4b3c-8f15-615f6cfaf205"
#else
    "2713d927-26a2-4437-abda-798e2ca0824a"
#endif
)]

[assembly: AssemblyVersion("1.0.13.4")]
[assembly: AssemblyFileVersion("1.0.13.4")]

[assembly: AssemblyDelaySign(false)]
[assembly: AssemblyKeyFile("")]
[assembly: AssemblyKeyName("")]
