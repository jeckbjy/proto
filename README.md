# lite proto

lite proto相对于protocal buffer而言是一种简单的消息通信协议,使用c#开发，并支持插件扩展,主要工作原理解析proto文件并调用不同语言的插件输出对应语言的代码

- 支持单行注释，以#开始
- 仅支持import，enum，struct三种结构，不支持嵌套定义
- 支持的基础数据有bool，sint8，uint8，sint16，uint16，sint32,uint32,sint64,uint64,float32(float),float64(double)，struct和指针
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
类似tlv编码格式，所有field均使用flag+value的形式进行编码，而对于value仅包含两种格式：变长uint64和legth+content的复杂数据类型

- flag用一个uint8保存：struct中的每个field都会有一个唯一id，id使用增量的方式保存在tag中，所以通常情况下tag都为0或者是一个很小的值，为了压缩tag，则按如下编码flag
	- 最高位标识类型，0：变长uint64，1：len+content类型
	- 高2，3位标识tag，0-2直接保存,3则表示flag后紧跟tag剩余大小
	- 低5位数据信息，var编码，最高位标识为1标识结束，故0-15可直接表示，剩余数据：如果是基本类型则紧随，如何是复杂类型，则长度在indexs中存储

- 基础格式编码转换
	- sint8先转成uint8去除符号再转成uint64
	- bool,uint8,uint16,uint32,uint64直接强转成uint64
	- sint16,sint32,sint64会使用zigzag编码转化成uint64
	- float32内存中强转成uint32,c中实现 union { float  f; uint32_t i; }
	- float64内存中强转成uint64,c中实现 union { double f; uint64_t i; }
- string，blob等已知长度复杂类型：使用length+content编码
- stl内部数据序列化：
	- 自身使用的length+content的形式
	- 内部序列化有两种方式,目前使用的方式1
		1. 和外部的field编码一样，tag使用0表示：缺点是会浪费3个无效bit，因为tag和类型是已知的,优点是无需另实现序列化函数
		2. 去除flag信息，无数据浪费，但缺点是需要重新写一套序列化函数
- struct(内部struct)序列化：由于事先不知length故flag中不能容纳时需要放到索引区去获得大小
- packet（顶层struct）序列化:head+body
	1. head:flag(3-3-2)+msg_len+idx_len+msgid,flag占用1个字节标识后边每个数据所占用字节数，msg_len,idx_len,msgid使用小端编码
	2. body:content+index，index由于序列化之前并不知道，故必须放到末尾
# 计划修改
为了压缩tag信息，目前编码方式是：flag中高位标识tag信息，低位标识data数据，这样导致了api在的复杂，在将来的开发中可能会改为tag和data完全分离，tag信息合并压缩