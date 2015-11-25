#pragma once
#include <stddef.h>
#include <cstdint>
#include <vector>
#include <list>
#include <map>
#include <set>
#include <stack>
#include <string>
#include <assert.h>

class pt_stream;
class pt_decoder;
class pt_encoder;

struct pt_message
{
	virtual ~pt_message(){}
	virtual size_t msgid() const { return 0; }
	virtual void decode(pt_decoder& stream) = 0;
	virtual void encode(pt_encoder& stream) const = 0;
};

// 自动释放的ptr,谨慎使用
template<typename T>
struct pt_ptr
{
public:
	pt_ptr() :m_ptr(0){}
	~pt_ptr(){ release(); }

	T* operator->() const { return m_ptr; }
	T& operator*() const { return *m_ptr; }
	pt_ptr& operator=(T* x) { release(); m_ptr = x; return *this; }
	void reset() { m_ptr = 0; }
	void release() 
	{
		if (m_ptr)
		{
			delete m_ptr; 
			m_ptr = 0; 
		}
	}
private:
	T* m_ptr;
};

// 默认初始化为0
template<typename T>
struct pt_num
{
	T data;
	pt_num(const T& x = 0) :data(x){}
	operator T() const { return data; }
	pt_num& operator=(const T& x) { data = x; return *this; }
	pt_num& operator++() { ++data; return *this; }
	pt_num& operator--() { --data; return *this; }
	pt_num& operator+=(T x) { data += x; return *this; }
	pt_num& operator-=(T x) { data -= x; return *this; }
	pt_num& operator*=(T x) { data *= x; return *this; }
	pt_num& operator/=(T x) { assert(x != 0); data /= x; return *this; }


	pt_num operator +(T x) { pt_num copy(*this); data += x; return copy; }
	pt_num operator -(T x) { pt_num copy(*this); data -= x; return copy; }
	pt_num operator *(T x) { pt_num copy(*this); data *= x; return copy; }
	pt_num operator /(T x) { pt_num copy(*this); data /= x; return copy; }

	pt_num operator++(int) { pt_num copy(*this); ++data; return copy; }
	pt_num operator--(int) { pt_num copy(*this); --data; return copy; }
};

// 不使用命名空间proto type
typedef pt_num<bool>				pt_bool;
typedef pt_num<signed char>			pt_s8;
typedef pt_num<short>				pt_s16;
typedef pt_num<int>					pt_s32;
typedef pt_num<long long>			pt_s64;
typedef pt_num<unsigned char>		pt_u8;
typedef pt_num<unsigned short>		pt_u16;
typedef pt_num<unsigned int>		pt_u32;
typedef pt_num<unsigned long long>	pt_u64;
typedef pt_num<float>				pt_f32;
typedef pt_num<double>				pt_f64;
typedef std::string					pt_str;

enum TrimMode
{
	TRIM_LEFT	= 0x01,
	TRIM_RIGHT	= 0x02,
	TRIM_BOTH	= TRIM_LEFT | TRIM_RIGHT,
};

#define SEEK_BEG 4	// 相对于spos,而SEEK_SET是相对于0点

class pt_stream
{
	struct chunk;
public:
	pt_stream(size_t bytes = 1024);
	~pt_stream();

	bool peek(void* data, size_t len) const;
	bool read(void* data, size_t len);
	bool write(const void* data, size_t len);
	bool append(const void* data, size_t len);
	bool advance(size_t len);
	bool reserve(size_t len);
	void seek(long len, int origin = SEEK_BEG);
	void resize(size_t len);
	void discard(bool need_trim = true);
	void concat();
	void trim(TrimMode mode = TRIM_BOTH);
	void release();

	void suspend(size_t& old_epos, size_t len);
	void recovery(size_t pos);

	size_t size() const { return m_epos - m_spos; }
	size_t space() const { return m_epos - m_cpos; }
	size_t cursor() const { return m_cpos; }
	size_t position() const { return m_cpos - m_spos; }
	bool empty() const { return m_spos == m_epos; }
	bool eof() const { return m_cpos >= m_epos; }
	void skip(size_t len) { seek((long)len, SEEK_CUR); }
	void rewind() { seek(0, SEEK_BEG); }

private:
	chunk* alloc(size_t len, char* data = NULL);
	void push_chunk(chunk* block);

private:
	// 双向循环链表
	struct chunk
	{
		chunk*	prev;
		chunk*	next;
		size_t	spos;	// 起始偏移
		size_t	size;	// 此块大小
		char*	data;
		size_t	epos() const { return spos + size; }
	};

