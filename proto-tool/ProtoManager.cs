using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;

namespace proto
{
    class ProtoManager
    {
        Dictionary<string, ProtoPlugin> m_plugins = new Dictionary<string, ProtoPlugin>();
        HashSet<string> m_input = new HashSet<string>();
        List<Proto> m_protos = new List<Proto>();

        public ProtoManager()
        {
            LoadPlugins();
            LoadConfig();
        }

        void LoadPlugins()
        {
            // 当前目录
            DirectoryInfo di = new DirectoryInfo(".");
            FileInfo[] files = di.GetFiles("*.dll");
            for (int i = 0; i < files.Length; ++i)
            {
                FileInfo fi = files[i];
                if (!fi.Name.StartsWith("plugin"))
                    continue;
                // find protowriter
                Assembly assembly = Assembly.LoadFile(files[i].FullName);
                foreach (var t in assembly.GetTypes())
                {
                    if (t.GetInterface("IProtoPlugin") != null)
                    {
                        var plugin = (ProtoPlugin)Activator.CreateInstance(t);
                        m_plugins.Add(plugin.Target, plugin);
                    }
                }
            }
        }

        void LoadConfig()
        {// 解析配置文件
            XmlTextReader reader = new XmlTextReader("./config.xml");
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "input")
                    {
                        string dir = reader.GetAttribute("dir");
                        if (dir.Length != 0)
                            AddInputDir(dir);
                    }
                    else if (reader.Name == "target")
                    {
                        string name = reader.GetAttribute("name");
                        if(m_plugins.ContainsKey(name))
                        {
                            ProtoPlugin plugin = m_plugins[name];

                            plugin.OutputDir = reader.GetAttribute("dir");
                            plugin.ManagerName = reader.GetAttribute("manager");
                            plugin.SetCheckWriteTime(reader.GetAttribute("check"));
                        }
                    }
                }
            }
        }

        public void AddInputDir(string dir)
        {
            if(Directory.Exists(dir))
                m_input.Add(dir);
        }

        public void Process()
        {
            if (m_input.Count == 0)
                m_input.Add("./proto/");

            ProtoParser parser = new ProtoParser();
            foreach (var path in m_input)
            {
                DirectoryInfo di = new DirectoryInfo(path);
                FileInfo[] files = di.GetFiles("*.proto");
                for (int i = 0; i < files.Length; ++i)
                {
                    Proto proto = parser.Parse(files[i].FullName);
                    if (proto != null)
                        m_protos.Add(proto);
                }
            }
            Check();
            Output();
        }

        void Check()
        {
            // 所有结构体或者enum,不能有重名
            Dictionary<string, Scope> blocks = new Dictionary<string, Scope>();
            Dictionary<string, string> enum_fields = new Dictionary<string, string>();
            // 先统计信息
            foreach(var proto in m_protos)
            {
                foreach(var msg in proto.Scopes)
                {
                    if (blocks.ContainsKey(msg.name))
                        throw new Exception(String.Format("duplicate name[{0}] in file[{1}]", msg.name, proto.Name));
                    blocks.Add(msg.name, msg);

                    if(msg is EnumScope)
                    {// 要求enum field名字不能重复
                        EnumScope enumScope = msg as EnumScope;
                        foreach (var field in enumScope.fields)
                        {
                            if (enum_fields.ContainsKey(field.name))
                                throw new Exception(String.Format("duplicate enum field name[{0}] in file[{1}]", field.name, proto.Name));
                            enum_fields.Add(field.name, msg.name);
                        }
                    }
                }
            }

            // 校验ID和名字
            foreach(var proto in m_protos)
            {
                foreach(var scope in proto.Scopes)
                {
                    if (!(scope is StructScope))
                        continue;

                    StructScope msg = scope as StructScope;

                    if (msg.HasID && enum_fields.ContainsKey(msg.id_name))
                    {
                        msg.id_owner = enum_fields[msg.id_name];
                    }

                    // 校验field
                    foreach (StructField field in msg.fields)
                    {
                        if (field.value_type != FieldType.STRUCT)
                            continue;

                        if (blocks.ContainsKey(field.value_name))
                            throw new Exception(String.Format("cannot find field in struct[{0}]", msg.name));

                        // 校验proto包含关系
                    }
                }
            }
        }

        void Output()
        {
            foreach(var kv in m_plugins)
            {
                ProtoPlugin plugin = kv.Value;
                
                if(!Directory.Exists(plugin.OutputDir))
                {
                    Directory.CreateDirectory(plugin.OutputDir);
                }

                foreach(var proto in m_protos)
                {
                    plugin.WriteProto(proto);
                }

                plugin.WriteManager(m_protos);
            }
        }
    }
}
