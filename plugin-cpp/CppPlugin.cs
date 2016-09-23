using proto;
using System;
using System.IO;
using System.Collections.Generic;

namespace plugin_cpp
{
    class CppPlugin : ProtoPlugin
    {
        public override string Target { get { return "cpp"; } }
        public override string Extension { get { return "h"; } }

        public override void WriteManager(List<Proto> protos)
        {
            base.WriteManager(protos);

            // write head
            string headPath = OutputDir + ManagerName + ".h";
            m_writer = new StreamWriter(headPath);
            m_writer.WriteLine("struct pt_message;");
            m_writer.WriteLine("pt_message* pt_create_packet(unsigned int msgid);");
            m_writer.Close();

            // write cpp
            string cppPath = OutputDir + ManagerName + ".cpp";
            m_writer = new StreamWriter(cppPath);
            m_writer.WriteLine("#include <proto.h>");
            m_writer.WriteLine("#include <{0}>", headPath);

            // 写入头文件等
            foreach (var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;
                m_writer.WriteLine(String.Format("#include <{0}.h>", proto.Name));
            }
            // 写入函数
            m_writer.WriteLine();
            m_writer.WriteLine("pt_message* pt_create_packet(uint32_t msgid){");
            m_writer.WriteLine("    switch(msgid){");
            foreach (var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;

                foreach (var scope in proto.Scopes)
                {
                    if (!(scope is StructScope))
                        continue;
                    StructScope msg = scope as StructScope;
                    if (!msg.HasID)
                        continue;
                    m_writer.WriteLine(String.Format("    case {0}: return new {1}();", msg.id_name, msg.name));
                }
            }
            m_writer.WriteLine("    default:return 0;");
            m_writer.WriteLine("    }");
            m_writer.WriteLine("}");
            m_writer.Close();
        }

        // 
        protected override void WriteBegin()
        {
            base.WriteBegin();
            m_writer.WriteLine("#include \"proto.h\"");
        }

        protected override void WriteImport(string import)
        {
            m_writer.WriteLine("#include \"{0}.h\"", import);
        }

        protected override void WriteEnum(EnumScope msg)
        {
            m_writer.WriteLine("enum {0}", msg.name);
            m_writer.WriteLine("{");
            foreach (var field in msg.fields)
            {
                m_writer.WriteLine("    {0} = {1},", field.name, field.index);
            }
            m_writer.WriteLine("};");
        }

        protected override void WriteStruct(StructScope msg)
        {
            m_writer.WriteLine("struct {0} : public pt_message", msg.name);
            m_writer.WriteLine("{");
            // fields
            foreach (var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                if (field.container == Container.NONE)
                {// 指针
                    if (field.pointer)
                        m_writer.WriteLine("    pt_ptr<{0}> {1};", GetTypeName(field.value), field.name);
                    else
                        m_writer.WriteLine("    {0} {1};", GetTypeName(field.value), field.name);
                }
                else if (field.container == Container.MAP || field.container == Container.HMAP)
                {
                    m_writer.WriteLine("    {0}<{1}, {2}> {3};", GetContainerName(field.container), GetTypeName(field.key), GetTypeName(field.value), field.name);
                }
                else
                {
                    m_writer.WriteLine("    {0}<{1}> {2};", GetContainerName(field.container), GetTypeName(field.value), field.name);
                }
            }

            // msgID
            if (!string.IsNullOrEmpty(msg.id_name))
            {
                m_writer.WriteLine();
                m_writer.WriteLine("    size_t msgid() {{ return {0}; }}", msg.id_name);
            }

            List<FieldSet> fsets = new List<FieldSet>();
            ProcessFields(msg.fields, fsets);

            WriteEncode(msg.fields, fsets);
            WriteDecode(msg.fields, fsets);
            m_writer.WriteLine("};");
        }

        void WriteEncode(List<StructField> fields, List<FieldSet> fsets)
        {
            m_writer.WriteLine();
            m_writer.WriteLine("    void encode(pt_encoder& stream) const");
            m_writer.WriteLine("    {");
            if (fsets.Count > 0)
            {
                foreach (var set in fsets)
                {
                    if (set.streamed)
                    {
                        m_writer.Write("        stream");
                        for (int i = set.beg_index; i <= set.end_index; ++i)
                            m_writer.Write(" << {0}", fields[i].name);
                        m_writer.WriteLine(";");
                    }
                    else
                    {
                        var field = fields[set.beg_index];
                        m_writer.WriteLine("        stream.write({0}, {1});", field.name, field.tag);
                    }
                }
            }
            m_writer.WriteLine("    }");
        }

        void WriteDecode(List<StructField> fields, List<FieldSet> fsets)
        {
            m_writer.WriteLine();
            m_writer.WriteLine("    void decode(pt_decoder& stream)");
            m_writer.WriteLine("    {");
            if (fsets.Count > 0)
            {
                foreach (var set in fsets)
                {
                    if (set.streamed)
                    {
                        m_writer.Write("        stream");
                        for (int i = set.beg_index; i <= set.end_index; ++i)
                            m_writer.Write(" >> {0}", fields[i].name);
                        m_writer.Write(";\n");
                    }
                    else
                    {
                        var field = fields[set.beg_index];
                        m_writer.WriteLine("        stream.read({0}, {1});", field.name, field.tag);
                    }
                }
            }
            m_writer.WriteLine("    }");
        }

        void ProcessFields(List<StructField> field_list, List<FieldSet> field_sets)
        {
            // 分组并限制最长10个
            StructField field;
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
            switch (info.type)
            {
                case FieldType.BOOL: return "pt_bool";
                case FieldType.SINT: return "pt_s32";
                case FieldType.UINT: return "pt_u32";
                case FieldType.SINT8: return "pt_s8";
                case FieldType.SINT16: return "pt_s16";
                case FieldType.SINT32: return "pt_s32";
                case FieldType.SINT64: return "pt_s64";
                case FieldType.UINT8: return "pt_u8";
                case FieldType.UINT16: return "pt_u16";
                case FieldType.UINT32: return "pt_u32";
                case FieldType.UINT64: return "pt_u64";
                case FieldType.FLOAT32: return "pt_f32";
                case FieldType.FLOAT64: return "pt_f64";
                case FieldType.STRING: return "pt_str";
                case FieldType.BLOB: return "pt_str";
                case FieldType.STRUCT:
                    return info.name;
                default:
                    return "";
            }
        }

        string GetContainerName(Container type)
        {
            switch (type)
            {
                case Container.LIST: return "pt_list";
                case Container.VECT: return "pt_vec";
                case Container.MAP: return "pt_map";
                case Container.SET: return "pt_set";
                case Container.HMAP: return "pt_hmap";
                case Container.HSET: return "pt_hset";
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
