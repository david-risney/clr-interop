using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TlbImpRuleEngine;
using TypeLibTypes.Interop;

namespace TlbDump
{
    public class TlbToNode
    {
        public static Node GenerateLibrary(TypeLib library)
        {
            string name;
            library.GetDocumentation(-1, out name, out _, out _, out _);
            Node node = new Node("Library", name, null);

            int nCount = library.GetTypeInfoCount();
            for (int n = 0; n < nCount; ++n)
            {
                TypeInfo type = library.GetTypeInfo(n);
                //string typeTypeName = type.GetDocumentation();
                //NativeType2String.AddNativeUserDefinedType(typeTypeName);

                // For dual interfaces, it has a "funky" TKIND_DISPATCH|TKIND_DUAL interface with a parter of TKIND_INTERFACE|TKIND_DUAL interface
                // The first one is pretty bad and has duplicated all the interface members of its parent, which is not we want
                // We want the second v-table interface
                // So, if we indeed has seen this kind of interface, prefer its partner
                // However, we should not blindly get the partner because those two interfaces partners with each other
                // So we need to first test to see if the interface is both dispatch & dual, and then get its partner interface
                using (TypeAttr attr = type.GetTypeAttr())
                {
                    if (attr.IsDual && attr.IsDispatch)
                    {
                        TypeInfo typeReferencedType = type.GetRefTypeNoComThrow();
                        if (typeReferencedType != null)
                        {
                            type = typeReferencedType;
                        }
                    }
                }

                TypeInfoMatchTarget typeInfoMatchTarget = null;
                using (TypeAttr attr = type.GetTypeAttr())
                {
                    TYPEKIND kind = attr.typekind;
                    switch (kind)
                    {
                        case TYPEKIND.TKIND_ENUM:
                        case TYPEKIND.TKIND_INTERFACE:
                            {
                                typeInfoMatchTarget = new TypeInfoMatchTarget(library, type, kind);

                                if (!(typeInfoMatchTarget.Type == "Interface" &&
                                    typeInfoMatchTarget.Name == "IUnknown"))
                                {

                                    String childName;
                                    type.GetDocumentation(TYPEATTR.MEMBER_ID_NIL, out childName);
                                    Node childNode = new Node(typeInfoMatchTarget.Type, childName, null);
                                    node.children_.Add(childNode);
                                    childNode.Add("guid", typeInfoMatchTarget.GUID.ToString());

                                    childNode.children_.AddRange(GenerateMethods(type));
                                    childNode.children_.AddRange(GenerateFields(type));
                                }
                            }
                            break;
                    }
                }
            }

            return node;
        }

        private static List<Node> GenerateFields(TypeInfo parentTypeInfo)
        {
            List<Node> fields = new List<Node>();
            //
            // Walk through all vars (including elements from structs and disp interface, ...)
            //
            using (TypeAttr attr = parentTypeInfo.GetTypeAttr())
            {
                for (int i = 0; i < attr.cVars; ++i)
                {
                    FieldInfoMatchTarget variableInfo = new FieldInfoMatchTarget(parentTypeInfo, i);
                    string name;

                    parentTypeInfo.GetDocumentation(variableInfo.VarDesc.memid, out name);
                    Node child = new Node("Field", name, null);
                    child.Add("type", variableInfo.Type);

                    fields.Add(child);
                }
            }

            return fields;
        }

        private static List<Node> GenerateMethods(TypeInfo parentTypeInfo)
        {
            List<Node> methods = new List<Node>();

            using (TypeAttr attr = parentTypeInfo.GetTypeAttr())
            {
                //
                // Walk through all the function/propput/propget/propref properties
                //
                for (int i = ConvCommon2.GetIndexOfFirstMethod(parentTypeInfo, attr);
                     i < attr.cFuncs; ++i)
                {
                    FunctionInfoMatchTarget functionInfo =
                        new FunctionInfoMatchTarget(parentTypeInfo, (short)i);

                    string name;
                    parentTypeInfo.GetDocumentation(functionInfo.FuncDesc.memid, out name);

                    if (functionInfo.FuncDesc.IsPropertyGet)
                    {
                        name += " (getter)";
                    }
                    else if (functionInfo.FuncDesc.IsPropertyPut || functionInfo.FuncDesc.IsPropertyPutRef)
                    {
                        name += " (setter)";
                    }
                    Node node = new Node("Method", name, null);
                    node.children_.AddRange(GenerateParameters(parentTypeInfo, i));

                    methods.Add(node);
                }
            }


            return methods;
        }

