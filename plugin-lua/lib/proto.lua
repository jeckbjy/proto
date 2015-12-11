require("libproto")
-- 导入
-- proto type
PROTO_NIL = 1
PROTO_BLN = 2	-- bool
PROTO_S64 = 3
PROTO_U64 = 4
PROTO_F32 = 5
PROTO_F64 = 6
PROTO_STR = 7
PROTO_MSG = 11
-- proto container
PROTO_VEC = 11
PROTO_MAP = 12
PROTO_SET = 13

function proto_is_basic(ftype)
	return type(ftype) == "number" and ftype >= PROTO_S64 and ftype <= PROTO_F64
end

function proto_is_table(ftype)
    return type(ftype) == "table"
end

--[[
function pt_enum(tbl, start_index)
	local enum_tbl = {}
	local enum_idx = start_index or 0
	for i, v in ipairs(tbl) do
		enum_tbl[v] = enum_idx + i
	end
	return enum_tbl
end
]]

--- @brief 调试时打印变量的值  
--- @param data 要打印的字符串  
--- @param [max_level] table要展开打印的计数，默认nil表示全部展开  
--- @param [prefix] 用于在递归时传递缩进，该参数不供用户使用于
function proto_dump(data, level, prefix)
	if type(prefix) ~= "string" then  
        prefix = ""  
    end  
    if type(data) ~= "table" then  
        print(prefix .. tostring(data))  
    else  
        print(data)  
        if max_level ~= 0 then  
            local prefix_next = prefix .. "    "  
            print(prefix .. "{")  
            for k,v in pairs(data) do  
                io.stdout:write(prefix_next .. k .. " = ")  
                if type(v) ~= "table" or (type(max_level) == "number" and max_level <= 1) then  
                    print(v)  
                else  
                    if max_level == nil then  
                        var_dump(v, nil, prefix_next)  
                    else  
                        var_dump(v, max_level - 1, prefix_next)  
                    end  
                end  
            end  
            print(prefix .. "}")  
        end  
    end
end

function proto_class(name,super)
	local cls_meta = {}
	local cls = setmetatable( { cls_name = name}, cls_meta )
	cls.__index = cls
	cls.ctor = function() end
	cls_meta.__call = function(...) return cls.new(...) end
	-- 基类
	if super then
		cls.super = function(...) super.ctor(...) end
		cls_meta.__index = super
	end
	cls.new = function(...)
		-- 创建实例
		local instance = setmetatable({}, cls)
		-- 调用构造函数,没有递归调用
		if cls.ctor then
			cls.ctor(instance, ...)
		end
		return instance
	end
	return cls
end

---------------------------------------------------------------
-- Encoder
---------------------------------------------------------------
Encoder = proto_class("Encoder")

function Encoder:ctor(stream)
    self.stream = stream
    self.indexs = {}
end

