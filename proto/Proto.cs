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
        LIST,
        VECT,
        HMAP,   // hash_map
        HSET,   // hash_set
        MAP,
        SET,
    }

    public static class Util
    {
        public static FieldType GetValueType(string str)
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
                case "string":  return FieldType.STRING;
                case "blob":    return FieldType.BLOB;
                default:        return FieldType.STRUCT;
            }
        }

        public static Container GetContainerType(string str)
        {
            switch (str)
            {
                case "list":    return Container.LIST;
                case "vect":    return Container.VECT;
                case "vector":  return Container.VECT;
                case "map":     return Container.MAP;
                case "set":     return Container.SET;
                case "hmap":    return Container.HMAP;
                case "hset":    return Container.HSET;
                case "hash_map":return Container.HMAP;
                case "hash_set":return Container.HSET;
                default:        return Container.NONE;
            }
        }
    }

    public class TypeInfo
    {
        public string name;
        public FieldType type;

        public bool IsStruct
        {
            get { return type == FieldType.STRUCT; }
        }

        public void SetName(string name)
        {
            this.name = name;
            this.type = Util.GetValueType(name);
        }
    }

    public class Field
    {
        public string name;
        public int index = Int32.MaxValue;
    }

    public class Scope
    {
        public string name;
        public virtual void Process() { }
    }

    //
    public class EnumScope : Scope
    {
        public List<Field> fields = new List<Field>();
        public int maxWidth = 0;

        public bool Empty
        {
            get { return fields.Count == 0; }
        }

        public void AddField(Field field)
        {
            this.fields.Add(field);
        }

        public override void Process()
        {
            if (fields.Count == 0)
                return;

            maxWidth = 0;
            // 默认从1开始,而且如果是struct，index必须大于0
            if (fields[0].index == Int32.MaxValue)
                fields[0].index = 1;
            // 自动累加索引
            for (int i = 1; i < fields.Count; ++i)
            {
                if (fields[i].name.Length > maxWidth)
                    maxWidth = fields[i].name.Length;

                if (fields[i].index == Int32.MaxValue)
                    fields[i].index = fields[i - 1].index + 1;
            }
        }
    }

    // 结构体类型
    public class StructField : Field
    {
        public uint tag = 0;            // index偏移

        public Container container = Container.NONE;
        public TypeInfo key;
        public TypeInfo value;
        public bool pointer = false;    // 是否是指针
        public bool deprecated = false; // 是否废弃
    }

    public class StructScope : Scope
    {
        public string id_name;   // 唯一id，可以是数字，或者是unique enum
        public string id_owner;

        public List<StructField> fields = new List<StructField>();

        public bool HasID
        {
            get { return !string.IsNullOrEmpty(id_name); }
        }

        public string GetMsgID(string split)
        {
            return String.Format("{0}{1}{2}", id_owner, split, id_name);
        }

        public string GetID(string interval)
        {
            if (HasID)
            {
                if (string.IsNullOrEmpty(id_owner))
                    return id_name;
                else
                    return id_owner + interval + id_name;
            }
            else
            {
                return string.Empty;
            }
        }

        public void AddField(StructField field)
        {
            fields.Add(field);
        }

        public override void Process()
        {
            if (fields.Count == 0)
                return;
            // 默认从1开始,而且如果是struct，index必须大于0
            if (fields[0].index == Int32.MaxValue)
                fields[0].index = 1;
            // 自动累加索引
            for (int i = 1; i < fields.Count; ++i)
            {
                if (fields[i].index == Int32.MaxValue)
                    fields[i].index = fields[i - 1].index + 1;
            }

            // 保证递增性
            for (int i = 1; i < fields.Count; ++i)
            {
                if (fields[i].index <= fields[i - 1].index)
                    throw new Exception("struct index must be increase");
            }
            // 计算tag,即偏移
            StructField field;
            int index = 0;
            for (int i = 0; i < fields.Count; ++i)
            {
                field = fields[i];
                if (field.deprecated)
                    continue;
                field.tag = (uint)(field.index - index);
                index = field.index;
                if (field.tag == 0)
                    throw new Exception("bad struct tag");
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
     *      hmap<int, Person> cc;
     * }
     */
    public class Proto
    {
        DateTime m_lastTime;        // 上次访问时间
        string m_name;              // 不含路径
        string m_path;              // 文件路径
        bool m_hasPacket = false;   // 是否含有packet判断标准是id不为空
        List<String> m_imports = new List<string>();       // 注意不包含proto后缀
        List<Scope>  m_scopes = new List<Scope>();

        public DateTime LastWriteTime
        {
            get { return m_lastTime; }
        }

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
        public List<Scope>  Scopes { get { return m_scopes; } }

        public Proto(string path)
        {
            this.m_path = path;
            this.m_name = Path.GetFileNameWithoutExtension(path);
            FileInfo fi = new FileInfo(m_path);
            m_lastTime = fi.LastWriteTime;
        }

        public void AddImport(string import)
        {
            m_imports.Add(import);
        }

        public void AddScope(Scope scope)
        {
            scope.Process();
            m_scopes.Add(scope);
            if(scope is StructScope)
            {
                StructScope structScope = scope as StructScope;
                if (structScope.HasID)
                    m_hasPacket = true;
            }
        }
    }
}
