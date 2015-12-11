#include "proto.h"
#include <algorithm>

// ����
#define MASK_TYPE	0x80
#define MASK_TAGS	0x60
#define MASK_7BIT	0x10		// 7bit�����ʶ
#define MASK_DATA	0x1F

// �����ֽ���,ʹ��С��
static uint8_t encode_group_var(uint8_t* buff, uint32_t data)
{
	if (data == 0)
		return 0;
	uint8_t count = 0;
	while (data > 0)
	{
		buff[count++] = (uint8_t)data;
		data >>= 8;
	}
	return count;
}

static bool decode_group_var(uint8_t* buff, uint32_t& data, uint8_t len)
{
	if (len > 4)
		return false;
	data = 0;
	uint8_t off = 0;
	for (uint8_t i = 0; i < len; ++i)
	{
		data |= (buff[i] << off);
		off += 8;
	}

	return true;
}

pt_stream::pt_stream(size_t bytes)
	: m_head(NULL)
	, m_curr(NULL)
	, m_init(bytes)
	, m_caps(0)
	, m_spos(0)
	, m_epos(0)
	, m_cpos(0)
{

}

pt_stream::~pt_stream()
{
	// �ͷ������ڴ�
	release();
}

bool pt_stream::peek(void* data, size_t len) const
{
	if (m_cpos + len > m_epos)
		return false;
	char* ptr = (char*)data;
	chunk* node = m_curr;
	size_t offset = m_cpos - m_curr->spos;
	size_t count;
	do 
	{
		count = node->size - offset;
		if (count > len)
			count = len;
		memcpy(ptr, node->data + offset, count);
		len -= count;

		offset = 0;
		node = node->next;
	} while (node && len > 0);
	return true;
}

bool pt_stream::read(void* data, size_t len)
{
	if (peek(data, len))
	{
		m_cpos += len;
		return true;
	}
	return false;
}

bool pt_stream::write(const void* data, size_t len)
{
	if (!reserve(m_cpos + len))
		return false;
	size_t epos = m_cpos + len;

	if (data)
	{
		const char* ptr = (const char*)data;
		chunk* node = m_curr;
		size_t off = m_cpos - node->spos;
		size_t count;
		do
		{
			count = node->size - off;
			if (count > len)
				count = len;
			memcpy(node->data + off, ptr, count);
			len -= count;
			off = 0;
			node = node->next;
		} while (node && len > 0);
	}

	m_cpos = epos;
	if (m_cpos > m_epos)
		m_epos = m_cpos;

	return true;
}

bool pt_stream::append(const void* data, size_t len)
{
	// ��ĩβ���
	if (m_cpos != m_epos)
		seek(m_epos, SEEK_SET);
	return write(data, len);
}

bool pt_stream::advance(size_t len)
{
	if (!reserve(m_cpos + len))
		return false;
	m_cpos += len;
	if (m_cpos > m_epos)
		m_epos = m_cpos;
	return true;
}

bool pt_stream::reserve(size_t len)
{
	if (len <= m_caps || len == 0)
		return true;
	// ���㣬��Ҫ����
	size_t alloc_size = (std::max)(len - m_caps, m_init);
	chunk* block = alloc(alloc_size);
	if (!block)
		return NULL;
	push_chunk(block);
	return true;
}

void pt_stream::seek(long len, int origin)
{
	assert(m_head != NULL);
	// �����0λ��
	size_t cpos;
	switch (origin)
	{
	case SEEK_SET: cpos = len; break;
	case SEEK_BEG: cpos = m_spos + len; break;
	case SEEK_CUR: cpos = m_cpos + len; break;
	case SEEK_END: cpos = m_epos - len; break;
	default:assert(false);
	}
	assert(cpos >= m_spos && cpos <= m_epos);
	if (cpos == m_cpos || cpos < m_spos || cpos > m_epos)
		return;
	if (cpos < m_head->size)
	{
		m_curr = m_head;
	}
	else if (cpos >= m_head->prev->spos)
	{
		m_curr = m_head->prev;
	}
	else if (cpos >= m_cpos)
	{
		while (cpos >= m_curr->epos())
			m_curr = m_curr->next;
	}
	else
	{
		while (cpos < m_curr->spos)
			m_curr = m_curr->prev;
	}

	m_cpos = cpos;
}

void pt_stream::resize(size_t len)
{
	reserve(m_spos + len);
	m_epos = m_spos + len;
	if (m_cpos > m_epos)
	{
		seek(0, SEEK_END);
	}
}

