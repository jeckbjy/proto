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

        public override void BeginWrite()
        {
            base.BeginWrite();
            m_writer.WriteLine("#include \"proto.h\"");
        }

        public override void WriteImport(string import)
        {
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
            m_writer.WriteLine(String.Format("struct {0} : public pt_message", msg.name));
            m_writer.WriteLine("{");
            // fields
            foreach (var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                if(field.container == Container.NONE)
                {// 指针
                    if(field.pointer)
                        m_writer.WriteLine(String.Format("    pt_ptr<{0}> {1};", GetTypeName(field.value), field.name));
                    else
                        m_writer.WriteLine(String.Format("    {0} {1};", GetTypeName(field.value), field.name));
                }
                else if(field.container == Container.MAP || field.container == Container.HASH_MAP)
                {
                    m_writer.WriteLine(String.Format("    {0}<{1}, {2}> {3};", GetContainerName(field.container), GetTypeName(field.key), GetTypeName(field.value), field.name));
                }
                else
                {
                    m_writer.WriteLine(String.Format("    {0}<{1}> {2};", GetContainerName(field.container), GetTypeName(field.value), field.name));
                }
            }

            // msgID
            if(!string.IsNullOrEmpty(msg.id_name))
            {
                m_writer.WriteLine(String.Format("    size_t msgid() {{ return {0}; }}", msg.id_name));
            }

            List<FieldSet> fsets = new List<FieldSet>();
            ProcessFields(msg.fields, fsets);

            WriteEncode(msg.fields, fsets);
            WriteDecode(msg.fields, fsets);
            m_writer.WriteLine("};");
        }

        void WriteEncode(List<Field> fields, List<FieldSet> fsets)
        {
            m_writer.WriteLine("    void encode(pt_encoder& stream) const");
            m_writer.WriteLine("    {");
            if (fsets.Count > 0)
            {
                m_writer.WriteLine("        stream.beg_tag();");
                foreach(var set in fsets)
                {
                    if(set.streamed)
                    {
                        m_writer.Write("        stream");
                        for (int i = set.beg_index; i <= set.end_index; ++i)
                            m_writer.Write(String.Format(" << {0}", fields[i].name));
                        m_writer.WriteLine(";");
                    }
                    else
                    {
                        Field field = fields[set.beg_index];
                        m_writer.WriteLine(String.Format("        stream.write({0}, {1});", field.name, field.tag));
                    }
                }
                m_writer.WriteLine("        stream.end_tag();");
            }
            m_writer.WriteLine("    }");
        }

        void WriteDecode(List<Field> fields, List<FieldSet> fsets)
        {
            m_writer.WriteLine("    void decode(pt_decoder& stream)");
            m_writer.WriteLine("    {");
            if (fsets.Count > 0)
            {
                m_writer.WriteLine("        stream.beg_tag();");
                foreach (var set in fsets)
                {
                    if (set.streamed)
                    {
                        m_writer.Write("        stream");
                        for (int i = set.beg_index; i <= set.end_index; ++i)
                            m_writer.Write(String.Format(" >> {0}", fields[i].name));
                        m_writer.Write(";\n");
                    }
                    else
                    {
                        Field field = fields[set.beg_index];
                        m_writer.WriteLine(String.Format("        stream.read({0}, {1});", field.name, field.tag));
                    }
                }
                m_writer.WriteLine("        stream.end_tag();");
            }
            m_writer.WriteLine("    }");
        }

        void ProcessFields(List<Field> field_list, List<FieldSet> field_sets)
        {
            // 分组并限制最长10个
            Field field;
            FieldSet fset;
            for (int index = 0; index < field_list.Count; ++index)
            {
                field = field_list[index];
                if (field.deprecated)
                    continue;
                fset = new FieldSet();
                field_sets.Add(fset);
                fset.beg_index = index;
                if (field.tag > 1)
                {
                    fset.streamed = false;
                    fset.end_index = index;
                }
                else
                {
                    fset.streamed = true;
                    // 查找结束位置
                    int next_range = Math.Min(field_list.Count, index + 10);
                    int end_index = index + 1;
                    for (; end_index < next_range; ++end_index)
                    {
                        field = field_list[end_index];
                        if (field.deprecated || field.tag > 1)
                            break;
                    }
                    fset.end_index = end_index - 1;
                }
                // 移动索引
                index = fset.end_index;
            }
        }

        string GetTypeName(TypeInfo info)
        {
            switch(info.type)
            {
                case FieldType.BOOL:    return "pt_bool";
                case FieldType.SINT:    return "pt_s32";
                case FieldType.UINT:    return "pt_u32";
                case FieldType.SINT8:   return "pt_s8";
                case FieldType.SINT16:  return "pt_s16";
                case FieldType.SINT32:  return "pt_s32";
                case FieldType.SINT64:  return "pt_s64";
                case FieldType.UINT8:   return "pt_u8";
                case FieldType.UINT16:  return "pt_u16";
                case FieldType.UINT32:  return "pt_u32";
                case FieldType.UINT64:  return "pt_u64";
                case FieldType.FLOAT32: return "pt_f32";
                case FieldType.FLOAT64: return "pt_f64";
                case FieldType.STRING:  return "pt_str";
                case FieldType.BLOB:    return "pt_str";
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
                case Container.LIST: return "pt_list";
                case Container.VEC: return "pt_vec";
                case Container.MAP: return "pt_map";
                case Container.SET: return "pt_set";
                case Container.HASH_MAP: return "pt_hmap";
                case Container.HASH_SET: return "pt_hset";
                default:
                    return "";
            }
        }

        // Field集合
        class FieldSet
        {
            public bool streamed;
            public int beg_index;
            public int end_index;
        }
    }
}
