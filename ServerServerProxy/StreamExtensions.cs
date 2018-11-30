using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerServerProxy
{
    static class StreamExtensions
    {


        public async static Task CopyToAsync(this WebSocket source, Stream destination, int bufferSize = 81920)
        {
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize", ("ArgumentOutOfRange_NeedPosNum"));
            }
            //if (!source.CanRead && !source.CanWrite)
            //{
            //    throw new ObjectDisposedException(null, ("ObjectDisposed_StreamClosed"));
            //}
            //if (!destination.State.CanRead && !destination.CanWrite)
            //{
            //    throw new ObjectDisposedException("destination", ("ObjectDisposed_StreamClosed"));
            //}
            //if (!source.CanRead)
            //{
            //    throw new NotSupportedException(("NotSupported_UnreadableStream"));
            //}
            if (!destination.CanWrite)
            {
                throw new NotSupportedException(("NotSupported_UnwritableStream"));
            }
            await CopyToAsyncInternal(source, destination, bufferSize);
        }



        public async static Task CopyToAsync(this Stream source, WebSocket destination, int bufferSize = 81920)
        {
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize", ("ArgumentOutOfRange_NeedPosNum"));
            }
            if (!source.CanRead && !source.CanWrite)
            {
                throw new ObjectDisposedException(null, ("ObjectDisposed_StreamClosed"));
            }
            //if (!destination.State.CanRead && !destination.CanWrite)
            //{
            //    throw new ObjectDisposedException("destination", ("ObjectDisposed_StreamClosed"));
            //}
            if (!source.CanRead)
            {
                throw new NotSupportedException(("NotSupported_UnreadableStream"));
            }
            //if (!destination.CanWrite)
            //{
            //    throw new NotSupportedException(("NotSupported_UnwritableStream"));
            //}
            await CopyToAsyncInternal(source, destination, bufferSize);
        }


        private static async Task CopyToAsyncInternal(Stream source, WebSocket destination, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await destination.SendAsync(new ArraySegment<byte>(buffer, 0, bytesRead), WebSocketMessageType.Binary, false, CancellationToken.None);
            }
        }

        private static async Task CopyToAsyncInternal(WebSocket source, Stream destination, int bufferSize)
        {
            var buffer = new ArraySegment<byte>(new byte[bufferSize]);
            int bytesRead;
            
            while ((bytesRead = (await source.ReceiveAsync(buffer, CancellationToken.None)).Count) != 0)
            {
                await destination.WriteAsync(buffer.ToArray(), 0, bytesRead);
            }
        }
    }
}
