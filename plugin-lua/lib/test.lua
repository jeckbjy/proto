require("proto")
---------------------------------------------------------------
-- Test
---------------------------------------------------------------
function factory(msgid)
end

function test()
	local buf = Buffer.new()
	local encoder = Encoder.new(buf)
	local decoder = Decoder.new(buf)
	local msg = LoginMsg.new()
	encoder.encode(msg)
	local ret_msg = decoder.decode(factory)
end