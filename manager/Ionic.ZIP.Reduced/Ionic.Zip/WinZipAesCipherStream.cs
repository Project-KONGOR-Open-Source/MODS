using System;
using System.IO;
using System.Security.Cryptography;

namespace Ionic.Zip;

internal class WinZipAesCipherStream : Stream
{
	private const int BLOCK_SIZE_IN_BYTES = 16;

	private WinZipAesCrypto _params;

	private Stream _s;

	private CryptoMode _mode;

	private int _nonce;

	private bool _finalBlock;

	private bool _NextXformWillBeFinal;

	internal HMACSHA1 _mac;

	internal RijndaelManaged _aesCipher;

	internal ICryptoTransform _xform;

	private byte[] counter = new byte[16];

	private byte[] counterOut = new byte[16];

	private long _length;

	private long _totalBytesXferred;

	private byte[] _PendingWriteBuffer;

	private int _pendingCount;

	public byte[] FinalAuthentication
	{
		get
		{
			if (!_finalBlock)
			{
				if (_totalBytesXferred != 0)
				{
					throw new BadStateException("The final hash has not been computed.");
				}
				byte[] buffer = new byte[0];
				_mac.ComputeHash(buffer);
			}
			byte[] array = new byte[10];
			Array.Copy(_mac.Hash, 0, array, 0, 10);
			return array;
		}
	}

	public override bool CanRead
	{
		get
		{
			if (_mode != CryptoMode.Decrypt)
			{
				return false;
			}
			return true;
		}
	}

	public override bool CanSeek => false;

	public override bool CanWrite => _mode == CryptoMode.Encrypt;

	public override long Length
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public override long Position
	{
		get
		{
			throw new NotImplementedException();
		}
		set
		{
			throw new NotImplementedException();
		}
	}

	internal WinZipAesCipherStream(Stream s, WinZipAesCrypto cryptoParams, long length, CryptoMode mode)
		: this(s, cryptoParams, mode)
	{
		_length = length;
	}

	internal WinZipAesCipherStream(Stream s, WinZipAesCrypto cryptoParams, CryptoMode mode)
	{
		_params = cryptoParams;
		_s = s;
		_mode = mode;
		_nonce = 1;
		if (_params == null)
		{
			throw new BadPasswordException("Supply a password to use AES encryption.");
		}
		int num = _params.KeyBytes.Length * 8;
		if (num != 256 && num != 128 && num != 192)
		{
			throw new ArgumentException("keysize");
		}
		_mac = new HMACSHA1(_params.MacIv);
		_aesCipher = new RijndaelManaged();
		_aesCipher.BlockSize = 128;
		_aesCipher.KeySize = num;
		_aesCipher.Mode = CipherMode.ECB;
		_aesCipher.Padding = PaddingMode.None;
		byte[] rgbIV = new byte[16];
		_xform = _aesCipher.CreateEncryptor(_params.KeyBytes, rgbIV);
		if (_mode == CryptoMode.Encrypt)
		{
			_PendingWriteBuffer = new byte[16];
		}
	}

	private int ProcessOneBlockWriting(byte[] buffer, int offset, int last)
	{
		if (_finalBlock)
		{
			throw new InvalidOperationException("The final block has already been transformed.");
		}
		int num = last - offset;
		int num2 = ((num > 16) ? 16 : num);
		Array.Copy(BitConverter.GetBytes(_nonce++), 0, counter, 0, 4);
		if (num2 == last - offset)
		{
			if (_NextXformWillBeFinal)
			{
				counterOut = _xform.TransformFinalBlock(counter, 0, 16);
				_finalBlock = true;
			}
			else if (buffer != _PendingWriteBuffer || num2 != 16)
			{
				Array.Copy(buffer, offset, _PendingWriteBuffer, _pendingCount, num2);
				_pendingCount += num2;
				_nonce--;
				return 0;
			}
		}
		if (!_finalBlock)
		{
			_xform.TransformBlock(counter, 0, 16, counterOut, 0);
		}
		for (int i = 0; i < num2; i++)
		{
			buffer[offset + i] = (byte)(counterOut[i] ^ buffer[offset + i]);
		}
		if (_finalBlock)
		{
			_mac.TransformFinalBlock(buffer, offset, num2);
		}
		else
		{
			_mac.TransformBlock(buffer, offset, num2, null, 0);
		}
		return num2;
	}

