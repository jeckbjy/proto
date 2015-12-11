#define LUA_LIB
#include <lua.h>
#include <lualib.h>
#include <lauxlib.h>
#include <stdint.h>
#include <stdio.h>
#include "lbuf.h"

typedef unsigned int uint;

static uint64_t encode_i64(int64_t n)	{ return (n << 1) ^ (n >> 63); }
static uint64_t encode_f64(double n)	{ union { double f; uint64_t i; } d; d.f = n; return d.i; }
static uint64_t encode_f32(float n)		{ union { float  f; uint32_t i; } d; d.f = n; return d.i; }

static int64_t  decode_i64(uint64_t n)	{ return (n >> 1) ^ -(int64_t)(n & 1); }
static double   decode_f64(uint64_t n)	{ union { double f; uint64_t i; } d; d.i = n; return d.f; }
static float    decode_f32(uint64_t n)	{ union { float  f; uint32_t i; } d; d.i = (uint32_t)n; return d.f; }

static size_t encode_var(char* buff, uint64_t value)
{
	size_t count = 0;
	while (value > 0x7F)
	{
		buff[count++] = ((uint8_t)(value)& 0x7F) | 0x80;
		value >>= 7;
	}
	buff[count++] = value & 0x7F;
	return count;
}

static uint64_t decode_var(const char* buff, size_t len)
{
	const char* ptr = buff;
	uint64_t data;
	char off = 0;
	char tmp;
	do 
	{
		if (off >= 64)
			return 0;
		tmp = *ptr;
		data |= ((uint64_t)(tmp & 0x7F)) << off;
		off += 7;
	} while (tmp & 0x80);
	return data;
}

//////////////////////////////////////////////////////////////////////////
// proto encode
//////////////////////////////////////////////////////////////////////////
static int pack_var(lua_State* L, uint64_t n)
{
	char buff[10];
	size_t len = encode_var(buff, n);
	lua_pushlstring(L, buff, len);
	return 1;
}

static int pack_u64(lua_State* L)
{
	uint64_t n = (uint64_t)lua_tointeger(L, 1);
	lua_pushinteger(L, (lua_Integer)n);
	//return pack_var(L, n);
	return 1;
}

static int pack_s64(lua_State* L)
{
	int64_t n = (int64_t)lua_tointeger(L, 1);
	lua_pushinteger(L, (lua_Integer)encode_i64(n));
	//return pack_var(L, encode_i64(n));
	return 1;
}

static int pack_f32(lua_State* L)
{
	float n = (float)lua_tonumber(L, 1);
	//return pack_var(L, encode_f32(n));
	lua_pushinteger(L, (lua_Integer)encode_f32(n));
	return 1;
}

static int pack_f64(lua_State* L)
{
	double n = (double)luaL_checknumber(L, 1);
	lua_pushinteger(L, (lua_Integer)encode_f64(n));
	//return pack_var(L, encode_f64(n));
	return 1;
}

//////////////////////////////////////////////////////////////////////////
// unpack
//////////////////////////////////////////////////////////////////////////
static int unpack_u64(lua_State* L)
{
	uint64_t n = (uint64_t)luaL_checkinteger(L, 1);
	lua_pushinteger(L, (lua_Integer)n);
	return 1;
}

static int unpack_s64(lua_State* L)
{
	uint64_t n = (uint64_t)luaL_checkinteger(L, 1);
	lua_pushinteger(L, decode_i64(n));
	return 1;
}

static int unpack_f64(lua_State* L)
{
	uint64_t n = (uint64_t)luaL_checkinteger(L, 1);
	lua_pushnumber(L, decode_f64(n));
	return 1;
}

static int unpack_f32(lua_State* L)
{
	uint64_t n = (uint64_t)luaL_checkinteger(L, 1);
	lua_pushnumber(L, decode_f32(n));
	return 1;
}

static const luaL_Reg proto_reg[] = {
	{ "pack_u64", pack_u64 },
	{ "pack_s64", pack_s64 },
	{ "pack_f64", pack_f64 },
	{ "pack_f32", pack_f32 },

	{ "unpack_u64", unpack_u64 },
	{ "unpack_s64", unpack_s64 },
	{ "unpack_f64", unpack_f64 },
	{ "unpack_f32", unpack_f32 },

	{NULL, NULL}
};

LUAMOD_API int luaopen_libproto(lua_State* L)
{
	// 注册buf
	luaopen_buf(L);
	// 放到全局表中
	luaL_newlib(L, proto_reg);
	lua_pushvalue(L, -1);
	lua_setglobal(L, "proto");
	return 1;
}
