using System;
using System.Net;

namespace JHServerEngine
{
    public class MessageEventArgs : EventArgs
    {
        public EndPoint RemoteEndPoint { get; private set; }
        public byte[] Data { get; private set; }

        public MessageEventArgs(EndPoint remoteEndPoint, byte[] data)
        {
            this.RemoteEndPoint = remoteEndPoint;
            this.Data = data;
        }
    }
}