	chunk*	m_head;		// 链表头
	chunk*	m_curr;		// 当前指针
	size_t	m_init;		// 初始化大小
	size_t	m_caps;		// 总容量
	// 相对于0点偏移
	size_t	m_spos;		// 数据起始偏移
	size_t	m_epos;		// 数据结尾偏移,仅作标识使用
	size_t	m_cpos;		// 当前位置，读写使用
};

class pt_codec
{
public:
	pt_codec(pt_stream* stream) :m_stream(stream), m_tag(0){}

	void beg_tag() { m_stack.push(m_tag); m_tag = 0; }
	void end_tag() { m_tag = m_stack.top(); m_stack.pop(); }

protected:
	typedef std::vector<uint32_t> IndexVec;
	typedef std::stack<int> Stack;
	pt_stream*	 m_stream;
	size_t	 m_tag;
	Stack	 m_stack;
	IndexVec m_indexs;
};

// 写入
class pt_encoder : public pt_codec
{
	inline static uint64_t encodei64(int64_t n) { return (n << 1) ^ (n >> 63); }
	inline static uint64_t encodef64(double n) { union { double f; uint64_t i; } d; d.f = n; return d.i; }
	inline static uint64_t encodef32(float n) { union { float f; uint32_t i; } d; d.f = n; return d.i; }

	inline static uint64_t convert_to(const bool& n)	 { return n ? 1 : 0; }
	inline static uint64_t convert_to(const uint8_t&  n) { return n; }
	inline static uint64_t convert_to(const uint16_t& n) { return n; }
	inline static uint64_t convert_to(const uint32_t& n) { return n; }
	inline static uint64_t convert_to(const uint64_t& n) { return n; }
	inline static uint64_t convert_to(const int8_t&  n)  { return (uint8_t)n; }
	inline static uint64_t convert_to(const int16_t& n)  { return encodei64(n); }
	inline static uint64_t convert_to(const int32_t& n)  { return encodei64(n); }
	inline static uint64_t convert_to(const int64_t& n)  { return encodei64(n); }
	inline static uint64_t convert_to(const double& n)	 { return encodef64(n); }
	inline static uint64_t convert_to(const float& n)	 { return encodef32(n); }

public:
	pt_encoder(pt_stream* stream) :pt_codec(stream){}

	void encode(pt_message& msg);

	template<typename T>
	pt_encoder& write(const T& data, size_t tag = 1)
	{
		m_tag += tag;
		if (write_field(data))
			m_tag = 0;
		return *this;
	}

