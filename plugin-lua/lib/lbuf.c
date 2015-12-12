#define LUA_LIB
#include "lbuf.h"
#include <string.h>
#include <stdio.h>

#ifdef _WIN32
#define snprintf sprintf_s
#endif

void buf_init(lbuf_t* buf, const void* data, size_t len)
{
	memset(buf, 0, sizeof(lbuf_t));
	buf_reserve(buf, len);
	if (data)
	{
		memcpy(buf_cursor(buf), data, len);
		buf->epos = len;
	}
}

void buf_free(lbuf_t* buf)
{
	if (buf->data != NULL)
	{
		if (--buf->data->ref == 0)
		{
			free(buf->data);
		}
		buf->data = NULL;
		buf->spos = buf->epos = buf->cpos = 0;
	}
}

void buf_slice(lbuf_t* buf, lbuf_t* sub, size_t off, size_t len)
{
	if (buf->data == NULL)
		return;
	size_t epos = buf->spos + off + len;
	if (epos > buf->epos)
		return;
	++buf->data->ref;
	sub->data = buf->data;
	sub->spos = buf->spos + off;
	sub->epos = epos;
	sub->cpos = buf->spos;
}

bool buf_reserve(lbuf_t* buf, size_t len)
{
	if (len == 0)
		return true;
	size_t new_len = len;
	lstr_t* block = buf->data;
	if (block)
	{// copy on write
		if (block->ref <= 1 && block->len >= len)
		{
			return true;
		}
		new_len = max(block->len, len);
		new_len <<= 1;
	}

	//printf("reserver beg %d %d %d:\n", buf->spos, buf->cpos, buf->epos);
	lstr_t* new_block = malloc(sizeof(lstr_t) + new_len);
	if (!new_block)
		return false;
	new_block->buf = (char*)new_block + sizeof(lstr_t);
	// copy old data
	if (!buf_empty(buf))
		memcpy(new_block->buf, buf_data(buf), buf_size(buf));
	// 释放并赋值
	if (buf->data && --buf->data->ref == 0)
		free(buf->data);

	new_block->ref = 1;
	new_block->len = new_len;
	buf->data = new_block;
	if (buf->spos > 0)
	{
		buf->epos -= buf->spos;
		buf->cpos -= buf->spos;
		buf->spos = 0;
	}
	//printf("reserver end %d %d %d:\n", buf->spos, buf->cpos, buf->epos);
	return true;
}

bool buf_resize(lbuf_t* buf, size_t len)
{
	size_t epos = buf->spos + len;
	if (!buf_reserve(buf, epos))
		return false;
	buf->epos = epos;
	return true;
}

void buf_seek(lbuf_t* buf, int off, int origin)
{
	size_t cpos;
	switch (origin)
	{
	case SEEK_SET:
		cpos = off;
		break;
	case SEEK_BEG:
		cpos = buf->spos + off;
		break;
	case SEEK_CUR:
		cpos = buf->cpos + off;
	case SEEK_END:
		cpos = buf->epos - off;
	}
	if (cpos < buf->spos || cpos > buf->epos)
		return;
	buf->cpos = cpos;
}

void buf_write(lbuf_t* buf, const void* data, size_t len)
{
	if (data == 0)
		return;
	//printf("write cpos beg: %d\n", buf->cpos);
	char* ptr = buf_move(buf, len);
	if (ptr)
		memcpy(ptr, data, len);
	//printf("write cpos end: %d\n", buf->cpos);
}

bool buf_read(lbuf_t* buf, void* data, size_t len)
{
	char* ptr = buf_skip(buf, len);
	if (ptr)
		memcpy(data, ptr, len);
	return ptr != NULL;
}

char* buf_peek(lbuf_t* buf, void* data, size_t len)
{
	if (len == 0 || buf_can_read(buf) < len)
		return NULL;
	char* ptr = buf_cursor(buf);
	if (data != NULL)
		memcpy(data, ptr, len);
	return ptr;
}

char* buf_move(lbuf_t* buf, size_t len)
{
	//向前移动len个字节，空间不足创建，类似write，但是不会写入数据
	if (len == 0)
		return NULL;
	size_t epos = buf->cpos + len;
	if (buf_reserve(buf, epos))
	{
		char* ptr = buf_cursor(buf);
		if (epos > buf->epos)
			buf->epos = epos;
		buf->cpos += len;
		return ptr;
	}
	return NULL;
}

