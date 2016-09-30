
----------------------------------------------------------------
--
-----------------------------------------------------------------
function encodei64(n)
	local r = n << 1
--	print(r)
	if n < 0 then
		r = r ~(-1)
	end
--	print("encode",r)
	return r
end

function decodei64(n)
	local r = (n >> 1) ~ (-(n&1))
--	print("decode",r)
	return r
end

function encodef64(n)
end

function decodei64(n)
end

function encodef32(n)
end
function decodef32(n)
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