function Encoder:encode(msg)
    local bpos,ipos,epos
	local stream = self.stream
    stream:move(20)
	print("move end",stream:info())
    bpos = stream:cursor()
    self:write_msg(msg)
    ipos = stream:cursor()
	print("bpos, ipos", bpos, ipos)
    if #self.indexs > 0 then
        -- 从后向前写入index
		local idx_len = #self.indexs
		for i = idx_len, 1 do
			local info = self.indexs[i]
			if info.size > 0 then
				self:write_var(info.size)
			end
		end
    end
    epos = stream:cursor()
    -- 写入头部
	print("pos", epos, bpos, ipos)
	local b1 = proto.pack_group_var(epos - bpos)
	local b2 = proto.pack_group_var(epos - ipos)
	local b3 = proto.pack_group_var(msg.proto_id)
	flag = (#b1 << 5) + (#b2 << 2) + #b3
	local len = #b1 + #b2 + #b3 + 1
	local spos = bpos - len
    stream:seek(spos, SEEK_SET)
    stream:discard()
    stream:write(flag)
	stream:write(b1)
	stream:write(b2)
	stream:write(b3)
	stream:seek(spos, SEEK_SET)
end

function Encoder:write_msg(msg)
    local desc = msg.proto_desc
    local last_id = 0
    for field_id, field_desc in ipairs(desc) do
        local field = msg[field_desc.name]
		if self:check(field, field_desc) then
			local result = self:write_field(field_id - last_id, field, field_desc)
			if result then
				last_id = field_id
			end
		end
    end
end

function Encoder:check(field, desc)
    if field == nil then
        return false
    end
	-- 容器
	if desc.kind ~= PROTO_NIL then
		return #field > 0
	end
	-- 类型
	local ftype = desc.ftype
	if proto_is_basic(ftype) then
		return tonumber(field) ~= 0
	elseif ftype == PROTO_STR then
		return #field > 0
	end
	return true
end

function Encoder:write_field(tag, field, field_desc)
	local kind  = field_desc.kind
	local ftype = field_desc.ftype
	local ktype = field_desc.ktype
	if kind == PROTO_NIL then
		self:write_value(tag, field, ftype)
	else
		-- 容器
        local index = self:write_beg(tag)
        if kind == PROTO_VEC then
            for i,v in ipairs(field) do
                self:write_value(1, v, ftype)
            end
		elseif kind == PROTO_SET then
			for i, v in pairs(field) do
				self:write_value(1, i, ftype)
			end
        elseif kind == PROTO_MAP then
            for i, v in pairs(field) do
                self:write_value(1, i, ktype)
                self:write_value(1, v, ftype)
            end
        end
        self:write_end(index)
	end
end

function Encoder:write_var(val)
	print("do write var")
	self.stream:write_var(val)
end

function Encoder:write_value(tag, field, ftype)
	if proto_is_basic(ftype) then
		-- 简单类型
        local value
		if ftype == PROTO_BLN then
			value = proto.pack_u64(field)
        elseif ftype == PROTO_U64 then
            value = proto.pack_u64(field)
	    elseif ftype == PROTO_S64 then
		    value = proto.pack_s64(field)
        elseif ftype == PROTO_F32 then
            value = proto.pack_f32(field)
        elseif ftype == PROTO_F64 then
            value = proto.pack_f64(field)
	    end
		
		self:write_tag(tag, value, false)
		--self:write_var(value)
	else
		-- 复杂类型
		local index = self:write_beg(tag)
		if ftype == PROTO_STR then
			self.stream:write(field)
		elseif type(ftype) == "table" then
			self:write_msg(field)
		end
		self:write_end(index, tag)
	end
end

function Encoder:write_tag(tag, val, ext)
	print("write_tag beg",self.stream:cursor())
    -- flag
    local flag = ext and 0x80 or 0
    -- tag
    tag = tag - 1
    if tag < 3 then
        flag = flag | (tag << 5)
        tag = 0
    else
        flag = flag | 0x60
        tag = tag - 2
    end
    -- val
    flag = flag | (val & 0x0F)
    val = val >> 4
	if val > 0 then
		flag = flag | 0x10
	end
	-- 写入1个字节
	--self.stream:info()
	self.stream:write(flag)
	--self.stream:info()
	-- 写入tag，val
	if tag > 0 then
		self:write_var(tag)
	end
	if val > 0 then
		self:write_var(val)
	end
	print("write_tag end",self.stream:cursor())
end

function Encoder:write_beg(tag)
    local info = {}
    info.spos = self.stream:cursor()
    self:write_tag(tag, 0, false)
    info.epos = self.stream:cursor()
    info.size = 0
    table.insert(self.indexs, info)
    return #self.indexs
end

function Encoder:write_end(index)
	local info = self.indexs[index]
	local epos = self.stream:cursor()
	local leng = epos - info.bpos
	self.stream:seek(info.tpos, SEEK_SET)
	local flag = self.stream:peek()
	flag = flag | (leng & 0x0F)
	leng = leng >> 4
	-- 还有剩余数据
	if leng > 0 then
		flag = flag | 0x10
		info.size = leng
	end
	self.stream.write(flag)
	self.stream:seek(epos, SEEK_SET)
end

---------------------------------------------------------------
-- Decoder
---------------------------------------------------------------
Decoder = proto_class("Decoder")

function Decoder:ctor(stream)
	self.stream = stream
	self.indexs = {}
end

-- 解析消息
function Decoder:decode(fun)
	-- def result
	local msg
    -- clear
    self.indexs = {}
    -- read head
	local stream = self.stream
	local ok, pkg_len, idx_len, msg_id, msg_len = self:read_head()
	if ok == false then
		return nil
	end
	-- read body
	local spos = stream:cursor()
	local epos = spos + pkg_len
	local msg_cls = fun(msg_id)
	if msg_cls ~= nil then
		-- 先读取index
		if idx_len > 0 then
			while stream:cursor() < epos do
				local val = stream:read_var()
				if val then
					table.insert(self.indexs, val)
				end
			end
			stream:seek(spos, SEEK_SET)
		end
		-- parse msg
		msg = self:read_msg(msg_cls, msg_len)
	end
	-- 移动到正确位置
	stream:seek(epos, SEEK_SET)
	return msg
end

function Decoder:read_head()
	local stream = self.stream
	local spos = stream:cursor()
	local flag = stream:read()
	print("flag",flag)
	local len1 = flag >> 5
	local len2 = (flag >> 2) & 0x07
	local len3 = flag & 0x03
	local leng = len1 + len2 + len3
	if stream:can_read() < leng then
		stream:seek(spos, SEEK_SET)
		return false
	end
	local pkg_len = proto.unpack_group_var(stream:read(len1))
	local idx_len = proto.unpack_group_var(stream:read(len2))
	local msg_id  = proto.unpack_group_var(stream:read(len3))
	if stream:can_read() < pkg_len then
		stream:seek(spos, SEEK_SET)
		return false
	end
	print(pkg_len, idx_len, msg_id, pkg_len - idx_len)
	return true, pkg_len, idx_len, msg_id, pkg_len - idx_len
end

function Decoder:read_tag()
	local tag, val, ext, tmp
	local flag = self.stream:read()
	if flag == nil then
		return nil
	end
	ext = (flag & 0x80) ~= 0
	-- tag
	tag = flag & 0x60
	if tag == 3 then
		tmp = self:read_var()
		if tmp == nil then
			return nil
		end
		tag = tag + tmp + 2
	end
	tag = tag + 1
	val = flag & 0x0F
	if (flag & 0x10) ~= 0 then
		if not ext then
			tmp = self:read_var()
		elseif #self.indexs > 0 then 
			tmp = self.indexs[#self.indexs]
			table.remove(self.indexs, #self.indexs)
		end
		if tmp == nil then
			return nil
		end
		val = tmp << 4 | val
	end
    return val, ext, tag
end

function Decoder:read_msg(msg, msg_len)
    local stream = self.stream
	local msg_epos = stream:cursor() + msg_len
    local desc = msg.proto_desc
    local field_id = 0
    local field_desc
    while stream:cursor() < msg_epos do
        local val, ext, tag = self:read_tag()
        -- 计算结束位置
		local epos = stream:cursor()
		if ext then
			epos = epos + val
		end
		--
        field_id = field_id + tag
        field_desc = desc[field_id]
        if field_desc then
            msg[field_desc.name] = self:read_field(field_desc, val)
        end
		-- 移动到正确位置，忽略无效字段，并能防止读取错误
		stream:seek(epos, SEEK_SET)
    end
end

function Decoder:read_field(field_desc, data)
    local kind = field_desc.kind
	if kind == PROTO_NIL then
        return self:read_value(field_desc.ftype, data)
	else
		-- 容器
        local result = {}
        local epos = self.stream:cursor() + data
        if kind == PROTO_VEC then
            while self.stream:cursor() < epos do
                local val = self:read_tag()
                local item = self:read_value(field_desc.ftype, val)
                table.insert(result, item)
            end
		elseif kind == PROTO_MAP then
            while self.stream:cursor() < epos do
                local key = self:read_value(field_desc.ktype)
                local val = self:read_value(field_desc.ftype)
                result[key] = val
            end
        elseif kind == PROTO_SET then
            while self.stream:cursor() < epos do
                local key = self:read_value(field_desc.ftype)
                result[key] = true
            end
        end
        return result
	end
end

function Decoder:read_value(ftype, data)
    if proto_is_basic(ftype) then
        local value
        if ftype == PROTO_BLN then
            value = proto.unpack_u64(data)
        elseif ftype == PROTO_U64 then
            value = proto.unpack_u64(data)
        elseif ftype == PROTO_S64 then
            value = proto.unpack_s64(data)
        elseif ftype == PROTO_F32 then
            value = proto.unpack_f32(data)
        elseif ftype == PROTO_F64 then
            value = proto.unpack_f64(data)
        end
        return value
    else
        if ftype == PROTO_STR then
            return self.stream:read(data)
        elseif proto_is_table(ftype) then
            return self:read_msg(ftype, data)
        end
    end
end

function Decoder:read_var()
	return self.stream:read_var()
end
