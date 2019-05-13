using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace MyNetworkModule
{
    public class CUserToken
    {
        public Socket socket { get; set; }

        CMessageResolver message_resolver;

        Queue<CPacket> sending_queue;

        private object cs_sending_queue;

        IPeer peer;

        public SocketAsyncEventArgs receive_event_args { get; private set; }
        public SocketAsyncEventArgs send_event_args { get; private set; }

        public void on_receive(byte[] buffer, int offset, int transfered)
        {
            this.message_resolver.on_receive(buffer, offset, transfered, on_message);
        }

        public void set_peer(IPeer peer)
        {
            this.peer = peer;
        }

        public void send(CPacket msg)
        {
            CPacket clone = new CPacket();
            msg.copy_to(clone);

            lock (this.cs_sending_queue)
            {
                // 큐가 비어 있다면 큐에 추가하고 바로 비동기 전송 매소드를 호출한다.
                if (this.sending_queue.Count <= 0)
                {
                    this.sending_queue.Enqueue(clone);
                    start_send();
                    return;
                }

                // 큐에 무언가가 들어 있다면 아직 이전 전송이 완료되지 않은 상태이므로 큐에 추가만 하고 리턴한다.
                // 현재 수행중인 SendAsync가 완료된 이후에 큐를 검사하여 데이터가 있으면 SendAsync를 호출하여 전송해줄 것이다.
                Console.WriteLine("Queue is not empty. Copy and Enqueue a msg. protocol id : " + msg.protocol_id);
                this.sending_queue.Enqueue(clone);
            }
        }

        void start_send()
        {
            lock (this.cs_sending_queue)
            {
                CPacket msg = this.sending_queue.Peek();
                msg.record_size();

                this.send_event_args.SetBuffer(this.send_event_args.Offset, msg.position);
                Array.Copy(msg.buffer, 0, this.send_event_args.Buffer, this.send_event_args.Offset, msg.position);

                bool pending = this.socket.SendAsync(this.send_event_args);

                if (!pending)
                {
                    process_send(this.send_event_args);
                }
            }
        }




        static int sent_count = 0;
        static object cs_count = new object();
        public void process_send(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                //Console.WriteLine(string.Format("Failed to send. error {0}, transferred {1}", e.SocketError, e.BytesTransferred));
                return;
            }

            lock (this.cs_sending_queue)
            {
                // count가 0이하일 경우는 없겠지만...
                if (this.sending_queue.Count <= 0)
                {
                    throw new Exception("Sending queue count is less than zero!");
                }
                int size = this.sending_queue.Peek().position;
                if (e.BytesTransferred != size)
                {
                    string error = string.Format("Need to send more! transferred {0},  packet size {1}", e.BytesTransferred, size);
                    Console.WriteLine(error);
                    return;
                }
                lock (cs_count)
                {
                    ++sent_count;
                    //if (sent_count % 20000 == 0)
                    {
                        Console.WriteLine(string.Format("process send : {0}, transferred {1}, sent count {2}",
                            e.SocketError, e.BytesTransferred, sent_count));
                    }
                }

                this.sending_queue.Dequeue();


                // 아직 전송하지 않은 대기중인 패킷이 있다면 다시한번 전송을 요청한다.
                if (this.sending_queue.Count > 0)
                {
                    start_send();
                }
            }
        }

        void on_message(Const<byte[]> buffer)
        {
            if (this.peer != null)
            {
                this.peer.on_message(buffer);
            }
        }

        public void on_removed()
        {
            this.sending_queue.Clear();

            if (this.peer != null)
            {
                this.peer.on_removed();
            }
        }
    }
}