void pt_stream::discard(bool need_trim)
{
	if (m_spos == m_cpos)
		return;
	m_spos = m_cpos;
	if (need_trim)
		trim(TRIM_LEFT);
}

void pt_stream::concat()
{
	// <2��ʱֱ�ӷ���
	if (empty() || m_head->prev == m_head)
		return;
	// �ϲ����ͷ�
	seek(0, SEEK_SET);
	size_t len = size();
	chunk* block = alloc(len);
	read(block->data, len);
	release();
	push_chunk(block);
}

void pt_stream::trim(TrimMode mode)
{
	// ֻ��һ��ʱ���ô���,�������ٻ�ʣ��һ��
	if (!m_head || m_head->prev == m_head)
		return;
	chunk* tail = m_head->prev;
	chunk* node;
	chunk* temp;
	size_t nums;
	// ��Ҫ�������ұߣ���Ϊ����Ҫ�ؽ�����
	if ((mode & TRIM_RIGHT) && (m_epos < tail->spos))
	{// �����ұ�,��ǰ����
		nums = 0;
		node = tail;
		while (m_epos < node->spos)
		{
			nums += node->size;
			temp = node;
			node = node->prev;
			free(temp);
		}
		// ������β
		tail = node;
		tail->next = m_head;
		m_head->prev = tail;
		m_caps -= nums;
	}

	if ( (mode & TRIM_LEFT) && (m_spos >= m_head->epos()) )
	{// �������,������
		node = m_head;
		while (m_spos >= node->epos())
		{
			nums += node->size;
			temp = node;
			node = node->next;
			// �ͷ�
			free(temp);
		}
		// ��������
		node->prev = tail;
		tail->next = node;
		m_head = node;
		m_caps -= nums;
		if (m_spos == m_epos)
		{// ������ȫ��һ�����
			m_spos = m_epos = m_cpos = 0;
		}
		else
		{
			m_spos -= nums;
			m_epos -= nums;
			m_cpos -= nums;
		}
		// ���¼���ƫ��
		nums = 0;
		node = m_head;
		for (;;)
		{
			node->spos = nums;
			nums += node->size;
			node = node->next;
			if (node == m_head)
				break;
		}
	}
}

void pt_stream::release()
{
	if (m_head)
	{
		chunk* next;
		chunk* node = m_head;
		node->prev->next = NULL;
		while (node)
		{
			next = node->next;
			free(node);
			node = next;
		}
		m_head = NULL;
		m_curr = NULL;
		m_caps = m_spos = m_epos = m_cpos = 0;
	}
}

void pt_stream::suspend(size_t& old_epos, size_t len)
{
	old_epos = m_epos;
	size_t epos = m_cpos + len;
	assert(epos <= m_epos);
	if (epos < m_epos)
		m_epos = epos;
}

void pt_stream::recovery(size_t epos)
{
	assert(m_epos >= m_cpos && epos <= m_caps);
	m_epos = epos;
}

pt_stream::chunk* pt_stream::alloc(size_t len, char* data)
{
	size_t alloc_size = data ? sizeof(chunk) : sizeof(chunk) + len;
	char* buff = (char*)malloc(alloc_size);
	if (!buff)
		return 0;
	chunk* block = (chunk*)buff;
	block->size = len;
	block->data = data ? data : buff + sizeof(chunk);
	return block;
}

void pt_stream::push_chunk(chunk* block)
{
	assert(block->size != 0 && block->data != 0);
	if (m_head == NULL)
	{// ������
		m_head = block;
		block->spos = 0;
		block->prev = block->next = block;
		m_curr = m_head;
	}
	else
	{// ����
		chunk* tail = m_head->prev;
		tail->next = block;
		block->prev = tail;
		block->next = m_head;
		m_head->prev = block;
		block->spos = tail->spos + tail->size;
	}
	m_caps += block->size;
}