        private static List<Node> GenerateParameters(TypeInfo interfaceTypeInfo, int funcIndex)
        {
            List<Node> parameters = new List<Node>();
            int paramIndex = 0;
            FuncDesc funcDesc = interfaceTypeInfo.GetFuncDesc(funcIndex);
            ElemDesc retElemDesc = funcDesc.elemdescFunc;
            SignatureInfoMatchTarget retSignatureInfo = new SignatureInfoMatchTarget(interfaceTypeInfo,
                funcIndex, retElemDesc, paramIndex);
            string typeString =
                    (new TlbType2String(interfaceTypeInfo, retElemDesc.tdesc)).GetTypeString();
            parameters.Add(new Node("Return", retSignatureInfo.Name, null));
            parameters[parameters.Count - 1].Add("type", retSignatureInfo.NativeSignature);

            ++paramIndex;
            for (int i = 0; i < funcDesc.cParams; ++i)
            {
                ElemDesc paramElemDesc = funcDesc.GetElemDesc(i);

                typeString =
                    (new TlbType2String(interfaceTypeInfo, paramElemDesc.tdesc)).GetTypeString();

                SignatureInfoMatchTarget paramSignatureInfo = new SignatureInfoMatchTarget(
                    interfaceTypeInfo, funcIndex, paramElemDesc, paramIndex);
                parameters.Add(new Node("Parameter", paramSignatureInfo.Name, null));
                parameters[parameters.Count - 1].Add("type", paramSignatureInfo.NativeSignature);

                ++paramIndex;
            }
            return parameters;
        }
    }

    public class ConvCommon2
    {
        /// <summary>
        /// This function is used to workaround around the fact that the TypeInfo might return IUnknown/IDispatch methods (in the case of dual interfaces)
        /// So we should always call this function to get the first index for different TypeInfo and never save the id
        /// </summary>
        static public int GetIndexOfFirstMethod(TypeInfo type, TypeAttr attr)
        {
            if (attr.typekind != TypeLibTypes.Interop.TYPEKIND.TKIND_DISPATCH) return 0;

            int nIndex = 0;
            if (attr.cFuncs >= 3)
            {
                // Check for IUnknown first
                using (FuncDesc func = type.GetFuncDesc(0))
                {
                    if (func.memid == 0x60000000 &&
                       func.elemdescFunc.tdesc.vt == (int)System.Runtime.InteropServices.VarEnum.VT_VOID &&
                       func.cParams == 2 &&
                       func.GetElemDesc(0).tdesc.vt == (int)System.Runtime.InteropServices.VarEnum.VT_PTR &&
                       func.GetElemDesc(1).tdesc.vt == (int)System.Runtime.InteropServices.VarEnum.VT_PTR &&
                       "QueryInterface" == type.GetDocumentation(func.memid))
                    {
                        nIndex = 3;
                    }
                }

                if (attr.cFuncs >= 7)
                {
                    using (FuncDesc func = type.GetFuncDesc(3))
                    {
                        // Check IDispatch
                        if (func.memid == 0x60010000 &&
                            func.elemdescFunc.tdesc.vt == (int)System.Runtime.InteropServices.VarEnum.VT_VOID &&
                            func.cParams == 1 &&
                            func.GetElemDesc(0).tdesc.vt == (int)System.Runtime.InteropServices.VarEnum.VT_PTR &&
                            "GetTypeInfoCount" == type.GetDocumentation(func.memid))
                        {
                            nIndex = 7;
                        }
                    }
                }
            }
            return nIndex;
        }

        /// <summary>
        /// If the type is aliased, return the ultimated non-aliased type if the type is user-defined, otherwise, return
        /// the aliased type directly. So the result could still be aliased to a built-in type.
        /// If the type is not aliased, just return the type directly
        /// </summary>
        static public void ResolveAlias(TypeInfo type, TypeDesc typeDesc, out TypeInfo realType, out TypeAttr realAttr)
        {
            if ((System.Runtime.InteropServices.VarEnum)typeDesc.vt != System.Runtime.InteropServices.VarEnum.VT_USERDEFINED)
            {
                // Already resolved
                realType = type;
                realAttr = type.GetTypeAttr();
                return;
            }
            else
            {
                TypeInfo refType = type.GetRefTypeInfo(typeDesc.hreftype);
                TypeAttr refAttr = refType.GetTypeAttr();

                // If the userdefined typeinfo is not itself an alias, then it is what the alias aliases.
                // Also, if the userdefined typeinfo is an alias to a builtin type, then the builtin
                // type is what the alias aliases.
                if (refAttr.typekind != TypeLibTypes.Interop.TYPEKIND.TKIND_ALIAS || (System.Runtime.InteropServices.VarEnum)refAttr.tdescAlias.vt != System.Runtime.InteropServices.VarEnum.VT_USERDEFINED)
                {
                    // Resolved
                    realType = refType;
                    realAttr = refAttr;
                }
                else
                {
                    // Continue resolving the type
                    ResolveAlias(refType, refAttr.tdescAlias, out realType, out realAttr);
                }
            }
        }
    }
}
