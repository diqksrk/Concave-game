using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MyNetworkModule
{
    public class CNetworkService
    {
        CListener client_listener;

        BufferManager bufferManager;

        SocketAsyncEventArgsPool receive_event_args_pool;
        SocketAsyncEventArgsPool send_event_args_pool;

        public delegate void SessionHandler(CUserToken token);
        public SessionHandler session_created_callback { get; set; }

        int connected_count;
        int max_count;

        int max_connections;
        int buffer_size;
        readonly int pre_alloc_count = 2;

        public CNetworkService()
        {
            this.connected_count = 0;
            this.session_created_callback = null;
        }

        public void initialize()
        {
            this.max_connections = 10000;
            this.buffer_size = 1024;

            this.bufferManager = new BufferManager(this.max_connections * this.pre_alloc_count * this.buffer_size, this.buffer_size);
            this.receive_event_args_pool = new SocketAsyncEventArgsPool(this.max_count);
            this.send_event_args_pool = new SocketAsyncEventArgsPool(this.max_count);

            this.bufferManager.InitBuffer();

            SocketAsyncEventArgs arg;

            for (int i=0; i<max_connections; i++)
            {

                CUserToken token = new CUserToken();
                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_complted);
                    arg.UserToken = token;

                    receive_event_args_pool.Push(arg);
                    this.bufferManager.SetBuffer(arg);
                }

                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
                    arg.UserToken = token;

                    send_event_args_pool.Push(arg);
                    this.bufferManager.SetBuffer(arg);
                }
            }

        }

        void on_new_client(Socket client_socket, object token)
        {
            SocketAsyncEventArgs receive_args = this.receive_event_args_pool.Pop();
            SocketAsyncEventArgs send_args = this.send_event_args_pool.Pop();

            receive_args.Completed += new EventHandler<SocketAsyncEventArgs>(receive_complted);

            CUserToken user_token = null;
            if (this.session_created_callback != null)
            {
                user_token = receive_args.UserToken as CUserToken;
                this.session_created_callback(user_token);
            }

            begin_receive(client_socket, receive_args, send_args);
        }



        void begin_receive(Socket socket, SocketAsyncEventArgs receive_args, SocketAsyncEventArgs send_args)
        {
            CUserToken token = receive_args.UserToken as CUserToken;

            token.socket = socket;
            bool pending = socket.ReceiveAsync(receive_args);
            if (!pending)
            {
                process_receive(receive_args);    
            }
        }

        void send_completed(object sender, SocketAsyncEventArgs e)
        {
            CUserToken token = e.UserToken as CUserToken;
            token.process_send(e);
        }

        void receive_complted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                process_receive(e);
                return;
            }
            throw new ArgumentException("The last operation completed on the socket was not a receive.");
        }

        public void listen(string host, int port, int backlog)
        {
            this.client_listener = new CListener();
            this.client_listener.callback_on_newclient += on_new_client;
            this.client_listener.start(host, port, backlog);
        }

        private void process_receive(SocketAsyncEventArgs e)
        {
            CUserToken token = e.UserToken as CUserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                token.on_receive(e.Buffer, e.Offset, e.BytesTransferred);

                bool pending = token.socket.ReceiveAsync(e);
                if (!pending)
                {
                    process_receive(e);
                }
            }
            else
            {
                Console.WriteLine(string.Format("error {0},  transferred {1}", e.SocketError, e.BytesTransferred));
                close_clientsocket(token);
            }
        }

        public void close_clientsocket(CUserToken token)
        {
            token.on_removed();
            
            if (this.receive_event_args_pool != null)
            {
                this.receive_event_args_pool.Push(token.receive_event_args);
            }

            if (this.send_event_args_pool != null)
            {
                this.send_event_args_pool.Push(token.send_event_args);
            }
        }
    }
}
