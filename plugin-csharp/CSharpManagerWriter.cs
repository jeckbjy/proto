using System;
using System.IO;
using System.Collections.Generic;
using proto;

namespace plugin_csharp
{
    class CSharpManagerWriter : ManagerWriger
    {
        public override void WriteManager()
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
            foreach (var pro in m_protos)
            {
                foreach (var msg in pro.Messages)
                {
                    if (!msg.IsStruct)
                        continue;
                    messages.Add(String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", msg.name));
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
    }
}
