-- ����
-- proto type
PROTO_BOOL = 1
PROTO_S64 = 2
PROTO_U64 = 3
PROTO_F32 = 4
PROTO_F64 = 5
PROTO_STR = 6
PROTO_MSG = 7
-- proto container
PROTO_NIL = 10
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
	-- ����
	if super then
		cls.super = function(...) super.ctor(...) end
		cls_meta.__index = super
	end
	cls.new = function(...)
		-- ����ʵ��
		local instance = setmetatable({}, cls)
		-- ���ù��캯��,û�еݹ����
		if cls.ctor then
			cls.ctor(instance, ...)
		end
		return instance
	end
	return cls
end

-- ���л�
function proto_encode(stream, msg)
	local desc = msg.pt_desc
	local last_id = 0
	for id, info in ipairs(desc) do
		local field = msg[info.name]
		if field != nil then
			-- ��ֵ����??
			-- ��������д��
		end
	end
end

function proto_decode(stream, msg)
end