char* buf_skip(lbuf_t* buf, size_t len)
{
	if (len == 0)
		return 0;
	size_t can_read = buf->epos - buf->cpos;
	if (can_read < len)
		return 0;
	char* ptr = buf_cursor(buf);
	buf->cpos += len;
	return ptr;
}

void buf_arrange(lbuf_t* buf)
{
	size_t len = buf_size(buf);
	if (len == 0 || buf->spos == 0)
		return;
	if (buf_reserve(buf, len) && buf->spos != 0)
	{
		memcpy(buf_data(buf), buf_cursor(buf), len);
		buf->cpos -= buf->spos;
		buf->epos -= buf->spos;
		buf->spos = 0;
	}
}

void buf_clear(lbuf_t* buf)
{
	buf_free(buf);
	buf->spos = buf->cpos = buf->epos = 0;
}

bool buf_read_var(lbuf_t* buf, uint64_t* data)
{
	*data = 0;
	char off = 0;
	char tmp;
	do 
	{
		if (off >= 64)
			break;
		if (!buf_read(buf, &tmp, 1))
			return false;
		*data |= (uint64_t)(tmp & 0x7F) << off;
		off += 7;
	} while (tmp & 0x80);
	return true;
}

void buf_write_var(lbuf_t* buf, uint64_t value)
{
	//高位标识：0表示结尾,1表示后边还有数据
	char buff[20];
	size_t count = 0;
	while (value > 0x7F)
	{
		buff[count++] = ((uint8_t)(value)& 0x7F) | 0x80;
		value >>= 7;
	}
	buff[count++] = value & 0x7F;
	buf_write(buf, buff, count);
}

//////////////////////////////////////////////////////////////////////////
// lua api
//////////////////////////////////////////////////////////////////////////
void lua_class(lua_State* L, const luaL_Reg* funs, const char* name, const char* super)
{
	luaL_newmetatable(L, name);
	luaL_setfuncs(L, funs, 0);
	lua_pushvalue(L, -1);
	lua_setfield(L, -2, "__index");
	lua_pushstring(L, name);
	lua_setfield(L, -2, "__name");
	if (super != 0)
		luaL_setmetatable(L, super);
	// 放到全局表中
	lua_setglobal(L, name);
}

lbuf_t* lua_new_buf(lua_State* L, const char* data, size_t len)
{
	lbuf_t* buf = (lbuf_t*)lua_newuserdata(L, sizeof(lbuf_t));
	buf_init(buf, data, len);
	luaL_setmetatable(L, LUA_BUF_NAME);
	return buf;
}

//////////////////////////////////////////////////////////////////////////
// lua wrapper
//////////////////////////////////////////////////////////////////////////
static int lbuf_new(lua_State* L)
{
	size_t len = 0;
	const char* data = NULL;
	if (lua_isinteger(L, 1)){
		len = (size_t)lua_tointeger(L, 1);
	}else if (lua_isstring(L, 1)){
		data = lua_tolstring(L, 1, &len);
	}

	lbuf_t* buf = lua_new_buf(L, data, len);
	return 1;
}

static int lbuf_free(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	buf_free(buf);
	return 0;
}

static int lbuf_clear(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	buf_clear(buf);
	return 0;
}

static int lbuf_slice(lua_State* L)
{
	int params = lua_gettop(L);
	lbuf_t* buf = lua_checkbuf(L, 1);
	size_t  off = (size_t)lua_tointeger(L, 2);
	size_t  len = (size_t)lua_tointeger(L, 3);
	if (len == 0)
		len = buf_size(buf) - off;
	lbuf_t* sub = lua_new_buf(L, 0, 0);
	buf_slice(buf, sub, off, len);
	return 1;
}

