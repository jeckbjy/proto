#include "CodeStream.h"
#include <math.h>
#include <iostream>
using namespace std;

struct Address : public pt_message
{
	pt_s16 id;
	pt_str	name;
	Address() :id(0){}
	void encode(pt_encoder& stream) const
	{
		stream.beg_tag();
		stream << id << name;
		//stream.write(id).write(name).write(ids);
		stream.end_tag();
	}

	void decode(pt_decoder& stream)
	{
		stream.beg_tag();
		stream >> id >> name;
		//stream.read(id).read(name).read(ids);
		stream.end_tag();
	}
};

struct Person : public pt_message
{
	string	name;
	vector<Address> addrs;
	map<pt_s16, Address> addrs_map;
	
	void encode(pt_encoder& stream) const
	{
		stream.beg_tag();
		stream << name << addrs << addrs_map;
		stream.end_tag();
	}

	void decode(pt_decoder& stream)
	{
		stream.beg_tag();
		stream >> name >> addrs >> addrs_map;
		stream.end_tag();
	}
};

int main(int argc, char* argv[])
{
	Address addr;
	Person p1, p2;
	p1.name = "jack";
	addr.id = 1;
	addr.name = "tom";
	p1.addrs.push_back(addr);
	p1.addrs_map[addr.id] = addr;
	addr.id = 2;
	addr.name = "eileen";
	p1.addrs.push_back(addr);
	p1.addrs_map[addr.id] = addr;

	pt_stream stream;
	pt_encoder writer(&stream);
	writer.encode(p1);

	stream.rewind();
	pt_decoder reader(&stream);
	if (reader.decode_head())
		reader.decode(p2);

	system("pause");
	return 0;
}

