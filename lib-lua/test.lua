require("proto")
---------------------------------------------------------------
-- Test
---------------------------------------------------------------
LoginMsg = proto_class("LoginMsg")
LoginMsg.proto_id = 1
LoginMsg.proto_desc = {
	[1] = { kind = PROTO_NIL, ftype = PROTO_U64, ktype = PROTO_NIL, name = "uid", }
}
function factory(msgid)
	if msgid == 1 then
		return LoginMsg
	end
end

function test()
	local buf = Buffer.new()
	local encoder = Encoder.new(buf)
	local decoder = Decoder.new(buf)
	local msg = LoginMsg.new()
	msg.uid = 10
	encoder:encode(msg)
	print(buf:info())
	print(buf:tohex())
	local ret_msg = decoder:decode(factory)
	buf:info()
	proto_dump(ret_msg)
end

test()