	private int ProcessOneBlockReading(byte[] buffer, int offset, int count)
	{
		if (_finalBlock)
		{
			throw new NotSupportedException();
		}
		int num = count - offset;
		int num2 = ((num > 16) ? 16 : num);
		if (_length > 0 && _totalBytesXferred + count == _length && num2 == num)
		{
			_NextXformWillBeFinal = true;
		}
		Array.Copy(BitConverter.GetBytes(_nonce++), 0, counter, 0, 4);
		if (_NextXformWillBeFinal && num2 == count - offset)
		{
			_mac.TransformFinalBlock(buffer, offset, num2);
			counterOut = _xform.TransformFinalBlock(counter, 0, 16);
			_finalBlock = true;
		}
		else
		{
			_mac.TransformBlock(buffer, offset, num2, null, 0);
			_xform.TransformBlock(counter, 0, 16, counterOut, 0);
		}
		for (int i = 0; i < num2; i++)
		{
			buffer[offset + i] = (byte)(counterOut[i] ^ buffer[offset + i]);
		}
		return num2;
	}

	private void TransformInPlace(byte[] buffer, int offset, int count)
	{
		for (int i = offset; i < buffer.Length && i < count + offset; i += 16)
		{
			if (_mode == CryptoMode.Encrypt)
			{
				ProcessOneBlockWriting(buffer, i, count + offset);
			}
			else
			{
				ProcessOneBlockReading(buffer, i, count + offset);
			}
		}
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		if (_mode == CryptoMode.Encrypt)
		{
			throw new NotSupportedException();
		}
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer");
		}
		if (offset < 0 || count < 0)
		{
			throw new ArgumentException("Invalid parameters");
		}
		if (buffer.Length < offset + count)
		{
			throw new ArgumentException("The buffer is too small");
		}
		int count2 = count;
		if (_totalBytesXferred >= _length)
		{
			return 0;
		}
		long num = _length - _totalBytesXferred;
		if (num < count)
		{
			count2 = (int)num;
		}
		int num2 = _s.Read(buffer, offset, count2);
		TransformInPlace(buffer, offset, count2);
		_totalBytesXferred += num2;
		return num2;
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		if (_mode == CryptoMode.Decrypt)
		{
			throw new NotSupportedException();
		}
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer");
		}
		if (offset < 0 || count < 0)
		{
			throw new ArgumentException("Invalid parameters");
		}
		if (buffer.Length < offset + count)
		{
			throw new ArgumentException("The offset and count are too large");
		}
		if (count == 0)
		{
			return;
		}
		if (_pendingCount != 0)
		{
			if (count + _pendingCount <= 16)
			{
				Array.Copy(buffer, offset, _PendingWriteBuffer, _pendingCount, count);
				_pendingCount += count;
				return;
			}
			int num = 16 - _pendingCount;
			Array.Copy(buffer, offset, _PendingWriteBuffer, _pendingCount, num);
			_pendingCount = 0;
			offset += num;
			count -= num;
			ProcessOneBlockWriting(_PendingWriteBuffer, 0, 16);
			_s.Write(_PendingWriteBuffer, 0, 16);
			_totalBytesXferred += 16L;
		}
		TransformInPlace(buffer, offset, count);
		_s.Write(buffer, offset, count - _pendingCount);
		_totalBytesXferred += count - _pendingCount;
	}

	public override void Close()
	{
		if (_pendingCount != 0)
		{
			_NextXformWillBeFinal = true;
			ProcessOneBlockWriting(_PendingWriteBuffer, 0, _pendingCount);
			_s.Write(_PendingWriteBuffer, 0, _pendingCount);
			_totalBytesXferred += _pendingCount;
		}
		_s.Close();
	}

	public override void Flush()
	{
		_s.Flush();
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotImplementedException();
	}

	public override void SetLength(long value)
	{
		throw new NotImplementedException();
	}
}
