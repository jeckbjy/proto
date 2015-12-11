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
    local bpos,ipos,epos;
    stream:move(20)
    bpos = stream:cursor()
    proto_encode_msg(msg)
    ipos = stream:cursor()
    if #self.indexs > 0 then
        -- 写入index
    end
    epos = stream:cursor()
    -- 写入头部
    local buff
    local len1,len2,len3
    local len
    buff = buff .. encode_group_var(epos - bpos)
    buff = buff .. encode_group_var(epos - ipos)
    buff = buff .. encode_group_var(msg.proto_id)
    len = string.len(buff)
    stream:seek(20 - len)
    stream:discard()
    stream:write(buff, len)
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
		return field ~= 0
	else ftype == PROTO_STR then
		return #field > 0
	end
	return true
end

function Encoder:write_field(tag, field, kind, ftype, ktype)
    local kind = field_desc.kind
	if kind == PROTO_NIL then
		self:write_value(tag, field, ftype)
	else
		-- 容器
        local index = self:write_beg(tag)
        if kind == PROTO_VEC then
            for i,v in ipairs(field) do
                self:write_field(1, v, ftype)
            end
		elseif kind == PROTO_SET then
			for i, v in pairs(field) do
				self:write_field(1, i, ftype)
			end
        elseif kind == PROTO_MAP then
            for i, v in pairs(field) do
                self:write_field(1, i, ktype)
                self:write_field(1, v, ftype)
            end
        end
        self:write_end(index)
	end
end

function Encoder:write_var(val)
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
		if tag == 0 then
			self:write_tag(tag, value, false)
		else
			self:write_var(value)
		end
	else
		-- 复杂类型
		local index = self:write_beg(tag)
		if ftype == PROTO_STR then
			self.stream:write(field)
		elseif type(ftype) == "table" then
			self:write_msg(field)
		end
		self:write_end(index)
	end
end

function Encoder:write_tag(tag, val, ext)
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
    flag |= val & 0x0F
    val = val >> 4
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
end

---------------------------------------------------------------
-- Decoder
---------------------------------------------------------------
Decoder = proto_class("Decoder")

function Decoder:ctor(stream)
	self.stream = stream
	self.indexs = {}
end

function Decoder:decode(fun)
    -- clear
    self.indexs = {}
    -- read
end

function Decoder:read_tag()
    return 0
end

function Decoder:read_msg(msg, len)
    local stream = self.stream
    local desc = msg.proto_desc
    local field_id = 0
    local tag, field_desc
    while not stream:eof() do
        tag = self:read_tag()
        -- 计算结束位置
        field_id = field_id + tag
        field_desc = desc[field_id]
        if field_desc then
            msg[field_desc.name] = self:read_field()
        end
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
	
end
