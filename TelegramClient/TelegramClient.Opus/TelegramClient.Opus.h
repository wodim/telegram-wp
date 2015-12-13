#pragma once



namespace TelegramClient_Opus
{
    public ref class WindowsPhoneRuntimeComponent sealed
    {
    public:
        WindowsPhoneRuntimeComponent();
		int Sum(int a, int b);
		int InitPlayer(Platform::String^ path);
		void CleanupPlayer();
		void FillBuffer(Platform::WriteOnlyArray<uint8>^ buffer, int capacity, Platform::WriteOnlyArray<int>^ args);
		int64 GetTotalPcmDuration();

		int StartRecord(Platform::String^ path);
		int WriteFrame(const Platform::Array<uint8>^ buffer, int length);
		void StopRecord();

		bool IsOpusFile(Platform::String^ path);
		//void WriteFile( String^ strFile, String^ strContent );
		//void LoadFile(String^ strFile);
    };

	
}