using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;

namespace proto
{
    class Target
    {
        internal string name;
        internal string dir { get { return config["dir"]; } }
        internal Dictionary<string, string> config = new Dictionary<string, string>();

        public void parse(string param)
        {
            if(param != null && param.Length != 0)
            {
                string[] tokens = param.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    string[] datas = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (datas.Length != 2)
                        continue;
                    config.Add(datas[0], datas[1]);
                }
            }
            if(config["dir"] == null)
            {
                config["dir"] = "./";
            }
        }
    }
    class ProtoManager
    {
        Dictionary<string, IProtoPlugin> m_plugins = new Dictionary<string, IProtoPlugin>();
        Dictionary<string, Target> m_targets = new Dictionary<string, Target>();
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
                        var plugin = (IProtoPlugin)Activator.CreateInstance(t);
                        m_plugins.Add(plugin.Target, plugin);
                    }
                }
            }
        }

        void LoadConfig()
        {// 解析配置文件
            try
            {
                XmlTextReader reader = new XmlTextReader("./config.xml");
                while(reader.Read())
                {
                    if(reader.NodeType == XmlNodeType.Element)
                    {
                        if(reader.Name == "input")
                        {
                            string dir = reader.GetAttribute("dir");
                            if (dir.Length != 0)
                                AddInputDir(dir);
                        }
                        else if(reader.Name == "target")
                        {
                            Target target = new Target();
                            target.name = reader.GetAttribute("name");
                            string param = reader.GetAttribute("param");
                            target.parse(param);
                            string dir = reader.GetAttribute("dir");
                            if (dir != null)
                                target.config["dir"] = dir;
                            if (target.name != null)
                                m_targets.Add(target.name, target);
                        }
                    }
                }
            }
            catch(Exception)
            {
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
            foreach (var path in m_input)
            {
                DirectoryInfo di = new DirectoryInfo(path);
                FileInfo[] files = di.GetFiles("*.proto");
                for (int i = 0; i < files.Length; ++i)
                {
                    Proto proto = new Proto(files[i].FullName);
                    proto.Parse();
                    m_protos.Add(proto);
                }
            }
            Check();
            Output();
        }

        void Check()
        {
            // 所有结构体或者enum,不能有重名
            Dictionary<string, Message> blocks = new Dictionary<string, Message>();
            Dictionary<string, Field> enum_fields = new Dictionary<string, Field>();
            // 先统计信息
            foreach(var proto in m_protos)
            {
                foreach(var msg in proto.Messages)
                {
                    if (blocks.ContainsKey(msg.name))
                        throw new Exception(String.Format("duplicate name[{0}] in file[{1}]", msg.name, proto.Name));
                    blocks.Add(msg.name, msg);

                    if(msg.type == CmdType.ENUM)
                    {// 要求field名字不能重复
                        foreach (var field in msg.fields)
                        {
                            if(enum_fields.ContainsKey(field.name))
                                throw new Exception(String.Format("duplicate enum field name[{0}] in file[{1}]", field.name, proto.Name));
                            enum_fields.Add(field.name, field);
                        }
                    }
                }
            }

            // 校验ID和名字
            foreach(var proto in m_protos)
            {
                foreach(var msg in proto.Messages)
                {
                    if(msg.type == CmdType.STRUCT)
                    {
                        // todo:校验ID
                        // 校验field
                        foreach(var field in msg.fields)
                        {
                            if(field.value.type == FieldType.STRUCT)
                            {
                                Message block = blocks[field.value.name];
                                if (block == null || block.type != CmdType.STRUCT)
                                    throw new Exception(String.Format("cannot find field in struct[{0}]", msg.name));
                            }
                        }
                    }
                }
            }
        }

        void Output()
        {
            foreach (var kv in m_targets)
            {
                Target target = kv.Value;
                var plugin = m_plugins[kv.Key];
                if (plugin == null)
                    continue;
                IProtoWriter writer = plugin.CreateProtoWriter();
                //  创建目录
                if (!Directory.Exists(target.dir))
                {
                    Directory.CreateDirectory(target.dir);
                }
                // 输出每个文件
                foreach (var proto in m_protos)
                {
                    writer.Write(proto, target.config);
                }

                // 输出Manager
                IManagerWriter manager_writer = plugin.CreateManagerWriter();
                if (manager_writer != null)
                {
                    manager_writer.Write(m_protos, target.config);
                }
            }
        }
    }
}
