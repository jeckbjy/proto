# lite proto

lite proto相对于protocal buffer而言是一种简单的消息通信协议,使用c#开发，并支持插件扩展

- 支持单行注释
- 仅支持import，enum，struct三种结构，不支持嵌套定义
- 支持的基础数据有bool，int8，uint8，int16，uint16，int32,uint32,int64,uint64,float,double，struct和struct指针
- 支持的容器vector，list,map,set
- 底层序列化仅包含两种数据结构，variant uint64和struct，所有的基础类型都会转化成variant int64进行传输 
- 限制:所有enum中的field名字都不能重复，struct如果带有ID则必须唯一

# example:
<pre><code>
#this is a test proto
import "test1.proto"

enum Mode
{
	MODE_BUY = 1,
	MODE_SELL,
}

enum MsgID
{
	S2C_Login = 1,
	S2C_Logout,
	
	C2S_Login = 5,
	C2S_Logout,
}

struct Person
{
	string name;
}

#this is message
struct LoginMsg = S2C_Login
{
	Person person;		#this is father
	Person* child;
	int8 ivalue;
	uint8 uvalue = 10;
	vector<int> vec;
	map<int,string> map_value;
}
</code></pre>

# todo：消息序列化格式