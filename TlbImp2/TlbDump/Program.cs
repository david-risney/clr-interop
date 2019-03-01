using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TypeLibTypes.Interop;

namespace TlbDump
{
    class Program
    {
        static void Main(string[] args)
        {
            var program = new Program();
            program.DumpTypeLib(args[0], args[1]);
        }

        void DumpTypeLib(string tlbFilePath, string idlFilePath)
        {
            System.Runtime.InteropServices.ComTypes.ITypeLib TypeLib = null;
            APIHelper.LoadTypeLibEx(tlbFilePath, REGKIND.REGKIND_DEFAULT, out TypeLib);
            Dictionary<string, string> templates = new Dictionary<string, string>();

            templates.Add("Library",
@"# {name}

{description}

{children where kind = Interface use Interface}

{children where kind = Enum use Enum}");

            templates.Add("Interface",
@"## Interface {name}

{description}

{if children where kind = Event use EventHeader}
{if children where kind = Method use MethodHeader}
{if children where kind = Property use PropertyHeader}
");

            templates.Add("EventHeader",
@"### Events

{children where kind = Event use Event}
");

            templates.Add("MethodHeader",
@"### Methods

{children where kind = Method use Method}
");

            templates.Add("PropertyHeader",
@"### Properties

{children where kind = Property use Property}
");

            templates.Add("Event",
@"#### {eventName}

{description}

`{signature}`
Event args: {eventArgsType}
Event handler: {eventHandlerType}
");

            templates.Add("Method",
@"#### {name}

{description}

`{signature}`
");

            templates.Add("Property",
@"#### {name}

{description}

`{getterSignature}`

{if setterSignature use SetterHeader}
");
            templates.Add("SetterHeader",
@"`{setterSignature}`");

            templates.Add("Enum",
@"## Enum {name}

{description}

| Field | Value | Description |
| ----- | ----- | ----------- |
{children where kind = Field use Field}

");

            templates.Add("Field",
@"| {name} | {value} | {description} |");

            TypeLib tlb = new TypeLib((ITypeLib)TypeLib);
            Node library = TlbToNode.GenerateLibrary(tlb);
            Node helpStringLibrary = HelpStringIdlToNode.IdlToNode(idlFilePath);
            Node mergedTree = library.Clone();
            mergedTree.Merge(helpStringLibrary);
            // TlbToNode.LinkifyTypesInDescriptionsAndSignatures(mergedTree);
            Generator generator = new Generator(templates);

            var generated = generator.Fill(mergedTree, "Library");
            Console.WriteLine(generated);
            Console.WriteLine();
        }
    }

    [Flags]
    public enum REGKIND
    {
        REGKIND_DEFAULT = 0,
        REGKIND_REGISTER = 1,
        REGKIND_NONE = 2,
        REGKIND_LOAD_TLB_AS_32BIT = 0x20,
        REGKIND_LOAD_TLB_AS_64BIT = 0x40,
    }

    public class APIHelper
    {
        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void LoadTypeLibEx(String strTypeLibName,
            REGKIND regKind, out System.Runtime.InteropServices.ComTypes.ITypeLib TypeLib);
    }
}
