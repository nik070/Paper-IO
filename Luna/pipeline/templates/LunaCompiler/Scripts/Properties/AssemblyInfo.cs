using System.Reflection;
using Bridge;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[ assembly: AssemblyTitle( "UnityScriptsCompiler" ) ]
[ assembly: AssemblyDescription( "" ) ]
[ assembly: AssemblyConfiguration( "" ) ]
[ assembly: AssemblyCompany( "" ) ]
[ assembly: AssemblyProduct( "" ) ]
[ assembly: AssemblyCopyright( "anton" ) ]
[ assembly: AssemblyTrademark( "" ) ]
[ assembly: AssemblyCulture( "" ) ]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

[ assembly: AssemblyVersion( "1.0.*" ) ]

[ assembly: Convention( Target = ConventionTarget.Class, Notation = Notation.None ) ]

[ assembly: Convention( Target = ConventionTarget.Enum, Notation = Notation.None ) ]
[ assembly: Convention( Member = ConventionMember.EnumItem, Notation = Notation.None ) ]

[ assembly:
    Convention( Target = ConventionTarget.External, Member = ConventionMember.Field, Notation = Notation.CamelCase ) ]
[ assembly:
    Convention( Target = ConventionTarget.External, Member = ConventionMember.Method, Notation = Notation.CamelCase ) ]
[ assembly:
    Convention( Target = ConventionTarget.External, Member = ConventionMember.Property,
        Notation = Notation.CamelCase ) ]

[ assembly: Convention( Member = ConventionMember.Field, Notation = Notation.None ) ]
[ assembly: Convention( Member = ConventionMember.Method, Notation = Notation.None ) ]
[ assembly: Convention( Member = ConventionMember.Property, Notation = Notation.None ) ]
[ assembly: Convention( Target = ConventionTarget.Member, Notation = Notation.None ) ]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]
