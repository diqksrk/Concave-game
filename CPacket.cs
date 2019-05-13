using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyNetworkModule
{
    public class CPacket
    {
        public int position { get; private set; }
        public byte[] buffer { get; private set; }
        public Int16 protocol_id { get; private set; }

        public CPacket()
        {
            this.buffer = new byte[1024];
        }

        public void record_size()
        {
            Int16 body_size = (Int16)(this.position - Defines.HEADERSIZE);
            byte[] header = BitConverter.GetBytes(body_size);
            header.CopyTo(this.buffer, 0);
        }

        public void copy_to(CPacket target)
        {
            target.set_protocol(this.protocol_id);
            target.overwrite(this.buffer, this.position);
        }

        public void overwrite(byte[] source, int position)
        {
            Array.Copy(source, this.buffer, source.Length);
            this.position = position;
        }

        public void set_protocol(Int16 protocol_id)
        {
            this.protocol_id = protocol_id;
            //this.buffer = new byte[1024];

            // 헤더는 나중에 넣을것이므로 데이터 부터 넣을 수 있도록 위치를 점프시켜놓는다.
            this.position = Defines.HEADERSIZE;

            push_int16(protocol_id);
        }

        public void push_int16(Int16 data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.buffer, this.position);
            this.position += temp_buffer.Length;
        }
    }
}