	template<typename T>
	pt_encoder& operator <<(const T& data) 
	{
		write(data, 1); 
		return *this;
	}

public:// 辅助函数
	void write_tag(size_t tag, uint64_t val, bool ext);
	void write_beg(size_t& tpos, size_t& bpos);
	void write_end(size_t& tpos, size_t& bpos);
	void write_buf(const char* data, size_t len);
	void write_var(uint64_t data);

public:// for stl,不支持嵌套
	void write_data(const pt_message& msg);
	void write_data(const pt_str& str);
	void write_data(const bool& data)		{ write_var(convert_to(data)); }
	void write_data(const uint8_t& data)	{ write_var(convert_to(data)); }
	void write_data(const uint16_t& data)	{ write_var(convert_to(data)); }
	void write_data(const uint32_t& data)	{ write_var(convert_to(data)); }
	void write_data(const uint64_t& data)	{ write_var(convert_to(data)); }
	void write_data(const int8_t& data)		{ write_var(convert_to(data)); }
	void write_data(const int16_t& data)	{ write_var(convert_to(data)); }
	void write_data(const int32_t& data)	{ write_var(convert_to(data)); }
	void write_data(const int64_t& data)	{ write_var(convert_to(data)); }
	void write_data(const double& data)		{ write_var(convert_to(data)); }
	void write_data(const float& data)		{ write_var(convert_to(data)); }

public:
	bool write_field(const pt_message& data);
	bool write_field(const pt_str& data);
	bool write_field(const bool& data)		{ return write_type(data); }
	bool write_field(const uint8_t& data)	{ return write_type(data); }
	bool write_field(const uint16_t& data)	{ return write_type(data); }
	bool write_field(const uint32_t& data)	{ return write_type(data); }
	bool write_field(const uint64_t& data)	{ return write_type(data); }
	bool write_field(const int8_t& data)	{ return write_type(data); }
	bool write_field(const int16_t& data)	{ return write_type(data); }
	bool write_field(const int32_t& data)	{ return write_type(data); }
	bool write_field(const int64_t& data)	{ return write_type(data); }
	bool write_field(const double& data)	{ return write_type(data); }
	bool write_field(const float& data)		{ return write_type(data); }
	template<typename T> bool write_field(const std::vector<T>& data){ return write_stl(data); }
	template<typename T> bool write_field(const std::list<T>& data)	 { return write_stl(data); }
	template<typename T> bool write_field(const std::set<T>& data)	 { return write_stl(data); }
	template<typename U, typename V> bool write_field(const std::map<U, V>& data) { return write_stl(data); }

	template<typename T> bool write_field(pt_num<T>& num) { return write_field(num.data); }
	template<typename T> bool write_field(pt_ptr<T>& data) { return write_field(*data); }

private:
	template<typename T>
	inline bool write_type(const T& data)
	{
		if (data)
		{
			write_tag(m_tag, convert_to(data), false);
			return true;
		}

		return false;
	}
	// 编码stl
	template<typename STL>
	inline bool write_stl(const STL& data)
	{
		if (data.empty())
			return false;
		size_t tpos, bpos;
		write_beg(tpos, bpos);
		// 写入数据
		typename STL::const_iterator cur_itor;
		typename STL::const_iterator end_itor = data.end();
		for (cur_itor = data.begin(); cur_itor != end_itor; ++cur_itor)
		{
			write_data(*cur_itor);
		}
		write_end(tpos, bpos);
		return true;
	}

	template<typename U, typename V> 
	inline void write_data(const std::pair<U, V>& data)
	{
		write_data(data.first);
		write_data(data.second);
	}
};

// 解码
class pt_decoder : public pt_codec
{
	inline static int64_t	decodei64(uint64_t n) { return (n >> 1) ^ -(int64_t)(n & 1); }
	inline static double	decodef64(uint64_t n) { union { double f; uint64_t i; } d; d.i = n; return d.f; }
	inline static float		decodef32(uint64_t n) { union { float f; uint32_t i; } d; d.i = (uint32_t)n; return d.f; }

	inline static void convert_from(uint64_t n, bool& dst)		{ dst = (n != 0); }
	inline static void convert_from(uint64_t n, uint8_t& dst)	{ dst = (uint8_t)n; }
	inline static void convert_from(uint64_t n, uint16_t& dst)	{ dst = (uint16_t)n; }
	inline static void convert_from(uint64_t n, uint32_t& dst)	{ dst = (uint32_t)n; }
	inline static void convert_from(uint64_t n, uint64_t& dst)	{ dst = (uint64_t)n; }
	inline static void convert_from(uint64_t n, int8_t& dst)	{ dst = (int8_t)(uint8_t)n; }
	inline static void convert_from(uint64_t n, int16_t& dst)	{ dst = (int16_t)decodei64(n); }
	inline static void convert_from(uint64_t n, int32_t& dst)	{ dst = (int32_t)decodei64(n); }
	inline static void convert_from(uint64_t n, int64_t& dst)	{ dst = (int64_t)decodei64(n); }
	inline static void convert_from(uint64_t n, double& dst)	{ dst = decodef64(n); }
	inline static void convert_from(uint64_t n, float& dst)		{ dst = decodef32(n); }

public:
	pt_decoder(pt_stream* stream) :pt_codec(stream){}

	bool decode_head();
	bool decode(pt_message& msg);