//////////////////////////////////////////////////////////////////////////
// pt_encoder
//////////////////////////////////////////////////////////////////////////
void pt_encoder::encode(pt_message& msg)
{
	// clear
	m_tag = 0;
	m_stack.clear();
	m_indexs.clear();
	// flag:3-3-2
	// flag(1)+data(4)+index(4)+msgid(4)
	uint8_t	 flag;
	uint32_t bpos, ipos, epos;
	// Ԥ���ռ�
	m_stream->advance(20);
	bpos = m_stream->cursor();
	msg.encode(*this);
	ipos = m_stream->cursor();
	// д������
	if (m_indexs.size() > 0)
	{// �Ӻ���ǰд��
		for (TagInfoVec::reverse_iterator itor = m_indexs.rbegin(); itor != m_indexs.rend(); ++itor)
		{
			if (itor->leng > 0)
				write_var(itor->leng);
		}
	}
	epos = m_stream->cursor();
	// ���л�ͷ��
	uint8_t buff[20];
	uint8_t len1, len2, len3;
	uint8_t len = 1;
	len1 = encode_group_var(buff + len, epos - bpos);
	len += len1;
	len2 = encode_group_var(buff + len, epos - ipos);
	len += len2;
	len3 = encode_group_var(buff + len, msg.msgid());
	len += len3;
	flag = (len1 << 5) + (len2 << 2) + len3;
	buff[0] = flag;
	size_t start_pos = bpos - len;
	m_stream->seek(start_pos, SEEK_SET);
	m_stream->discard(false);
	m_stream->write(buff, len);
	m_stream->seek(start_pos, SEEK_SET);
}

void pt_encoder::write_tag(size_t tag, uint64_t val, bool ext)
{
	// �������:ע�⣺������ʹ�üӼ����������Ż�С����
	// ���λ��ʶ���ͣ�0��uint64��1������length�ĸ�������
	// 2��3λ��ʶtag,0-2,3��ʶ��߶�ȡ,tag����Ϊ1���ʿ����ȼ�1�ٱ���
	// 0:��ʾû�������ˣ�1��ʾ�������ݣ���5λ��ʶ���ݣ�0-15ֱ�Ӵ洢,ʣ�����ݽ�����߷ŵ�index��
	assert(tag > 0);
	tag -= 1;
	char flag = ext ? MASK_TYPE : 0;
	// tag:ռ2bit
	if (tag < 3)
	{
		flag |= (char)tag << 5;
		tag = 0;
	}
	else
	{
		flag |= MASK_TAGS;
		tag -= 2;
	}
	// val:�洢���ݵĵ�4λ
	flag |= (val & 0x0F);
	val >>= 4;
	if (val > 0)
		flag |= 0x10;
	// flag+tag+val
	m_stream->write(&flag, 1);
	if (tag > 0)
		write_var(tag);
	if (!ext && val > 0)
		write_var(val);
}

void pt_encoder::beg_write(size_t& index, size_t tag)
{
	index = m_indexs.size();
	TagInfo info;
	info.tpos = m_stream->cursor();
	write_tag(tag, 0, true);
	info.bpos = m_stream->cursor();
	info.leng = 0;
	m_indexs.push_back(info);
}

void pt_encoder::end_write(size_t& index, size_t tag)
{
	TagInfo& info = m_indexs[index];
	size_t epos = m_stream->cursor();
	size_t leng = epos - info.bpos;
	char flag;
	m_stream->seek(info.tpos, SEEK_SET);
	m_stream->peek(&flag, 1);
	flag |= (leng & 0x0F);
	leng >>= 4;
	if (leng > 0)
	{
		flag |= 0x10;
		info.leng = leng;
	}
	m_stream->write(&flag, 1);
	m_stream->seek(epos, SEEK_SET);
}

void pt_encoder::write_var(uint64_t value)
{
	//��λ��ʶ��0��ʾ��β,1��ʾ��߻�������
	char buff[20];
	size_t count = 0;
	while (value > 0x7F)
	{
		buff[count++] = ((uint8_t)(value)& 0x7F) | 0x80;
		value >>= 7;
	}
	buff[count++] = value & 0x7F;
	m_stream->write(buff, count);
}

void pt_encoder::write_buf(const char* data, size_t len)
{
	write_var(len);
	if (len > 0)
		m_stream->write(data, len);
}

bool pt_encoder::write_field(const pt_message& data, size_t tag)
{
	size_t index;
	beg_write(index, tag);
	beg_tag();
	data.encode(*this);
	end_tag();
	end_write(index, tag);
	return true;
}

bool pt_encoder::write_field(const pt_str& data, size_t tag)
{
	if (data.empty())
		return false;
	size_t index;
	beg_write(index, tag);
	m_stream->write(data.data(), data.size());
	end_write(index, tag);
	return true;
}