static int lbuf_write(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	int type = lua_type(L, 2);
	switch (type)
	{
	case LUA_TNIL:
		break;
	case LUA_TSTRING:
	{
		const char* data;
		size_t len;
		data = lua_tolstring(L, 2, &len);
		buf_write(buf, data, len);
	}
	break;
	case LUA_TUSERDATA:
	{
		lbuf_t* data = lua_checkbuf(L, 2);
		buf_write(buf, buf_data(buf), buf_size(data));
	}
	break;
	default:
	{
		uint8_t data = (uint8_t)lua_tointeger(L, 2);
		buf_write(buf, &data, 1);
	}
	break;
	}
	return 0;
}

static int lbuf_read(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	int params = lua_gettop(L);
	if (params == 1)
	{// 无参数时，默认读取一个字节
		char data;
		bool ret = buf_read(buf, &data, 1);
		if (ret)
			lua_pushinteger(L, data);
		else
			lua_pushnil(L);
	}
	else if (lua_isinteger(L, 2))
	{// param:len, mode
		size_t len = (size_t)lua_tointeger(L, 2);
		const char* data = buf_skip(buf, len);
		if (data == 0){
			lua_pushnil(L);
		}else{
			const char* mode = "s";
			if (lua_isstring(L, 3))
				mode = lua_tostring(L, 3);
		}

		const char* mode = "s";
		if (lua_isstring(L, 3))
			mode = lua_tostring(L, 3);
		if (*mode == 's')
			lua_pushlstring(L, data, len);
		else if (*mode == 'b')
			lua_new_buf(L, data, len);
		else
			luaL_error(L, "bad read format #3");
	}
	else
	{
		luaL_error(L, "bad param");
	}

	return 1;
}

static int lbuf_peek(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	int params = lua_gettop(L);
	if (params == 1)
	{// 无参数时，默认读取一个字节
		char data;
		bool ret = buf_peek(buf, &data, 1);
		if (ret)
			lua_pushinteger(L, data);
		else
			lua_pushnil(L);
	}
	else if (lua_isinteger(L, 2))
	{// param:len, mode
		size_t len = (size_t)lua_tointeger(L, 2);
		const char* data = buf_peek(buf, NULL, len);
		if (data == 0) {
			lua_pushnil(L);
		}
		else {
			const char* mode = "s";
			if (lua_isstring(L, 3))
				mode = lua_tostring(L, 3);
		}

		const char* mode = "s";
		if (lua_isstring(L, 3))
			mode = lua_tostring(L, 3);
		if (*mode == 's')
			lua_pushlstring(L, data, len);
		else if (*mode == 'b')
			lua_new_buf(L, data, len);
		else
			luaL_error(L, "bad read format #3");
	}
	else
	{
		luaL_error(L, "bad param");
	}

	return 1;
}

static int lbuf_read_var(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	uint64_t n;
	bool ret = buf_read_var(buf, &n);
	if (ret)
		lua_pushinteger(L, (lua_Integer)n);
	else
		lua_pushnil(L);
	return 1;
}

static int lbuf_write_var(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	uint64_t n = (uint64_t)luaL_checkinteger(L, 2);
	buf_write_var(buf, n);
	return 1;
}

static int lbuf_move(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	size_t  len = (size_t)luaL_checkinteger(L, 2);
	buf_move(buf, len);
	return 0;
}

static int lbuf_skip(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	size_t  len = (size_t)luaL_checkinteger(L, 2);
	buf_skip(buf, len);
	return 0;
}

static int lbuf_caps(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	lua_pushinteger(L, buf_caps(buf));
	return 1;
}

static int lbuf_size(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	lua_pushinteger(L, (lua_Integer)buf_size(buf));
	return 1;
}

static int lbuf_can_read(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	lua_pushinteger(L, (lua_Integer)buf_can_read(buf));
	return 1;
}

static int lbuf_empty(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	bool result = buf_empty(buf);
	lua_pushboolean(L, result);
	return 1;
}

static int lbuf_eof(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	bool result = buf_eof(buf);
	lua_pushboolean(L, result);
	return 1;
}

static int lbuf_cursor(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	lua_pushinteger(L, (lua_Integer)buf->cpos);
	return 1;
}

static int lbuf_position(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	lua_pushinteger(L, (lua_Integer)(buf->cpos - buf->spos));
	return 1;
}

static int lbuf_discard(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	buf->spos = buf->cpos;
	return 0;
}

