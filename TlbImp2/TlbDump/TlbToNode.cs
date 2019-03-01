using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TlbImpRuleEngine;
using TypeLibTypes.Interop;

namespace TlbDump
{
    public class TlbToNode
    {
        private static void MergeGetterAndSetterProperties(Node node, Node parent = null)
        {
            if (parent != null)
            {
                bool isSetter = node.properties_.ContainsKey("setterSignature");
                List<Node> matchingSiblings = parent.FindAllChildren(new string[] { node.properties_["name"] });
                foreach (var sibling in matchingSiblings)
                {
                    if (sibling != node)
                    {
                        sibling.Merge(node);
                        node.Merge(sibling);
                    }
                }

                if (isSetter)
                {
                    parent.children_.Remove(node);
                }
            }
            List<Node> children = new List<Node>(node.children_);
            foreach (var child in children)
            {
                MergeGetterAndSetterProperties(child, node);
            }
        }

        public static void LinkifyTypesInDescriptionsAndSignatures(Node node)
        {
            HashSet<string> typeNames = new HashSet<string>();
            GetTypeNames(node, typeNames);

            LinkifyTypesInDescriptionsAndSignatures(node, typeNames);
        }

        private static void GetTypeNames(Node node, HashSet<string> typeNames)
        {
            typeNames.Add(node.properties_["name"]);

            foreach (Node child in node.children_)
            {
                switch (child.properties_["kind"])
                {
                    case "Interface":
                    case "Enum":
                    case "Method":
                    case "Property":
                    case "Event":
                        GetTypeNames(child, typeNames);
                        break;
                }
                
            }
        }

        private static void LinkifyTypesInDescriptionsAndSignatures(Node node, HashSet<string> typeNames)
        {
            foreach (string typeName in typeNames)
            {
                string prefix = node.properties_["kind"] == "Interface" ? "Interface " : "";
                string replacement = "[" + typeName + "](#" + prefix + typeName.ToLower() + ")";
                LinkifyProperty(node, "description", typeName, replacement);
                LinkifyProperty(node, "getterSignature", typeName, replacement);
                LinkifyProperty(node, "setterSignature", typeName, replacement);
                LinkifyProperty(node, "signature", typeName, replacement);
            }

            foreach (Node child in node.children_)
            {
                LinkifyTypesInDescriptionsAndSignatures(child, typeNames);
            }
        }

        private static void LinkifyProperty(Node node, string property, string typeName, string replacement)
        {
            if (node.properties_.ContainsKey(property))
            {
                Regex regex = new Regex(@"(\W|^)(" + typeName + @")(\W|$)");
                node.properties_[property] = regex.Replace(node.properties_[property], @"$1" + replacement + @"$3");
            }
        }

        public static Node GenerateLibrary(TypeLib library)
        {
            string name;
            library.GetDocumentation(-1, out name, out _, out _, out _);
            Node node = new Node("Library", name);

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
                                    Node childNode = new Node(typeInfoMatchTarget.Type, childName);
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

            MergeGetterAndSetterProperties(node);
            LinkifyTypesInDescriptionsAndSignatures(node);
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
                    Node child = new Node("Field", name);
                    child.Add("type", variableInfo.Type);
                    child.Add("value", "" + variableInfo.Index); // Not sure this is correct.

                    fields.Add(child);
                }
            }

            return fields;
        }

        private enum MethodKind
        {
            Getter,
            Setter,
            Method,
            Event,
        };

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

                    bool isGetter = functionInfo.FuncDesc.IsPropertyGet;
                    bool isSetter = (functionInfo.FuncDesc.IsPropertyPut || functionInfo.FuncDesc.IsPropertyPutRef);
                    bool isProperty = isGetter || isSetter;
                    bool isEvent = name.StartsWith("Set") && name.EndsWith("EventHandler");
                    string methodKindName = isProperty ? "Property" : (isEvent ? "Event" : "Method");

                    Node node = new Node(methodKindName, name);
                    node.children_.AddRange(GenerateParameters(parentTypeInfo, i));

                    if (functionInfo.FuncDesc.IsPropertyGet)
                    {
                        node.Add("getterSignature", GenerateSignatureForNode(node, MethodKind.Getter));
                    }
                    else if (functionInfo.FuncDesc.IsPropertyPut || functionInfo.FuncDesc.IsPropertyPutRef)
                    {
                        node.Add("setterSignature", GenerateSignatureForNode(node, MethodKind.Setter));
                    }
                    else
                    {
                        node.Add("signature", GenerateSignatureForNode(node, MethodKind.Method));
                    }

                    if (isEvent)
                    {
                        string eventName = name.Substring("Set".Length, name.Length - "SetEventHandler".Length);
                        node.Add("eventHandlerType", "IEB" + eventName + "EventHandler");
                        node.Add("eventArgsType", "IEB" + eventName + "EventArgs");
                        node.Add("eventName", eventName);
                    }

                    methods.Add(node);
                }
            }


            return methods;
        }

        private static string GenerateSignatureForNode(Node node, MethodKind methodKind)
        {
            var returnNode = node.children_.Where(child => child.properties_["kind"] == "Return").FirstOrDefault();
            var paramNodes = node.children_.Where(child => child.properties_["kind"] == "Parameter");
            string prefix = "";
            if (methodKind == MethodKind.Getter)
            {
                prefix = "get_";
            }
            else if (methodKind == MethodKind.Setter)
            {
                prefix = "put_";
            }

            return "" +
                returnNode.properties_["type"] + " " +
                prefix + node.properties_["name"] + "(" +
                String.Join(", ", paramNodes.Select(param => param.properties_["type"] + " " + param.properties_["name"])) +
                ")";
        }

        private static string NormalizeTypeString(string nonNormalizedType)
        {
            string normalizedType = nonNormalizedType;
            if (normalizedType.IndexOf("]") >= 0)
            {
                normalizedType = normalizedType.Substring(normalizedType.IndexOf("]") + 1);
            }
            normalizedType = normalizedType.Replace(" *", "*");
            normalizedType = normalizedType.Replace("LPWSTR", "LPCWSTR");
            normalizedType = normalizedType.Replace("INT", "BOOL");
            normalizedType = normalizedType.Replace("tagRECT", "RECT");

            return normalizedType;
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
            parameters.Add(new Node("Return", retSignatureInfo.Name));
            parameters[parameters.Count - 1].Add("type", NormalizeTypeString(retSignatureInfo.NativeSignature));

            ++paramIndex;
            for (int i = 0; i < funcDesc.cParams; ++i)
            {
                ElemDesc paramElemDesc = funcDesc.GetElemDesc(i);

                typeString =
                    (new TlbType2String(interfaceTypeInfo, paramElemDesc.tdesc)).GetTypeString();

                SignatureInfoMatchTarget paramSignatureInfo = new SignatureInfoMatchTarget(
                    interfaceTypeInfo, funcIndex, paramElemDesc, paramIndex);
                parameters.Add(new Node("Parameter", paramSignatureInfo.Name));
                parameters[parameters.Count - 1].Add("type", NormalizeTypeString(paramSignatureInfo.NativeSignature));

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
