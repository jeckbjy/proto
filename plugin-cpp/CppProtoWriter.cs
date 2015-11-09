using System;
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
            m_writer.WriteLine(String.Format("#include \"{0}.h\"", import));
        }

        public override void WriteStruct(Message msg)
        {
            m_writer.WriteLine(String.Format("struct {0}", msg.name));
            m_writer.WriteLine("{");
            // fields
            foreach (var field in msg.fields)
            {
                if(field.container == Container.NONE)
                {// 指针
                    if(field.pointer)
                        m_writer.WriteLine(String.Format("    {0}* {1};", field.value.name, field.name));
                    else
                        m_writer.WriteLine(String.Format("    {0} {1};", field.value.name, field.name));
                }
                else
                {
                }
            }
            // 构造函数
            // 序列化函数
            // 反序列化函数
            m_writer.WriteLine("};");
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

        string GetFieldType(TypeInfo info)
        {
            if (info.type == FieldType.STRUCT)
                return info.name;
            return info.name;
        }
    }
}
