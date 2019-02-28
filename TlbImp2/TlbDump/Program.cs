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
{description?}

{children where kind = Interface use Interface}
{children where kind = Enum use Enum}");

            templates.Add("Interface",
@"## Interface {name}
{description?}
{children where kind = Method use Method}
{children where kind = Field use Field}
");
            templates.Add("Enum",
@"## Enum {name}
{description?}
{children where kind = Field use Field}
");
            templates.Add("Field",
@"### Enum Value {name}
{description?}
");
            templates.Add("Method",
@"### Method {name}
{description?}
#### Parameters
{children where kind = Parameter use Parameter}
#### Return value
{children where kind = Return use Return}
");
            templates.Add("Parameter",
@"* {type} {name} {description?}
");
            templates.Add("Return",
@"* {type} {description?}
");
            TypeLib tlb = new TypeLib((ITypeLib)TypeLib);
            Node library = TlbToNode.GenerateLibrary(tlb);
            Node helpStringLibrary = HelpStringIdlToNode.IdlToNode(idlFilePath);
            Node mergedTree = library.Clone();
            mergedTree.Merge(helpStringLibrary);
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
