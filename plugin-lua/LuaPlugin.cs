using proto;
using System;
using System.IO;
using System.Collections.Generic;

namespace plugin_lua
{
    public class LuaPlugin : ProtoPlugin
    {
        public override string Target { get { return "lua";} }
        public override string Extension { get { return "lua"; } }

        public override void WriteManager(List<Proto> protos)
        {
            base.WriteManager(protos);
            m_output = String.Format("{0}/{1}.{2}", m_outputDir, ManagerName, Extension);
            m_writer = new StreamWriter(File.Open(m_output, FileMode.Create));

            foreach(var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;

                m_writer.WriteLine(String.Format("require(\"{0}\")", proto.Name));
            }

            // 写入table
            m_writer.WriteLine();
            m_writer.WriteLine("local _packet_map = {");
            foreach (var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;
                foreach (var scope in proto.Scopes)
                {
                    if (scope is EnumScope)
                        continue;
                    var msg = scope as StructScope;
                    if (!msg.HasID)
                        continue;
                    m_writer.WriteLine(String.Format("    [{0}] = {1},", msg.id_name, msg.name));
                }
            }

            m_writer.WriteLine("}");
            m_writer.WriteLine();

            // 写入function
            m_writer.WriteLine("function create_packet(msgid)");
            m_writer.WriteLine("    return _packet_map[msgid]");
            m_writer.WriteLine("end");
            m_writer.Flush();
            m_writer.Close();
        }

        protected override void WriteBegin()
        {
            base.WriteBegin();

            m_writer.WriteLine("require(\"proto\")");
        }

        protected override void WriteImport(string import)
        {
            base.WriteImport(import);

            m_writer.WriteLine("require(\"{0}\")", import);
        }

        protected override void WriteEnum(EnumScope msg)
        {
            base.WriteEnum(msg);
            m_writer.WriteLine("-- enum_name:{0}", msg.name);
            foreach (var field in msg.fields)
            {
                m_writer.WriteLine(String.Format("{0} = {1}", field.name, field.index));
            }
        }

        protected override void WriteStruct(StructScope msg)
        {
            base.WriteStruct(msg);
            m_writer.WriteLine("{0} = proto_class(\"{0}\")", msg.name);
            if (msg.HasID)
                m_writer.WriteLine("{0}.proto_id = {1}", msg.name, msg.id_name);
            m_writer.WriteLine("{0}.proto_desc = {{", msg.name);
            foreach (var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                m_writer.WriteLine(
                    "    [{0,-2}] = {{ kind = {1}, ftype = {2, -9}, ktype = {3, -9}, name = \"{4}\", }},",
                    field.index, GetContainer(field.container), GetLuaType(field.key), GetLuaType(field.value), field.name
                    );
            }
            m_writer.WriteLine("}");
        }

        private string GetContainer(Container container)
        {
            switch (container)
            {
                case Container.NONE:
                    return "PROTO_NIL";
                case Container.VECT:
                case Container.LIST:
                    return "PROTO_VEC";
                case Container.SET:
                case Container.HSET:
                    return "PROTO_SET";
                case Container.MAP:
                case Container.HMAP:
                    return "PROTO_MAP";
                default:
                    return "nil";
            }
        }

        private string GetLuaType(TypeInfo info)
        {
            switch (info.type)
            {
                case FieldType.NONE:
                    return "PROTO_NIL";
                case FieldType.BOOL:
                    return "PROTO_BLN";
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
                    return info.name;
                default:
                    return "PROTO_NIL";
            }
        }
    }
}
