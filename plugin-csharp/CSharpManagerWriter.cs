using System;
using System.IO;
using System.Collections.Generic;
using proto;

namespace plugin_csharp
{
    class CSharpManagerWriter : IManagerWriter
    {
        public void Write(List<Proto> protos, Dictionary<string, string> config)
        {
            string path = config["dir"] + config["mgr"] + ".cs";
            StreamWriter writer = new StreamWriter(File.Open(path, FileMode.Create));
            writer.WriteLine("namespace proto\n{");
            writer.WriteLine("    public static class PacketManager");
            writer.WriteLine("    {");
            writer.WriteLine("        public static void Init()");
            writer.WriteLine("        {");
            // 写入数据
            List<string> messages = new List<string>();
            // 防止重复
            Dictionary<string, string> fields = new Dictionary<string, string>();
            // 写入消息
            foreach(var pro in protos)
            {
                foreach(var msg in pro.Messages)
                {
                    if (!msg.IsStruct)
                        continue;
                    messages.Add(String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", msg.name));
                    foreach(var field in msg.fields)
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

            if(messages.Count > 0)
            {
                foreach (var str in messages)
                    writer.WriteLine(str);
                writer.WriteLine();
            }

            if(fields.Count > 0)
            {
                foreach (var kv in fields)
                    writer.WriteLine(kv.Value);
            }

            writer.WriteLine("        }");
            writer.WriteLine("    }");
            writer.WriteLine("}");
            writer.Close();
        }
    }
}
