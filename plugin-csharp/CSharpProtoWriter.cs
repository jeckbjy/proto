using System;
using System.Collections.Generic;
using proto;

namespace plugin_csharp
{
    class CSharpProtoWriter : ProtoWriter
    {
        public override string Extension
        {
            get
            {
                return "cs";
            }
        }

        public override void BeginWrite()
        {
            m_writer.WriteLine("using System;");
            m_writer.WriteLine("using System.Collections.Generic;");
            m_writer.WriteLine();
            m_writer.WriteLine("namespace proto\n{");
        }

        public override void EndWrite()
        {
            m_writer.WriteLine("}");
        }

        public override void WriteImport(string import)
        {

        }

        public override void WriteEnum(Message msg)
        {
            m_writer.WriteLine("    public enum {0}", msg.name);
            m_writer.WriteLine("    {");
            foreach(var field in msg.fields)
            {
                m_writer.WriteLine("        {0} = {1},", field.name, field.index);
            }
            m_writer.WriteLine("    }");
        }

        public override void WriteStruct(Message msg)
        {
            if(msg.HasID)
            {
                if (!string.IsNullOrEmpty(msg.id_owner))
                    m_writer.WriteLine("    [ProtoPacket({0}.{1})]", msg.id_owner, msg.id_name);
                else
                    m_writer.WriteLine("    [ProtoPacket({0})]", msg.id_name);
            }
            m_writer.WriteLine("    public struct {0}", msg.name);
            m_writer.WriteLine("    {");
            foreach(var field in msg.fields)
            {
                if (field.deprecated)
                    continue;
                // 写入tag
                m_writer.WriteLine("        [ProtoField({0})]", field.index);
                m_writer.WriteLine("        public {0} {1};", CSharpFormat.GetFieldType(field), field.name);
            }
            m_writer.WriteLine("    }");
        }
    }
}
