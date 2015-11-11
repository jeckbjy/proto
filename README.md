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

- flag用一个uint8保存：struct中的每个field都会有一个唯一id，id使用增量的方式保存在tag中，所以通常情况下tag都为0或者是一个很小的值，为了压缩tag，则按如下编码flag
	- 最高位标识类型，0：变长uint64，1：len+content类型
	- 高2，3位标识tag，0-2直接保存,3则表示flag后紧跟tag剩余大小
	- 低5位标识数据信息,可以是基础结构数值，也可以是复杂结构的length，可表示范围0-29，30表示tag后紧随剩余数据，31表示长度是存储在外部的(仅嵌套的struct使用)

- 基础格式编码转换
	- int8先转成uint8去除符号再转成uint64
	- bool,uint8,uint16,uint32,uint64直接强转成uint64
	- int16,int32,int64会使用zigzag编码转化成uint64
	- float32内存中强转成uint32,c中实现 union { float  f; uint32_t i; }
	- float64内存中强转成uint64,c中实现 union { double f; uint64_t i; }
- 已知长度复杂类型：如string，使用length+content编码，且length紧随flag
- stl内部数据序列化：
	- 自身使用的length+content的形式
	- 内部序列化有两种方式,但无论哪种方式如果是struct类型都要带有length信息才能实现版本的升级，但由于不能预知长度，依然需要放入顶层struct的index列表中
		1. flag+value方式，tag使用0：缺点是会浪费3个无效bit，因为tag和类型是已知的,优点是无需另实现序列化函数
		2. 去除flag信息，无数据浪费，但缺点是需要重新写一套序列化函数
- struct序列化：分为顶层和内部两种情况,无论哪种预先都无法知道长度，故需要额外地方存储length
	1. 内部struct：类似string等复杂数据使用length+content形式，但区别是flag只占一个字节，大于等于30的length将会放在尾部的index索引中查询
	2. 顶层struct：相当于一个packet：包含flag+length+offset+msgid+content信息,flag占1byte，标识头信息所占字节,length,offset,msgid同flag使用类似group variant 方式编码，length标识不含头部的长度含index信息，offset：标明index的偏移，msgid一个附加信息，通常用于反序列化

