using System;
using System.Collections.Generic;
using System.IO;

namespace proto
{
    public enum FieldType
    {
        BOOL,
        INT8,
        UINT8,
        INT16,
        UINT16,
        INT32,
        UINT32,
        INT64,
        UINT64,
        FLOAT32,
        FLOAT64,
        STRING,
        BLOB,       // 字节组
        STRUCT,     // 自定义消息,需要提供名字
    }

    public enum Container
    {
        NONE,
        VECTOR,
        LIST,
        MAP,
        SET,
        HASH_MAP,
        HASH_SET,
    }

    public enum CmdType
    {
        UNKNOWN,    // 未知类型
        ENUM,       // 枚举
        STRUCT,     // 结构体
    }

    public struct TypeInfo
    {
        public string name;
        public FieldType type;
        public void SetName(string name)
        {
            this.name = name;
            this.type = ParseType(name);
        }

        public static FieldType ParseType(string str)
        {
            switch (str)
            {
                case "bool": return FieldType.BOOL;
                case "int8": return FieldType.INT8;
                case "int16": return FieldType.INT16;
                case "int32": return FieldType.INT32;
                case "int64": return FieldType.INT64;
                case "uint8": return FieldType.UINT8;
                case "uint16": return FieldType.UINT16;
                case "uint32": return FieldType.UINT32;
                case "uint64": return FieldType.UINT64;
                case "string": return FieldType.STRING;
                case "blob": return FieldType.BLOB;
                default: return FieldType.STRUCT;
            }
        }
    }

    public class Field
    {
        public string name;
        public uint index = 0;    // 唯一索引
        // struct info
        public TypeInfo key;
        public TypeInfo value;
        public Container container = Container.NONE;
        public bool pointer = false;    // 是否是指针,c++中构造析构
        public bool deprecated = false; // 是否废弃

        public static Container ParseContainer(string str)
        {
            switch(str)
            {
                case "vector":return Container.VECTOR;
                case "list":return Container.LIST;
                case "map":return Container.MAP;
                case "set":return Container.SET;
                case "hash_map":return Container.HASH_MAP;
                case "hash_set":return Container.HASH_SET;
                default:return Container.NONE;
            }
        }
    }

    // enum or struct
    public class Message
    {
        public CmdType type = CmdType.UNKNOWN;
        public string name;
        public string id_name;   // 唯一id，可以是数字，或者是unique
        public string id_owner;
        public List<Field> fields = new List<Field>();

        internal void process()
        {
            // 默认从0开始
            // 自动累加索引
            for(int i = 1; i < fields.Count; ++i)
            {
                if(fields[i].index != 0)
                    fields[i].index = fields[i - 1].index + 1;
            }
        }
    }

    /*
     * 单行注释#
     * 分隔符;或\n
     * \n或者;表示一个语句的结束
     * enum:
     * struct,message:message可以附加一个ID,ID可以是整数数字，或者enum标识符
     * example:
     * import "test.proto";
     * import "test1.proto";
     * enum Mode
     * {
     *      Mode_Buy = 1,
     * }
     * enum MsgID
     * {
     *      S2C_Login = 1
     *      S2C_Logout
     * }
     * 
     * struct Person
     * {
     *      string name;
     * }
     * 
     * 所有的结构体都初始化成指针
     * ID不能重复
     * #this is doc
     * struct LoginMsg = S2C_Login
     * {
     *      Person* person;
     *      string  name;
     *      int     ivalue;
     *      uint    uvalue=10;
     *      vector<int> aa;
     *      map<int,string> bb;
     *      hash_map<int, Person> cc;
     * }
     */
    public class Proto
    {
        string m_name;     // 不含路径
        string m_path;     // 文件路径
        List<String>  m_imports = new List<string>();       // 注意不包含proto后缀
        List<Message> m_messages = new List<Message>();

        public string Name
        {
            get { return m_name; }
        }

        public string FilePath
        {
            get { return m_path; }
        }

        public List<String> Imports { get { return m_imports; } }
        public List<Message> Messages { get { return m_messages; } }

        public Proto(string path)
        {
            this.m_path = path;
            this.m_name = Path.GetFileNameWithoutExtension(path);
        }

