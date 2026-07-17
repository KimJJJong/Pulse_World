using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerCore
{
	public class SendBufferHelper
	{
		// Thead간 경합 방지를 위한
		public static ThreadLocal<SendBuffer> CurrentBuffer = new ThreadLocal<SendBuffer>(() => { return null; });

		public static int ChunkSize { get; set; } = 65535;
		public static ArraySegment<byte> Open(int reserveSize)
		{
			if (CurrentBuffer.Value == null)
				CurrentBuffer.Value = new SendBuffer(ChunkSize);

			if (CurrentBuffer.Value.FreeSize < reserveSize)
				CurrentBuffer.Value = new SendBuffer(ChunkSize);

			return CurrentBuffer.Value.Open(reserveSize);
		}

		public static ArraySegment<byte> Close(int usedSize)
		{
			return CurrentBuffer.Value.Close(usedSize);
		}
	}

	public class SendBuffer
	{
		// [][][][][][][][][u][]
		byte[] _buffer;
		int _usedSize = 0;

		// 남은 사용 가능 공간
		public int FreeSize { get { return _buffer.Length - _usedSize; } }

		public SendBuffer(int chunkSize)
		{
			_buffer = new byte[chunkSize];
		}
        /// <summary>
        /// 버퍼를 쓰기 시작한 위치 반환
        /// </summary>
        /// <param name="reserveSize"> 예약 크기</param>
        /// <returns></returns>
        public ArraySegment<byte> Open(int reserveSize)
		{
			if (reserveSize > FreeSize)
				return null;

			return new ArraySegment<byte>(_buffer, _usedSize, reserveSize);
		}
		/// <summary>
		/// 데이터 사용후 쓰기 위치 이동
		/// </summary>
		/// <param name="usedSize"> 실 사용한 크기</param>
		/// <returns></returns>
		public ArraySegment<byte> Close(int usedSize)
		{
			ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, _usedSize, usedSize);
			_usedSize += usedSize;
			return segment;
		}
	}
}
