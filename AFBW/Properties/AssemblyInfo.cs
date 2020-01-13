using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Resources;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AdvancedFlyByWireNeo")]
[assembly: KSPModFolder("AdvancedFlyByWireNeo")]
[assembly: AssemblyDescription("Input mod for Kerbal Space Program")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("uwx @ github.com/uwx")]
[assembly: AssemblyProduct("KSPAdvancedFlyByWire")]
[assembly: AssemblyCopyright(@"Copyright © uwx 2020, © Alexander ""nlight"" Dzhoganov 2014")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c749b9c9-cbf8-4782-a73c-bd17e631c366")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
//[assembly: AssemblyVersion("1.1.0.28")]
[assembly: NeutralResourcesLanguageAttribute("")]

[assembly: KSPAssemblyDependency("ClickThroughBlocker", 1, 0)]
[assembly: KSPAssemblyDependency("ToolbarController", 1, 0)]

// ReSharper disable InconsistentNaming
public sealed class KSPModFolderAttribute : Attribute
{
    public string ModFolderName { get; }

    public KSPModFolderAttribute(string modFolderName)
    {
        ModFolderName = modFolderName;
    }
}

public static class KSPHelpers
{
    public static string GetModFolderName(this Type type)
        => type.Assembly.GetCustomAttribute<KSPModFolderAttribute>().ModFolderName;
}
