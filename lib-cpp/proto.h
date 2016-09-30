#pragma once

#if __cplusplus >= 201103L || _MSC_VER >= 1700
#define HAS_CPP11
#endif

#include <stddef.h>
#include <assert.h>
#include <cstdint>
#include <string>
#include <vector>
#include <list>
#include <map>
#include <set>
#include <stack>
#ifdef HAS_CPP11
#include <unordered_map>
#include <unordered_set>
#else
#include <hash_map>
#include <hash_set>
#endif

// stl
#ifdef HAS_CPP11
template<typename T>
using pt_vec = std::vector<T>;

template<typename T>
using pt_list = std::list<T>;

template<typename U, typename V>
using pt_map = std::map<U, V>;

template<typename T>
using pt_set = std::set<T>;

template<typename U, typename V>
using pt_hmap = std::unordered_map<U, V>;

template<typename T>
using pt_hset = std::unordered_set<T>;

template<typename U, typename V>
using pt_pair = std::pair<U, V>;

#else
template<typename T>
class pt_vec : public std::vector<T> {};

template<typename T>
class pt_list : public std::list<T>{};

template<typename U, typename V>
class pt_map : public std::map<U, V>{};

template<typename T>
class pt_set : public std::set<T>{};

template<typename U, typename V>
class pt_hmap : public std::hash_map<U, V>{};

template<typename T>
class pt_hset : public std::hash_set<T>{};

template<typename U, typename V>
class pt_pair : public std::pair<U, V> {};

#endif

// traits 萃取
template<bool _Test, class T = void>
struct pt_enable_if{};

template<class T>
struct pt_enable_if<true, T> { typedef T type; };

template<typename T, T val>
struct pt_constant
{
	static const T value = val;
	operator T() const { return value; }
};

typedef pt_constant<bool, true>		pt_true_type;
typedef pt_constant<bool, false>	pt_false_type;

template<typename T>
struct pt_is_basic : pt_false_type { };
template<> struct pt_is_basic<bool>		: pt_true_type{};
template<> struct pt_is_basic<char>		: pt_true_type{};
template<> struct pt_is_basic<int8_t>	: pt_true_type{};
template<> struct pt_is_basic<int16_t>	: pt_true_type{};
template<> struct pt_is_basic<int32_t>	: pt_true_type{};
template<> struct pt_is_basic<int64_t>	: pt_true_type{};
template<> struct pt_is_basic<uint8_t>	: pt_true_type{};
template<> struct pt_is_basic<uint16_t> : pt_true_type{};
template<> struct pt_is_basic<uint32_t> : pt_true_type{};
template<> struct pt_is_basic<uint64_t> : pt_true_type{};
template<> struct pt_is_basic<float>	: pt_true_type{};
template<> struct pt_is_basic<double>	: pt_true_type{};

template<typename T> 
struct pt_is_stl : pt_false_type { };
template<typename T> struct pt_is_stl<pt_vec<T> > : pt_true_type{};
template<typename T> struct pt_is_stl<pt_list<T> > : pt_true_type{};
template<typename T> struct pt_is_stl<pt_set<T> > : pt_true_type{};
template<typename T> struct pt_is_stl<pt_hset<T> > : pt_true_type{};
template<typename U, typename V> struct pt_is_stl<pt_map<U, V> > : pt_true_type{};
template<typename U, typename V> struct pt_is_stl<pt_hmap<U, V> > : pt_true_type{};

// 自动释放的ptr,谨慎使用
template<typename T>
struct pt_ptr
{
public:
	pt_ptr() :m_ptr(0){}
	~pt_ptr(){ release(); }

	operator bool() const { return m_ptr; }
	bool operator!() const { return !m_ptr; }

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
typedef pt_num<int>					pt_sint;
typedef pt_num<unsigned int>		pt_uint;
typedef std::string					pt_str;

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

struct pt_convert
{
	inline static uint64_t encodei64(int64_t n){ return (n << 1) ^ (n >> 63); }
	inline static uint64_t encodef64(double n) { union { double f; uint64_t i; } d; d.f = n; return d.i; }
	inline static uint64_t encodef32(float n)  { union { float  f; uint32_t i; } d; d.f = n; return d.i; }

	inline static int64_t  decodei64(uint64_t n) { return (n >> 1) ^ -(int64_t)(n & 1); }
	inline static double   decodef64(uint64_t n) { union { double f; uint64_t i; } d; d.i = n; return d.f; }
	inline static float    decodef32(uint64_t n) { union { float  f; uint32_t i; } d; d.i = (uint32_t)n; return d.f; }

