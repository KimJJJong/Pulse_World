using System;
using System.Collections.Generic;
using System.Text;

namespace ServerCore
{
	public class RecvBuffer
	{
		// [r][][w][][][][][][][]
		ArraySegment<byte> _buffer;
		int _readPos;	// Read Start Position
		int _writePos;	// Write Position

		public RecvBuffer(int bufferSize)
		{
			_buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
		}

		public int DataSize { get { return _writePos - _readPos; } }	// Data Size can Read : 얼마나 데이터가 쌓여 있는가?
		public int FreeSize { get { return _buffer.Count - _writePos; } }	// 버퍼에서 데이터를 더 쓸 수 있는 공간 : 남은 공간

		public ArraySegment<byte> ReadSegment
		{
			get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize); }
		}

		public ArraySegment<byte> WriteSegment
		{
			get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize); }
		}
		/// <summary>
		///  읽은 데이터를 제거하고, 읽지 않은 데이터를 앞으로 이동시켜 공간을 재활용 [][][r][w][][][][] -> [r][w][][][][][][]
		/// </summary>
		public void Clean()
		{
			int dataSize = DataSize;
			//	r과 w가 겹친다 : 읽을 Data가 없다
			if (dataSize == 0)
			{
				// 남은 데이터가 없으면 복사하지 않고 커서 위치만 리셋
				_readPos = _writePos = 0;
			}
			else
			{
				// 남은 찌끄레기가 있으면 시작 위치로 복사
				Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
				_readPos = 0;
				_writePos = dataSize;
			}
		}
		/// <summary>
		/// numOfBytes 크기의 데이터를 읽은 만큼 _readPos를 이동
		/// </summary>
		/// <param name="numOfBytes"></param>
		/// <returns></returns>
		public bool OnRead(int numOfBytes)
		{
			if (numOfBytes > DataSize)
				return false;

			_readPos += numOfBytes;
			return true;
		}
		/// <summary>
		/// 데이터를 쓴 만큼 _writePos이동
		/// </summary>
		/// <param name="numOfBytes"></param>
		/// <returns></returns>
		public bool OnWrite(int numOfBytes)
		{
			if (numOfBytes > FreeSize)
				return false;

			_writePos += numOfBytes;
			return true;
		}
	}
}
