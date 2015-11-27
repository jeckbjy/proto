using System;
using System.Collections.Generic;
using proto;

namespace plugin_lua
{
    class LuaProtoWriter : ProtoWriter
    {
        public override string Extension
        {
            get { return "lua"; }
        }

        public override void BeginWrite()
        {
            base.BeginWrite();
            //m_writer.WriteLine("local proto = require(\"proto\")");
            m_writer.WriteLine("require(\"proto\")");
        }

        public override void WriteImport(string import)
        {
            m_writer.WriteLine(String.Format("require(\"{0}\")", import));
        }

        public override void WriteEnum(Message msg)
        {
            m_writer.WriteLine(String.Format("-- enum_name:{0}", msg.name));
            foreach(var field in msg.fields)
            {
                m_writer.WriteLine(String.Format("{0} = {1}", field.name, field.index));
            }
        }

        public override void WriteStruct(Message msg)
        {
            m_writer.WriteLine("{0} = proto_class(\"{0}\")", msg.name);
            m_writer.WriteLine("{0}.pt_desc = {{", msg.name);
            foreach(var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                m_writer.WriteLine(
                    "    [{0,-2}] = {{ name = \"{1}\", container = {2}, type = {3}, cls = {4}, key = {5},}},",
                    field.index, field.name, GetContainer(field), GetLuaType(field.value), GetClassName(field.value), GetLuaType(field.key)
                    );
            }
            m_writer.WriteLine("}");
        }

        private string GetContainer(Field field)
        {
            switch(field.container)
            {
                case Container.NONE: 
                    return "PROTO_NIL";
                case Container.VECTOR:
                case Container.LIST:
                    return "PROTO_VEC";
                case Container.SET:
                case Container.HASH_SET:
                    return "PROTO_SET";
                case Container.MAP:
                case Container.HASH_MAP:
                    return "PROTO_MAP";
                default:
                    return "nil";
            }
        }

        private string GetLuaType(TypeInfo info)
        {
            switch(info.type)
            {
                case FieldType.BOOL:
                    return "PROTO_BOOL";
                case FieldType.SINT:
                case FieldType.SINT8:
                case FieldType.SINT16:
                case FieldType.SINT32:
                case FieldType.SINT64:
                    return "PROTO_S64";
                case FieldType.UINT:
                case FieldType.UINT8:
                case FieldType.UINT16:
                case FieldType.UINT32:
                case FieldType.UINT64:
                    return "PROTO_U64";
                case FieldType.FLOAT32:
                    return "PROTO_F32";
                case FieldType.FLOAT64:
                    return "PROTO_F64";
                case FieldType.STRING:
                case FieldType.BLOB:
                    return "PROTO_STR";
                case FieldType.STRUCT:
                    return "PROTO_MSG";
                default:
                    return "";
            }
        }

        private string GetClassName(TypeInfo info)
        {
            if (info.type == FieldType.STRUCT)
                return info.name;
            return "nil";
        }
    }
}