        public void Parse()
        {
            ProtoReader reader = new ProtoReader(m_path);
            while(!reader.EndOfStream)
            {
                string cmd = reader.ReadLine();
                if (cmd.Length == 0)
                    continue;
                if(cmd.StartsWith("import "))
                {
                    int start = cmd.IndexOf('\"', 7);
                    int end = cmd.LastIndexOf('\"');
                    if (start == -1 || end == -1)
                        throw new Exception("bad import" + reader.Line + cmd);
                    string file = cmd.Substring(start + 1, end - start - 1);
                    if (!file.EndsWith(".proto"))
                        throw new Exception(String.Format("import must end with .proto in line{0} of file{1}", reader.Line, m_path));
                    file = file.Remove(file.Length - 6);
                    m_imports.Add(file);
                    continue;
                }
                // 数据结构处理
                string[] tokens = cmd.Split(new char[]{ ' '}, StringSplitOptions.RemoveEmptyEntries);
                CmdType type = CmdType.UNKNOWN;
                switch(tokens[0])
                {
                    case "enum":type = CmdType.ENUM;break;
                    case "struct":type = CmdType.STRUCT;break;
                    default:
                        throw new Exception("unknow cmd line =" + reader.Line + ":cmd =" + cmd);
                }
                if (tokens.Length != 2 && tokens.Length != 4)
                    throw new Exception("bad message" + reader.Line + cmd);
                Message message = new Message();
                m_messages.Add(message);
                message.type = type;
                message.name = tokens[1];
                if(tokens.Length == 4)
                {
                    if(tokens[2] != "=")
                        throw new Exception("bad message" + reader.Line + cmd);
                    message.id_name = tokens[3];
                }
                ParseFields(message, reader);
                message.process();
            }
        }

        private void ParseFields(Message message, ProtoReader reader)
        {
            string data = reader.ReadLine();
            if (data != "{")
                throw new Exception("message must start {");
            while(true)
            {
                if (reader.EndOfStream)
                    throw new Exception("not find }");
                data = reader.ReadLine();
                if (data == "}")
                    break;
                Field field;
                switch(message.type)
                {
                    case CmdType.ENUM: field = ParseEnumField(data, reader.Line); break;
                    case CmdType.STRUCT: field = ParseStructField(data, reader.Line); break;
                    default:
                        throw new Exception("unknow msg type");
                }

                message.fields.Add(field);
            }
        }

        private Field ParseEnumField(string data, int line)
        {
            Field field = new Field();
            string[] tokens = data.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens.Length > 2)
                throw new Exception("bad enum field" + line + data);

            field.name = tokens[0].Trim();
            if(tokens.Length == 2)
                field.index = UInt32.Parse(tokens[1]);

            return field;
        }

        private Field ParseStructField(string data, int line)
        {
            Field field = new Field();
             
            // (type) name [= index]
            string type_name;
            if (data.IndexOf('=') != -1)
            {
                string[] tokens = data.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 2)
                {
                    if (tokens[1] == "delete" || tokens[1] == "deprecated")
                        field.deprecated = true;
                    else
                        field.index = UInt32.Parse(tokens[1]);
                }
                type_name = tokens[0].Trim();
            }
            else
            {
                type_name = data;
            }
            // parse type name
            // 首先解析名字，必然存在
            int index = type_name.LastIndexOf(' ');
            if (index == -1)
                throw new Exception("bad struct field" + line + data);
            field.name = type_name.Substring(index);
            // 判断是否可选，指针
            if (type_name[index - 1] == '*')
            {
                field.pointer = true;
                --index;
            }
            // 获得类型信息 
            string type = type_name.Substring(0, index).Trim();
            if (type[type.Length - 1] != '>')
            {// not container
                field.value.SetName(type);
            }
            else
            {// need check count
                string[] tokens = type_name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                field.container = Field.ParseContainer(tokens[0]);
                if (field.container == Container.NONE || tokens[1] != "<")
                    throw new Exception("bad struct field" + line + data);
                field.value.SetName(tokens[tokens.Length - 3]);
                if (field.container == Container.HASH_MAP || field.container == Container.HASH_SET)
                {
                    field.key.SetName(tokens[2]);
                }
            }
            return field;
        }
    }
}
