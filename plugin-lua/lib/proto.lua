-- 导入
-- proto type
PROTO_NIL = 1
PROTO_BLN = 2       -- boolean
PROTO_S64 = 3
PROTO_U64 = 4
PROTO_F32 = 5
PROTO_F64 = 6
PROTO_STR = 7
--PROTO_MSG = 8
-- proto container
PROTO_VEC = 11
PROTO_MAP = 12
PROTO_SET = 13

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

--编码
function encode_group_var(data)
    if data == 0 then
        return ""
    end
    local result = ""
end

--解码
function decode_group_var(buff)
    local result = 0
end

-- 序列化
function proto_encode(stream, msg)
    local bpos,ipos,epos;
    stream:advance(20)
    bpos = stream:cursor()
    proto_encode_msg(msg)
    ipos = stream:cursor()
    if stream:hasIndex() then
        -- 写入index
    end
    epos = stream:cursor()
    -- 写入头部
    local buff;
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

-- 序列化
function proto_encode_msg(stream, msg)
    local desc = msg.proto_desc
    local last_id = 0
    for id, info in ipairs(desc) do
        local field = msg[info.name]
        if field != nil then
            -- 写入数据
        end
    end
end

-- 字段
function proto_encode_field(stream, field, field_desc)
    local container = field_desc.container
    if container == PROTO_NIL then
    elseif container == PROTO_VEC then
    elseif container == PROTO_MAP then
    elseif container == PROTO_SET then
    end
end

-- 数据
function proto_encode_field(stream, field)
end

-- 反序列化
function proto_decode(stream, creator)
    -- 解析头部
end

-- 解析fields
function proto_decode_msg(stream, msg)
    local desc = msg.proto_desc
    local field_id = 0;
    while not stream:eof() do
        -- 读取tag
        local tag = stream:read_tag()
        field_id += tag
        local field_desc= desc[field_id]
        if field_desc then
            msg[field_desc.name] = proto_decode_field(stream, desc)            
        end
    end
end

function proto_decode_field(stream, desc)
    local result
    local container = desc.container
    if (not container) or (container == PROTO_NIL) then
        result = proto_decode_data(stream, desc.type)
    else
        result = {}
        local len = stream:read()
        local old_epos = stream:suspend(len)
        if container == PROTO_VEC then
            while not stream:eof() do
                local item = proto_decode_data(stream, desc.pt_type)
                table.insert(result, item)
            end
        elseif container == PROTO_MAP then
            while not stream:eof() do
                local key = proto_decode_data(stream, desc.key)
                local val = proto_decode_data(stream, desc.pt_type)
                result[key] = val
            end
        elseif container == PROTO_SET then
            while not stream:eof() do
                local key = proto_decode_data(stream, desc.pt_type)
                result[key] = true
            end
        end
        stream:recovery(old_epos)
    end
    return result
end

function proto_decode_data(stream, pt_type)
    if type(pt_type) == "table" then
        local len = stream:read()
        local old_epos = stream:suspend(len)
        local msg = pt_type.new()
        proto_decode_msg(stream, msg)
        stream:recovery(old_epos)
        return msg
    else
    end
end
