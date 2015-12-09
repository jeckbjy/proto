#include "proto.h"
#include <math.h>
#include <iostream>
using namespace std;

struct Person : pt_message
{
	pt_s32 id;
	pt_str name;
	pt_str addr;
	void encode(pt_encoder& stream) const
	{
		stream.beg_tag();
		stream << id << name << addr;
		stream.end_tag();
	}
	void decode(pt_decoder& stream)
	{
		stream.beg_tag();
		stream >> id >> name >> addr;
		stream.end_tag();
	}
};

struct LoginMsg : pt_message
{
	pt_s32 id;
	pt_str txt;
	Person info;
	pt_vec<Person> infos;
	size_t msgid() const { return 1; }
	void encode(pt_encoder& stream) const
	{
		stream.beg_tag();
		stream << id << txt << info << infos;
		stream.end_tag();
	}

	void decode(pt_decoder& stream)
	{
		stream.beg_tag();
		stream >> id >> txt >> info >> infos;
		stream.end_tag();
	}
};

int main(int argc, char* argv[])
{
	Person person;
	person.id = 10;
	person.name = "asrfwerwer";
	person.addr = "qwerqwrwr";
	LoginMsg msg;
	msg.id = 1;
	msg.txt = "asdfasfwrewerasfsaf";
	msg.info = person;
	msg.infos.push_back(person);

	LoginMsg result;
	pt_stream stream;
	pt_encoder writer(&stream);
	writer.encode(msg);

	stream.rewind();
	pt_decoder reader(&stream);
	if (reader.pre_decode())
		reader.decode(result);

	system("pause");
	return 0;
}

