#ifndef _LBUF_H
#define _LBUF_H

#include <lua.h>
#include <lualib.h>
#include <lauxlib.h>

#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>

#define SEEK_BEG 3		// Ô­µã
#define LUA_BUF_NAME "Buffer"
#define lua_checkbuf(L, i) ((lbuf_t*)luaL_checkudata(L, i, LUA_BUF_NAME))

typedef struct lstr_t{
	size_t	ref;
	size_t  len;
	char*	buf;
} lstr_t;

typedef struct lbuf_t{
	lstr_t* data;
	size_t	spos;
	size_t	epos;
	size_t	cpos;
} lbuf_t;

#define buf_cursor(buf)		(buf->data->buf + buf->cpos)
#define buf_data(buf)		(buf->data->buf + buf->spos)
#define buf_caps(buf)		(buf->data ? buf->data->len : 0)
#define buf_size(buf)		(buf->epos - buf->spos)
#define buf_can_read(buf)	(buf->epos - buf->cpos)
#define buf_empty(buf)		(buf->epos == buf->spos)
#define buf_eof(buf)		(buf->cpos == buf->epos)

LUALIB_API void buf_init(lbuf_t* buf, const void* data, size_t len);
LUALIB_API void buf_free(lbuf_t* buf);
LUALIB_API void buf_slice(lbuf_t* buf, lbuf_t* sub, size_t off, size_t len);
LUALIB_API bool buf_reserve(lbuf_t* buf, size_t len);
LUALIB_API bool buf_resize(lbuf_t* buf, size_t len);
LUALIB_API void buf_seek(lbuf_t* buf, int off, int origin);
LUALIB_API void buf_write(lbuf_t* buf, const void* data, size_t len);
LUALIB_API bool buf_read(lbuf_t* buf, void* data, size_t len);
LUALIB_API char* buf_peek(lbuf_t* buf, void* data, size_t len);
LUALIB_API char* buf_move(lbuf_t* buf, size_t len);
LUALIB_API char* buf_skip(lbuf_t* buf, size_t len);
LUALIB_API void buf_arrange(lbuf_t* buf);
LUALIB_API void buf_clear(lbuf_t* buf);
LUALIB_API bool buf_read_var(lbuf_t* buf, uint64_t* value);
LUALIB_API void buf_write_var(lbuf_t* buf, uint64_t value);

//////////////////////////////////////////////////////////////////////////
// lua api
//////////////////////////////////////////////////////////////////////////
#define luaL_set_enum(L, x) lua_pushinteger(L, x);lua_setglobal(L, #x)
LUALIB_API lbuf_t* lua_new_buf(lua_State* L, const char* data, size_t len);
LUALIB_API void lua_class(lua_State* L, const luaL_Reg* funs, const char* name, const char* super);
LUALIB_API int  luaopen_buf(lua_State* L);
#endif