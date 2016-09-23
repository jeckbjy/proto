using proto;
using System;
using System.IO;
using System.Collections.Generic;

namespace plugin_csharp
{
    class CSharpPlugin : ProtoPlugin
    {
        public override string Target { get { return "csharp"; } }
        public override string Extension { get { return "cs"; } }

        public override void WriteManager(List<Proto> protos)
        {
            m_writer.WriteLine("namespace proto\n{");
            m_writer.WriteLine("    public static class PacketManager");
            m_writer.WriteLine("    {");
            m_writer.WriteLine("        public static void Init()");
            m_writer.WriteLine("        {");
            // 写入数据
            List<string> messages = new List<string>();
            // 防止重复
            Dictionary<string, string> fields = new Dictionary<string, string>();
            // 写入消息
            foreach (var proto in protos)
            {
                foreach (var scope in proto.Scopes)
                {
                    if (scope is EnumScope)
                        continue;

                    StructScope msg = scope as StructScope;

                    messages.Add(String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", msg.id_name));
                    foreach (var field in msg.fields)
                    {
                        if (field.container != Container.NONE)
                        {
                            string type = CSharpFormat.GetFieldType(field);
                            string value = String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", type);
                            fields.Add(type, value);
                        }
                    }
                }
            }

            if (messages.Count > 0)
            {
                foreach (var str in messages)
                    m_writer.WriteLine(str);
                m_writer.WriteLine();
            }

            if (fields.Count > 0)
            {
                foreach (var kv in fields)
                    m_writer.WriteLine(kv.Value);
            }

            m_writer.WriteLine("        }");
            m_writer.WriteLine("    }");
            m_writer.WriteLine("}");
        }

        protected override void WriteBegin()
        {
            m_writer.WriteLine("using System;");
            m_writer.WriteLine("using System.Collections.Generic;");
            m_writer.WriteLine();
            m_writer.WriteLine("namespace proto\n{");
        }

        protected override void WriteEnd()
        {
            m_writer.WriteLine("}");
        }

        protected override void WriteImport(string import)
        {

        }

        protected override void WriteEnum(EnumScope msg)
        {
            m_writer.WriteLine("    public enum {0}", msg.name);
            m_writer.WriteLine("    {");
            foreach (var field in msg.fields)
            {
                m_writer.WriteLine("        {0} = {1},", field.name, field.index);
            }
            m_writer.WriteLine("    }");
        }

        protected override void WriteStruct(StructScope msg)
        {
            m_writer.WriteLine("    [ProtoPacket({0})]", msg.HasID ? msg.GetMsgID(".") : "");
            m_writer.WriteLine("    public struct {0}", msg.name);
            m_writer.WriteLine("    {");
            foreach (var field in msg.fields)
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
