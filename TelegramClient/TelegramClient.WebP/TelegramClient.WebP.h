#pragma once

namespace TelegramClient_WebP
{
    namespace LibWebP
	{
		public enum class DecodeType
		{
			RGB,
			RGBA,
			rgbA,
			BGR,
			BGRA,
			YUV
		};

		public ref class WebPDecoder sealed
		{
		public:
			WebPDecoder();
			Platform::String^ GetDecoderVersion();
			bool GetInfo(const Platform::Array<unsigned char>^ Data, int* Width, int* Height);
			Platform::Array<unsigned char>^ Decode(DecodeType type, const Platform::Array<unsigned char>^ Data, int* Width, int* Height); 
			Platform::Array<unsigned char>^ DecodeRgbA(const Platform::Array<unsigned char>^ Data, int* Width, int* Height);
			//Platform::Array<unsigned int>^ WebPDecoder::DecodeToWritableBitmap(const Platform::Array<unsigned char>^ Data, int* Width, int* Height);
			//Platform::Array<int>^ Decode
		};
	}
}