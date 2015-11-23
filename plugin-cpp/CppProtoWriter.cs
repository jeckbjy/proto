using System;
using System.Collections.Generic;
using proto;

namespace plugin_cpp
{
    class CppProtoWriter : ProtoWriter
    {
        public override string Extension
        {
            get
            {
                return "h";
            }
        }

        public override void WriteImport(string import)
        {
            m_writer.WriteLine("#include \"Stream.h\"");
            m_writer.WriteLine(String.Format("#include \"{0}.h\"", import));
        }

        public override void WriteEnum(Message msg)
        {
            m_writer.WriteLine(String.Format("enum {0}", msg.name));
            m_writer.WriteLine("{");
            foreach (var field in msg.fields)
            {
                m_writer.WriteLine(String.Format("    {0} = {1},", field.name, field.index));
            }
            m_writer.WriteLine("};");
        }

        public override void WriteStruct(Message msg)
        {
            m_writer.WriteLine(String.Format("struct {0} : public Message", msg.name));
            m_writer.WriteLine("{");
            // fields
            foreach (var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                if(field.container == Container.NONE)
                {// 指针
                    if(field.pointer)
                        m_writer.WriteLine(String.Format("    {0}* {1};", GetTypeName(field.value), field.name));
                    else
                        m_writer.WriteLine(String.Format("    {0} {1};", GetTypeName(field.value), field.name));
                }
                else if(field.container == Container.MAP || field.container == Container.HASH_MAP)
                {
                    m_writer.WriteLine(String.Format("    std::{0}<{1},{2}> {3}", GetContainerName(field.container), GetTypeName(field.key), GetTypeName(field.value), field.name));
                }
                else
                {
                    m_writer.WriteLine(String.Format("    std::{0}<{1}> {2}", GetContainerName(field.container), GetTypeName(field.value), field.name));
                }
            }

            // msgID
            if(!string.IsNullOrEmpty(msg.id_name))
            {
                m_writer.WriteLine(String.Format("    size_t msgid() {{ return {0}; }}", msg.id_name));
            }

            WriteEncode(msg.fields);
            WriteDecode(msg.fields);
            m_writer.WriteLine("};");
        }

        void WriteEncode(List<Field> fields)
        {
            m_writer.WriteLine("    void encode(StreamReader& stream) const");
            m_writer.WriteLine("    {");
            if (fields.Count > 0)
            {
                m_writer.WriteLine("        stream.beg_tag();");
                foreach (var field in fields)
                {
                    if (field.deprecated)
                        continue;
                    if (field.tag == 1)
                        m_writer.WriteLine(String.Format("        stream.write({0});", field.name));
                    else
                        m_writer.WriteLine(String.Format("        stream.write({0}, {1});", field.name, field.tag));
                }
                m_writer.WriteLine("        stream.end_tag();");
            }
            m_writer.WriteLine("    }");
        }

        void WriteDecode(List<Field> fields)
        {
            m_writer.WriteLine("    void decode(StreamReader& stream)");
            m_writer.WriteLine("    {");
            if(fields.Count > 0)
            {
                m_writer.WriteLine("        stream.beg_tag();");
                foreach(var field in fields)
                {
                    if (field.deprecated)
                        continue;
                    if(field.tag == 1)
                        m_writer.WriteLine(String.Format("        stream.read({0});", field.name));
                    else
                        m_writer.WriteLine(String.Format("        stream.read({0}, {1});", field.name, field.tag));
                }
                m_writer.WriteLine("        stream.end_tag();");
            }
            m_writer.WriteLine("    }");
        }

        string GetTypeName(TypeInfo info)
        {
            switch(info.type)
            {
                case FieldType.BOOL: return "bool";
                case FieldType.INT: return "int32_t";
                case FieldType.UINT: return "uint32_t";
                case FieldType.INT8: return "int8_t";
                case FieldType.INT16: return "int16_t";
                case FieldType.INT32: return "int32_t";
                case FieldType.INT64: return "int64_t";
                case FieldType.UINT8:  return "uint8_t";
                case FieldType.UINT16: return "uint16_t";
                case FieldType.UINT32: return "uint32_t";
                case FieldType.UINT64: return "uint64_t";
                case FieldType.FLOAT32: return "float";
                case FieldType.FLOAT64: return "double";
                case FieldType.STRING: return "string";
                case FieldType.BLOB: return "string";
                case FieldType.STRUCT:
                    return info.name;
                default:
                    return "";
            }
        }

        string GetContainerName(Container type)
        {
            switch(type)
            {
                case Container.VECTOR: return "vector";
                case Container.LIST: return "list";
                case Container.MAP: return "map";
                case Container.SET: return "set";
                case Container.HASH_MAP: return "hash_map";
                case Container.HASH_SET: return "hash_set";
                default:
                    return "";
            }
        }
    }
}
