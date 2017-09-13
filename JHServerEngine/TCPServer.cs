using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace JHServerEngine
{
    public delegate void MessageEvent(object sender, MessageEventArgs e);

    public class TCPServer
    {
        private Socket socket;
        private EndPoint endPoint;
        private Thread acceptThread;

        public List<Socket> ClientList { get; private set; } = new List<Socket>();
        public int BufferSize { get; private set; }

        private bool isExit = false; // 스레드 종료 플래그

        #region 처리할 각종 이벤트
        public event MessageEvent MessageReceived;
        public event MessageEvent MessageSent;
        public event MessageEvent NewClientConnected;
        public event MessageEvent ClientDisconneted;
        #endregion

        /// <summary>
        /// TCP 소켓 통신을 위한 서버
        /// </summary>
        /// <param name="port">통신을 위한 포트 번호</param>
        /// <param name="bufferSize">한 번에 전송 가능한 최대 버퍼 크기(바이트 수)</param>
        public TCPServer(int port, int bufferSize = 1024)
        {
            this.endPoint = new IPEndPoint(IPAddress.Any, port);
            this.BufferSize = bufferSize;
        }

        /// <summary>
        /// TCP 소켓 통신을 시작함
        /// </summary>
        /// <param name="backlog">접속 대기 큐의 길이</param>
        public void Start(int backlog)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            isExit = false;

            try
            {
                // 소켓 바인드 및 접속 대기
                socket.Bind(endPoint);
                socket.Listen(backlog);

                // 접속 요청을 받아들이는 스레드 생성 및 실행
                acceptThread = new Thread(AcceptClient);
                acceptThread.IsBackground = true;
                acceptThread.Start();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 접속된 모든 클라이언트에게 데이터를 보냄
        /// </summary>
        /// <param name="data"></param>
        public void SendToAll(byte[] data)
        {
            // 현재 접속 중인 모든 클라이언트에게 데이터 전송
            foreach (Socket c in ClientList)
            {
                try
                {
                    c.Send(data);
                    if (MessageSent != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(c.RemoteEndPoint, data);
                        MessageSent(this, args);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private void AcceptClient()
        {
            Socket newSocket = null;
            while (!isExit)
            {
                try
                {
                    // 새 클라이언트 접속 시 리스트에 추가
                    newSocket = socket.Accept();
                    ClientList.Add(newSocket);

                    // 접속한 클라이언트로부터 메시지를 수신할 스레드를 생성하고 실행
                    Thread processThread = new Thread(new ParameterizedThreadStart(ReceiveFromClient));
                    processThread.IsBackground = true;
                    processThread.Start(newSocket);

                    // 새 클라이언트 연결 이벤트 호출
                    if (NewClientConnected != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(newSocket.RemoteEndPoint, null);
                        NewClientConnected(this, args);
                    }
                }
                catch (SocketException ex)
                {
                    throw ex;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            // 종료플래그가 켜져있으면 서버 소켓 폐기
            if (isExit)
            {
                socket.Dispose();
            }
        }

        private void ReceiveFromClient(object obj)
        {
            Socket client = obj as Socket;
            byte[] data = new byte[BufferSize];
            int length = 0;

            while (!isExit)
            {
                try
                {
                    //클라이언트로부터 데이터를 읽어들임
                    length = client.Receive(data);

                    //읽어들인 데이터의 길이 만큼 바이트 배열을 만들고, 배열 값 복사
                    byte[] received = new byte[length];
                    Array.Copy(data, received, length);

                    MessageEventArgs args = new MessageEventArgs(client.RemoteEndPoint, received);
                    if (MessageReceived != null)
                    {
                        MessageReceived(this, args);
                    }

                }
                catch (SocketException)
                {
                    //중간에 연결이 끊기면 리스트에서 제거
                    ClientList.Remove(client);

                    if (ClientDisconneted != null)
                    {
                        MessageEventArgs args = new MessageEventArgs(client.RemoteEndPoint, null);
                        ClientDisconneted(this, args);
                    }

                    //Thread.CurrentThread.Abort(); // 필요한지?
                    return;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            //종료 플래그가 켜져있으면 클라이언트 소켓 폐기
            if (isExit)
            {
                client.Dispose();
            }

        }

        public void Close()
        {
            try
            {
                if (socket != null)
                {
                    
                    socket.Dispose();
                    acceptThread.Interrupt();
                    socket = null;
                    isExit = true;
                }
            }
            catch(Exception e)
            {
                throw e;
            }
        }
    }


}