//////////////////////////////////////////////////////////////////////////
// pt_decoder
//////////////////////////////////////////////////////////////////////////
bool pt_decoder::decode(create_cb fun)
{
	if (pre_decode())
	{
		pt_message* msg = fun(m_msgid);
		if (!msg)
		{// ����
			skip();
			return false;
		}
		return decode(*msg);
	}
	return false;
}
bool pt_decoder::decode(pt_message& msg)
{
	// ��clear
	m_msgid = 0;
	m_tag = 0;
	m_stack.clear();
	m_indexs.clear();
	// ��ʼ����
	size_t msg_len = (size_t)m_size;
	// ͷ���Ѿ�������
	size_t beg_pos = m_stream->cursor();
	// �ȶ�ȡindex
	if (m_offset > 0)
	{
		uint64_t index;
		size_t end_pos = beg_pos + msg_len;
		m_indexs.reserve((msg_len - m_offset) >> 2);
		m_stream->seek(m_offset, SEEK_CUR);
		while (m_stream->cursor() < end_pos)
		{
			if (!read_var(index))
				return false;
			m_indexs.push_back((size_t)index);
		}
		m_stream->seek(beg_pos, SEEK_SET);
	}

	if (m_data > 0)
	{
		size_t old_epos;
		m_stream->suspend(old_epos, msg_len);
		msg.decode(*this);
		m_stream->recovery(old_epos);
	}
	// �ƶ�����ȷλ��
	m_stream->seek((long)(beg_pos + m_size), SEEK_SET);
	return true;
}

bool pt_decoder::pre_decode()
{
	char flag;
	uint32_t msg_len, idx_len, msg_id;
	size_t pos = m_stream->cursor();
	if (!m_stream->read(&flag, 1))
		return false;
	uint8_t buff[20];
	uint8_t len1, len2, len3, len;
	len1 = (flag >> 5);
	len2 = (flag >> 2) & 0x07;
	len3 = flag & 0x03;
	len = len1 + len2 + len3;
	if (!m_stream->read(buff, len))
	{
		m_stream->seek(pos, SEEK_SET);
		return false;
	}
	// ��������,data_len:index_len:msgid
	decode_group_var(buff, msg_len, len1);
	decode_group_var(buff + len1, idx_len, len2);
	decode_group_var(buff + len1 + len2, msg_id, len3);
	// У�������Ƿ�����
	if (m_stream->space() < msg_len)
	{
		m_stream->seek(pos, SEEK_SET);
		return false;
	}
	m_ext = true;
	m_size = msg_len;
	m_offset = idx_len == 0 ? 0 : msg_len - idx_len;
	return true;
}

void pt_decoder::skip()
{
	if (m_ext && m_size > 0)
		m_stream->seek((long)m_size, SEEK_CUR);
}

bool pt_decoder::pre_read(size_t tag)
{
	if (m_stream->eof())
		return false;

	if (m_tag == 0)
	{
		if (!read_tag())
			return false;
	}

	// ��ȡ��������Ч����
	while (tag > m_tag)
	{
		// skip length content
		skip();
		if (!read_tag())
			return false;
	}
	// �����������ʹ��Ĭ������
	m_tag -= tag;
	if (m_tag != 0)
		m_data = 0;
	return true;
}

bool pt_decoder::read_tag(bool use_tag)
{
	char flag;
	uint64_t temp;
	if (!m_stream->read(&flag, 1))
		return false;
	m_ext = ((flag & MASK_TYPE) != 0);
	// ����tag
	if (use_tag)
	{
		size_t tag = flag & MASK_TAGS;
		if (tag == 3)
		{
			if (!read_var(temp))
				return false;
			tag += (size_t)temp + 2;
		}
		tag += 1;
		m_tag += tag;
	}
	// ����data
	m_data = flag & 0x0F;
	if (flag & 0x10)
	{// ���ж�������,���ַ�ʽ��ȡ
		uint64_t temp;
		if (!m_ext)
		{
			if (!read_var(temp))
				return false;
		}
		else
		{
			if (m_indexs.empty())
				return false;
			temp = m_indexs.front();
			m_indexs.pop_back();
		}
		m_data = (temp << 4) | m_data;
	}

	return true;
}

bool pt_decoder::read_var(uint64_t& data)
{
	data = 0;
	char off = 0;
	char tmp;
	do
	{
		if (off >= 64)
			return false;
		if (!m_stream->read(&tmp, 1))
			return false;
		data |= (uint64_t)(tmp & 0x7F) << off;
		off += 7;
	} while (tmp & 0x80);

	return true;
}

void pt_decoder::read_field(pt_str& data)
{
	if (!m_ext)
		return;
	size_t len = (size_t)m_size;
	if (len)
	{
		data.resize(len);
		m_stream->read(&data[0], len);
	}
	else
	{
		data.clear();
	}
}

void pt_decoder::read_field(pt_message& msg)
{
	if (!m_ext || !m_data)
		return;
	size_t epos;
	m_stream->suspend(epos, (size_t)m_size);
	beg_tag();
	msg.decode(*this);
	end_tag();
	m_stream->recovery(epos);
}