static int lbuf_arrange(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	buf_arrange(buf);
	return 0;
}

static int lbuf_seek(lua_State* L)
{
	int params = lua_gettop(L);
	lbuf_t* buf = lua_checkbuf(L, 1);
	int off = (int)luaL_checkinteger(L, 2);
	int origin = SEEK_CUR;
	if (params > 2)
	{
		if (lua_isinteger(L, 3)){
			origin = (int)lua_tointeger(L, 3);
		}else if (lua_isstring(L, 3)){
			static const char* options[] = { "SEEK_SET", "SEEK_CUR", "SEEK_END", "SEEK_BEG" };
			const char* mode = lua_tostring(L, 3);
			origin = SEEK_CUR;
			for (int i = 0; i < 4; ++i)
			{
				if (strcmp(mode, options[i]))
				{
					origin = i;
					break;
				}
			}
		}
	}
	buf_seek(buf, off, origin);
	return 0;
}

static int lbuf_resize(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	size_t len = (size_t)luaL_checkinteger(L, 2);
	buf_resize(buf, len);
	return 0;
}

static int lbuf_reserve(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	size_t len = (size_t)luaL_checkinteger(L, 2);
	buf_reserve(buf, len);
	return 0;
}

static int lbuf_tostring(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	if (buf_empty(buf))
		lua_pushstring(L, "");
	else
		lua_pushlstring(L, buf_data(buf), buf_size(buf));
	return 1;
}

static int lbuf_tohex(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	if (buf_empty(buf)){
		lua_pushstring(L, "");
	}else{
		luaL_Buffer hex_buf;
		luaL_buffinit(L, &hex_buf);
		luaL_addlstring(&hex_buf, "0x", 2);
		char* ptr = buf_data(buf);
		char* end = ptr + buf_size(buf);
		char  tmp[3];
		for (; ptr != end; ++ptr)
		{
			snprintf(tmp, 3, "%02x", (uint8_t)*ptr);
			luaL_addlstring(&hex_buf, tmp, 2);
		}
		luaL_pushresult(&hex_buf);
	}
	return 1;
}

static int lbuf_info(lua_State* L)
{
	lbuf_t* buf = lua_checkbuf(L, 1);
	char str[100];
	lstr_t* data = buf->data;
	snprintf(str, sizeof(str), "spos:%-5d epos:%-5d cpos:%-5d caps:%-5d refs:%-5d", buf->spos, buf->epos, buf->cpos, data ? data->len : 0, data ? data->ref : 0);
	lua_pushstring(L, str);
	return 1;
}

static const luaL_Reg buf_reg[] = {
	{ "__gc",		lbuf_free },
	{ "__tostring", lbuf_tostring},
	{ "tostring",	lbuf_tostring },
	{ "tohex",		lbuf_tohex},
	{ "new",		lbuf_new },
	{ "eof",		lbuf_eof },
	{ "empty",		lbuf_empty },
	{ "size",		lbuf_size},
	{ "can_read",	lbuf_can_read },
	{ "capacity",	lbuf_caps},
	{ "cursor",		lbuf_cursor},
	{ "position",	lbuf_position},
	{ "clear",		lbuf_clear},
	{ "resize",		lbuf_resize},
	{ "reserve",	lbuf_reserve},
	{ "slice",		lbuf_slice},
	{ "seek",		lbuf_seek},
	{ "write",		lbuf_write},
	{ "read",		lbuf_read},
	{ "peek",		lbuf_peek},
	{ "write_var",  lbuf_write_var},
	{ "read_var",	lbuf_read_var},
	{ "move",		lbuf_move},	
	{ "skip",		lbuf_skip},
	{ "discard",	lbuf_discard},
	{ "arrange",	lbuf_arrange},
	{ "info",		lbuf_info },
	{ NULL, NULL }
};

LUAMOD_API int luaopen_buf(lua_State* L)
{
	// 注册enum
	luaL_set_enum(L, SEEK_CUR);
	luaL_set_enum(L, SEEK_SET);
	luaL_set_enum(L, SEEK_END);
	luaL_set_enum(L, SEEK_BEG);

	lua_class(L, buf_reg, LUA_BUF_NAME, 0);
	return 0;
}
