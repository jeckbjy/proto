using System;
using proto;

namespace plugin_csharp
{
    static class CSharpFormat
    {
        public static string GetDataType(TypeInfo info)
        {
            switch(info.type)
            {
                case FieldType.BOOL: return "bool";
                case FieldType.SINT: return "int";
                case FieldType.UINT: return "uint";
                case FieldType.SINT8:  return "sbyte";
                case FieldType.UINT8:  return "byte";
                case FieldType.SINT16: return "short";
                case FieldType.UINT16: return "ushort";
                case FieldType.SINT32: return "int";
                case FieldType.UINT32: return "uint";
                case FieldType.SINT64: return "long";
                case FieldType.UINT64: return "ulong";
                case FieldType.FLOAT32: return "float";
                case FieldType.FLOAT64: return "double";
                case FieldType.STRING: return "string";
                case FieldType.BLOB: return "string";
                case FieldType.STRUCT: return info.name;
                default: return "";
            }
        }

        public static string GetContainerType(Container type)
        {
            switch(type)
            {
                case Container.LIST:
                    return "LinkedList";
                case Container.VEC:
                    return "List";
                case Container.MAP:
                    return "SortedDictionary";
                case Container.HASH_MAP:
                    return "Dictionary";
                case Container.SET:
                case Container.HASH_SET:
                    return "HashSet";
                default:
                    return "";
            }
        }

        public static string GetFieldType(Field field)
        {
            if (field.container == Container.NONE)
                return GetDataType(field.value);
            else if (field.container == Container.MAP || field.container == Container.HASH_MAP)
                return String.Format("{0}<{1}, {2}>", GetContainerType(field.container), GetDataType(field.key), GetDataType(field.value));
            else
                return String.Format("{0}<{1}>", GetContainerType(field.container), GetDataType(field.value));
        }
    }
}
