using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace JHClientEngine
{
    public delegate void MessageEvent(object sender, MessageEventArgs e);

    public class TCPClient
    {
        private Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private EndPoint serverEP;

        public int BufferSize { get; private set; }

        #region 수신 스레드
        private Thread receiveThread;
        #endregion

        #region 처리할 이벤트
        public event MessageEvent MessageReceived;
        public event MessageEvent MessageSent;
        public event MessageEvent ConnectSucceed;
        public event MessageEvent ConnectFailed;
        public event MessageEvent Disconnected;
        #endregion

        public TCPClient(EndPoint serverEP, int bufferSize = 1024)
        {
            this.serverEP = serverEP;
            this.BufferSize = bufferSize;
        }

        public TCPClient(IPAddress ipAddress, int port, int bufferSize = 1024)
            : this(new IPEndPoint(ipAddress, port), bufferSize)
        {
        }

        /// <summary>
        /// 서버로 접속 시작. 성공 시 ConnectSucceed, 실패 시 ConnectFailed 이벤트 호출
        /// </summary>
        public void Start()
        {
            try
            {
                server.Connect(serverEP);
                receiveThread = new Thread(Receive);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                if (ConnectSucceed != null)
                {
                    MessageEventArgs args = new MessageEventArgs(server.RemoteEndPoint, null);
                    ConnectSucceed(this, args);
                }
            }
            catch (SocketException)
            {
                if (ConnectFailed != null)
                {
                    MessageEventArgs args = new MessageEventArgs(serverEP, null);
                    ConnectFailed(this, args);
                }
            }
        }

        private void Receive()
        {
            byte[] data = new byte[BufferSize];
            int length = 0;

            while (true)
            {
                try
                {
                    length = server.Receive(data);
                    if (length == 0) continue; // 문제 수정?

                    byte[] received = new byte[length];

                    Array.Copy(data, received, length);

                    if (MessageReceived != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(server.RemoteEndPoint, received);
                        MessageReceived(this, args);
                    }
                }
                catch (SocketException)
                {
                    if (Disconnected != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(server.RemoteEndPoint, null);
                        Disconnected(this, args);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public void Send(byte[] data)
        {
            if (server == null)
            {
                return;
            }

            Thread sendThread = new Thread(() =>
            {
                try
                {
                    server.Send(data);
                    if (MessageSent != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(server.RemoteEndPoint, data);
                        MessageSent(this, args);
                    }
                }
                catch (SocketException)
                {
                    if (Disconnected != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(server.RemoteEndPoint, null);
                        Disconnected(this, args);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            });

            sendThread.IsBackground = true;
            sendThread.Start();
        }

        public void Close()
        {
            if (server != null)
            {
                server.Close();
            }
        }
    }
}
