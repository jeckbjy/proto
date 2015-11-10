# lite proto

lite proto相对于protocal buffer而言是一种简单的消息通信协议,使用c#开发，并支持插件扩展,主要工作原理解析proto文件并调用不同语言的插件输出对应语言的代码

- 支持单行注释，以#开始
- 仅支持import，enum，struct三种结构，不支持嵌套定义
- 支持的基础数据有bool，int8，uint8，int16，uint16，int32,uint32,int64,uint64,float32(float),float64(double)，struct和指针
- 支持的容器vector，list,map,set
- 底层序列化仅包含两种数据结构，变长uint64和struct，所有的基础类型都会转化成变长uint64进行传输 
- 支持版本升级，支持废弃删除字段
- 限制:所有enum中的field名字都不能重复，struct如果带有ID则必须唯一

# example:
<pre><code>
file:test1.proto

enum MsgID
{
	S2C_Login = 1,
	S2C_Logout,
	
	C2S_Login = 5,
	C2S_Logout,
}

file:test.proto
#this is a test proto
import "test1.proto"

enum Mode
{
	MODE_BUY = 1,
	MODE_SELL,
}

struct PhoneNumber
{
	string number;
	int8 type;
}

struct Person
{
	string name;	# person's name
	int32  id;
	string email;
}

struct AddressBook
{
	vector<Person> persons;
}

#this is message
struct LoginMsg = S2C_Login
{
	AddressBook address;
	int8 		idata = delete;
	uint8 		udata = 10;
	vector<int> vdata;
	map<int,string> mdata;
}
</code></pre>

# 消息序列化与反序列化
序列化所有field均使用flag+value的形式进行编码，而对于value仅包含两种格式：变长uint64和legth+content的复杂数据类型

- 基础格式编码转换
	- int8先转成uint8去除符号再转成uint64
	- bool,uint8,uint16,uint32,uint64直接强转成uint64
	- int16,int32,int64会使用zigzag编码转化成uint64
	- float32内存中强转成uint32,c中实现 union { float  f; uint32_t i; }
	- float64内存中强转成uint64,c中实现 union { double f; uint64_t i; }
- 复杂类型：内嵌struct，string，stl等：这些数据都会序列化成length+content的形式
- stl内部数据序列化：对于内部数据，并不需要tag信息，所以直接是变长uint64或者length+content形式
- 顶层struct序列化：由于序列化过程是流式进行的，在序列化内部struct时，事先并不知道struct占用长度，为了避免内存的再次拷贝，这里使用了两种策略，对于小于30个字节的struct可以直接存储在tag中，而大于等于30的则会放到一个队列中，统一放到数据尾部作为索引
- flag用一个uint8保存：每个field都会有一个唯一id，id使用增量的方式保存在tag中，所以通常情况下tag都为0或者是一个很小的值，为了压缩tag，则按如下编码flag
	- 最高位标识类型，0：变长uint64，1：len+content类型
	- 高2，3位标识tag，0-2直接保存,3则表示flag后紧跟tag剩余大小
	- 低5位标识数据信息,可以是基础结构数值，也可以是复杂结构的length，可表示范围0-29，30表示tag后紧随剩余数据，31只在复杂类型时表示长度是存储在外部的(即尾部索引处获得)