	template<typename T>
	pt_decoder& read(T& data, size_t tag = 1)
	{
		if (pre_read(tag))
			read_field(data);
		return *this;
	}

	template<typename T>
	pt_decoder& operator>>(T& data) 
	{
		read(data, 1);
		return *this; 
	}

public:
	uint32_t msgID() const { return m_msgid; }
	bool pre_read(size_t tag);
	bool read_tag();
	bool read_var(uint64_t& data);
	uint32_t pop_index();

public:
	void read_data(pt_message& data);
	void read_data(pt_str& data);

	void read_data(bool& dst)	 { read_basic(dst); }
	void read_data(uint8_t& dst) { read_basic(dst); }
	void read_data(uint16_t& dst){ read_basic(dst); }
	void read_data(uint32_t& dst){ read_basic(dst); }
	void read_data(uint64_t& dst){ read_basic(dst); }
	void read_data(int8_t& dst)  { read_basic(dst); }
	void read_data(int16_t& dst) { read_basic(dst); }
	void read_data(int32_t& dst) { read_basic(dst); }
	void read_data(int64_t& dst) { read_basic(dst); }
	template<typename T>
	void read_data(pt_num<T>& dst) { read_basic(dst.data); }

public:
	void read_field(pt_message& dst);
	void read_field(pt_str& dst);

	void read_field(bool& dst)		{ convert_from(m_data, dst); }
	void read_field(uint8_t& dst)	{ convert_from(m_data, dst); }
	void read_field(uint16_t& dst)	{ convert_from(m_data, dst); }
	void read_field(uint32_t& dst)	{ convert_from(m_data, dst); }
	void read_field(uint64_t& dst)	{ convert_from(m_data, dst); }
	void read_field(int8_t& dst)	{ convert_from(m_data, dst); }
	void read_field(int16_t& dst)	{ convert_from(m_data, dst); }
	void read_field(int32_t& dst)	{ convert_from(m_data, dst); }
	void read_field(int64_t& dst)	{ convert_from(m_data, dst); }
	void read_field(double& dst)	{ convert_from(m_data, dst); }
	void read_field(float& dst)		{ convert_from(m_data, dst); }

	template<typename T> void read_field(std::vector<T>& data)	{ read_stl(data); }
	template<typename T> void read_field(std::list<T>& data)	{ read_stl(data); }
	template<typename T> void read_field(std::set<T>& data)		{ read_stl(data); }
	template<typename U, typename V> void read_field(std::map<U, V>& data) { read_stl(data); }
	template<typename T> void read_field(pt_num<T>& num) { read_field(num.data); }
	template<typename T> void read_field(pt_ptr<T>& data)
	{
		data = new T();
		read_field(*data);
	}
private:
	template<typename T>
	inline void read_basic(T& dst)
	{
		uint64_t data;
		read_var(data);
		convert_from(data, dst);
	}

	template<typename U, typename V>
	inline void read_data(std::pair<const U, V>& kv)
	{
		U* key = const_cast<U*>(&kv.first);
		read_data(*key);
		read_data(kv.second);
	}

	template<typename T>
	inline void push_back(std::vector<T>& stl, typename std::vector<T>::value_type& t)
	{
		stl.push_back(t);
	}
	template<typename T>
	inline void push_back(std::list<T>& stl, typename std::list<T>::value_type& t)
	{
		stl.push_back(t);
	}
	template<typename T>
	inline void push_back(std::set<T>& stl, typename std::set<T>::value_type& t)
	{
		stl.insert(t);
	}
	template<typename U, typename V>
	inline void push_back(std::map<U, V>& stl, typename std::map<U, V>::value_type& t)
	{
		stl.insert(t);
	}

	template<typename STL>
	inline void read_stl(STL& stl)
	{
		if (!m_ext || !m_data)
			return;
		size_t epos;
		m_stream->suspend(epos, (size_t)m_data);
		typename STL::value_type item;
		while (!m_stream->eof())
		{
			read_data(item);
			this->push_back(stl, item);
		}
		m_stream->recovery(epos);
	}

private:
	uint32_t m_length;
	uint32_t m_offset;
	uint32_t m_msgid;
	uint64_t m_data;
	bool	 m_ext;
};