	inline static uint64_t encode(const bool& n)	 { return n ? 1 : 0; }
	inline static uint64_t encode(const uint8_t&  n) { return n; }
	inline static uint64_t encode(const uint16_t& n) { return n; }
	inline static uint64_t encode(const uint32_t& n) { return n; }
	inline static uint64_t encode(const uint64_t& n) { return n; }
	inline static uint64_t encode(const int8_t&  n)  { return encodei64(n); }
	inline static uint64_t encode(const int16_t& n)  { return encodei64(n); }
	inline static uint64_t encode(const int32_t& n)  { return encodei64(n); }
	inline static uint64_t encode(const int64_t& n)  { return encodei64(n); }
	inline static uint64_t encode(const double& n)	 { return encodef64(n); }
	inline static uint64_t encode(const float& n)	 { return encodef32(n); }

	inline static void decode(uint64_t n, bool& dst)	{ dst = (n != 0); }
	inline static void decode(uint64_t n, uint8_t& dst)	{ dst = (uint8_t)n; }
	inline static void decode(uint64_t n, uint16_t& dst){ dst = (uint16_t)n; }
	inline static void decode(uint64_t n, uint32_t& dst){ dst = (uint32_t)n; }
	inline static void decode(uint64_t n, uint64_t& dst){ dst = (uint64_t)n; }
	inline static void decode(uint64_t n, int8_t& dst)	{ dst = (int8_t) decodei64(n); }
	inline static void decode(uint64_t n, int16_t& dst)	{ dst = (int16_t)decodei64(n); }
	inline static void decode(uint64_t n, int32_t& dst)	{ dst = (int32_t)decodei64(n); }
	inline static void decode(uint64_t n, int64_t& dst)	{ dst = (int64_t)decodei64(n); }
	inline static void decode(uint64_t n, double& dst)	{ dst = decodef64(n); }
	inline static void decode(uint64_t n, float& dst)	{ dst = decodef32(n); }
};

class pt_codec
{
public:
	pt_codec(pt_stream* stream) :m_stream(stream), m_tag(0){}

	void beg_tag() { m_stack.push_back(m_tag); m_tag = 0; }
	void end_tag() { m_tag = m_stack.back(); m_stack.pop_back(); }

protected:
	typedef std::vector<int> Stack;
	pt_stream* m_stream;
	size_t	 m_tag;
	Stack	 m_stack;
};

// 写入
class pt_encoder : public pt_codec
{
public:
	pt_encoder(pt_stream* stream) :pt_codec(stream){}

	void write_var(uint64_t data);
	void write_buf(const char* data, size_t len);
	void write_beg(size_t& index, size_t tag);
	void write_end(size_t& index, size_t tag);
	void write_tag(size_t tag, uint64_t val, bool ext);

	void encode(pt_message& msg);

public:
	template<typename T>
	pt_encoder& operator <<(const T& data)
	{
		write(data, 1);
		return *this;
	}

	template<typename T>
	pt_encoder& write(const T& data, size_t tag = 1)
	{
		m_tag += tag;
		if (can_write(data))
		{
			write_field(data, m_tag);
			m_tag = 0;
		}
		return *this;
	}

	bool can_write(const pt_message&) { return true; }
	bool can_write(const pt_str& str) { return !str.empty(); }
	template<typename T>
	bool can_write(const pt_ptr<T>& ptr) { return ptr; }
	template<typename T>
	bool can_write(const pt_num<T>& num) { return num != 0; }
	template<typename T>
	typename pt_enable_if<pt_is_basic<T>::value,bool>::type
		can_write(const T& val) { return val; }
	template<typename T>
	typename pt_enable_if<pt_is_stl<T>::value, bool>::type
		can_write(const T& val) { return !val.empty(); }

	void write_field(const pt_message& data, size_t tag);
	void write_field(const pt_str& data, size_t tag);

	template<typename T> 
	void write_field(const pt_ptr<T>& ptr, size_t tag) 
	{
		if (ptr)
			write_field(*ptr, tag);
	}

	template<typename T> 
	void write_field(const pt_num<T>& num, size_t tag) 
	{
		write_field(num.data, tag); 
	}

