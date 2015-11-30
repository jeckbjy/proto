using System;
using System.Collections.Generic;
using System.IO;

namespace proto
{
    public enum FieldType
    {
        NONE,
        BOOL,
        SINT,
        UINT,
        SINT8,
        UINT8,
        SINT16,
        UINT16,
        SINT32,
        UINT32,
        SINT64,
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
                case "bool":    return FieldType.BOOL;
                case "int":     return FieldType.SINT;
                case "uint":    return FieldType.UINT;
                case "int8":    return FieldType.SINT8;
                case "int16":   return FieldType.SINT16;
                case "int32":   return FieldType.SINT32;
                case "int64":   return FieldType.SINT64;
                case "sint8":   return FieldType.SINT8;
                case "sint16":  return FieldType.SINT16;
                case "sint32":  return FieldType.SINT32;
                case "sint64":  return FieldType.SINT64;
                case "uint8":   return FieldType.UINT8;
                case "uint16":  return FieldType.UINT16;
                case "uint32":  return FieldType.UINT32;
                case "uint64":  return FieldType.UINT64;
                case "float":   return FieldType.FLOAT32;
                case "float32": return FieldType.FLOAT32;
                case "double":  return FieldType.FLOAT64;
                case "float64": return FieldType.FLOAT64;
                case "string": return FieldType.STRING;
                case "blob": return FieldType.BLOB;
                default: return FieldType.STRUCT;
            }
        }
    }

    public class Field
    {
        public string name;
        public uint index = UInt32.MaxValue;    // 唯一索引
        // struct info
        public uint tag = 0;
        public TypeInfo key;
        public TypeInfo value;
        public Container container = Container.NONE;
        public bool pointer = false;    // 是否是指针,c++中构造析构
        public bool deprecated = false; // 是否废弃

        internal int tokenSize
        {
            get
            {
                switch(container)
                {
                    case Container.NONE: return 2;
                    case Container.MAP:
                    case Container.HASH_MAP:
                        return 4;
                    default:
                        return 3;
                }
            }
        }

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

        public bool HasID
        {
            get { return !string.IsNullOrEmpty(id_name); }
        }

        internal void process()
        {
            if (fields.Count == 0)
                return;
            // 默认从1开始,而且如果是struct，index必须大于0
            if (fields[0].index == UInt32.MaxValue)
                fields[0].index = 1;
            // 自动累加索引
            for (int i = 1; i < fields.Count; ++i)
            {
                if (fields[i].index == UInt32.MaxValue)
                    fields[i].index = fields[i - 1].index + 1;
            }
            // 保证递增性
            if (type == CmdType.STRUCT)
            {
                if (fields[0].index == 0)
                    throw new Exception("struct index must start from 1,cannot equal 0");
                for (int i = 1; i < fields.Count; ++i)
                {
                    if (fields[i].index <= fields[i - 1].index)
                        throw new Exception("struct index must be increase");
                }
            }
            // 计算tag,即偏移
            Field field;
            uint index = 0;
            for(int i = 0; i < fields.Count; ++i)
            {
                field = fields[i];
                if (field.deprecated)
                    continue;
                field.tag = field.index - index;
                index = field.index;
                if (field.tag == 0)
                    throw new Exception("bad tag");
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
     *      delete vector<int> aa;
     *      map<int,string> bb;
     *      hash_map<int, Person> cc;
     * }
     */
    public class Proto
    {
        string m_name;      // 不含路径
        string m_path;      // 文件路径
        bool m_hasPacket;   // 是否含有packet判断标准是id不为空
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

        public bool HasPacket
        {
            get { return m_hasPacket; }
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
                Process();
            }
        }

        private void Process()
        {
            m_hasPacket = false;
            foreach(var msg in m_messages)
            {
                if(msg.HasID)
                {
                    m_hasPacket = true;
                    break;
                }
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
            string[] tokens;
            // (deprecated) (container)type name (= index)
            string type_name;
            if (data.IndexOf('=') != -1)
            {
                tokens = data.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                field.index = UInt32.Parse(tokens[1]);
                type_name = tokens[0].Trim();
            }
            else
            {
                type_name = data;
            }
            // check deprecated
            if(type_name.StartsWith("deprecated "))
            {
                field.deprecated = true;
                type_name = type_name.Substring("deprecated ".Length);
            }
            // parse type name
            int index = type_name.LastIndexOf('*');
            if (index != -1)
                field.pointer = true;
            // 解析类型和name,最后一个必然是name
            tokens = type_name.Split(new char[] { ' ', '<', ',', '>', '*', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                throw new Exception("bad field");
            field.container = Field.ParseContainer(tokens[0]);
            if(tokens.Length != field.tokenSize)
                throw new Exception("bad field");
            field.name = tokens[tokens.Length - 1];
            field.value.SetName(tokens[tokens.Length - 2]);
            if(field.container == Container.MAP || field.container == Container.HASH_MAP)
            {
                field.key.SetName(tokens[1]);
            }

            return field;
        }
    }
}