	template<typename T>
	typename pt_enable_if<pt_is_basic<T>::value>::type
		write_field(const T& data, size_t tag)
	{
		uint64_t tmp = pt_convert::encode(data);
		write_tag(tag, tmp, false);
	}

	template<typename STL>
	typename pt_enable_if<pt_is_stl<STL>::value>::type
		write_field(const STL& data, size_t tag)
	{
		size_t index;
		write_beg(index, tag);
		// 写入数据
		typename STL::const_iterator cur_itor;
		typename STL::const_iterator end_itor = data.end();
		for (cur_itor = data.begin(); cur_itor != end_itor; ++cur_itor)
		{
			write_item(*cur_itor);
		}
		write_end(index, tag);
	}

private:
	template<typename T>
	void write_item(const T& data)
	{
		write_field(data, 1);
	}

	template<typename U, typename V>
	void write_item(const pt_pair<U, V>& kv)
	{
		write_item(kv.first);
		write_item(kv.second);
	}

private:
	struct TagInfo
	{
		size_t tpos;
		size_t bpos;
		size_t leng;
	};
	typedef std::vector<TagInfo> TagInfoVec;
	TagInfoVec m_indexs;
};

// 解码
class pt_decoder : public pt_codec
{
public:
	typedef pt_message* create_cb(unsigned int msgid);
	pt_decoder(pt_stream* stream) :pt_codec(stream){}

	uint32_t msgID() const { return m_msgid; }
	void skip();
	bool pre_read(size_t tag);
	bool read_tag(bool use_tag = true);
	bool read_var(uint64_t& data);

	bool decode(create_cb fun);
	bool decode(pt_message& msg);
	bool pre_decode();

public:
	template<typename T>
	pt_decoder& operator>>(T& data)
	{
		read(data, 1);
		return *this;
	}

	template<typename T>
	pt_decoder& read(T& data, size_t tag = 1)
	{
		if (pre_read(tag))
			read_field(data);
		return *this;
	}

	void read_field(pt_message& dst);
	void read_field(pt_str& dst);

	template<typename T>
	void read_field(pt_ptr<T>& data)
	{
		data = new T();
		read_field(*data);
	}

	template<typename T> 
	void read_field(pt_num<T>& num) 
	{ 
		pt_convert::decode(m_data, num.data);
	}

	template<typename T>
	typename pt_enable_if<pt_is_basic<T>::value>::type
		read_field(T& dst)
	{
		pt_convert::decode(m_data, dst);
	}

	template<typename STL>
	typename pt_enable_if<pt_is_stl<STL>::value>::type
		read_field(STL& stl)
	{
		if (!m_ext || !m_data)
			return;
		size_t epos;
		m_stream->suspend(epos, (size_t)m_size);
		typename STL::value_type item;
		while (!m_stream->eof())
		{
			read_tag(true);
			read_field(item);
			pt_push_back(stl, item);
		}
		m_stream->recovery(epos);
	}

	template<typename U, typename V>
	inline void read_field(pt_pair<const U, V>& kv)
	{
		U* key = const_cast<U*>(&kv.first);
		read_field(*key);
		read_field(kv.second);
	}

private:
	template<typename T>
	inline void pt_push_back(pt_vec<T>& stl, typename pt_vec<T>::value_type& t)
	{
		stl.push_back(t);
	}
	template<typename T>
	inline void pt_push_back(pt_list<T>& stl, typename pt_list<T>::value_type& t)
	{
		stl.push_back(t);
	}
	template<typename T>
	inline void pt_push_back(pt_set<T>& stl, typename pt_set<T>::value_type& t)
	{
		stl.insert(t);
	}
	template<typename T>
	inline void pt_push_back(pt_hset<T>& stl, typename pt_hset<T>::value_type& t)
	{
		stl.insert(t);
	}

	template<typename U, typename V>
	inline void pt_push_back(pt_map<U, V>& stl, typename pt_map<U, V>::value_type& t)
	{
		stl.insert(t);
	}

	template<typename U, typename V>
	inline void pt_push_back(pt_hmap<U, V>& stl, typename pt_hmap<U, V>::value_type& t)
	{
		stl.insert(t);
	}

private:
	typedef std::vector<uint64_t> IndexVec;
	IndexVec m_indexs;
	uint32_t m_msgid;
	uint32_t m_offset;
	bool	 m_ext;
	union
	{
		uint64_t m_size;	// ext:body size
		uint64_t m_data;	// !ext: value
	};
};
