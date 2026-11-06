using System;
using System.Runtime.InteropServices;

namespace WinCanvas {
 // Minimal Media Foundation interop: a Source Reader that decodes any video Windows can play (MP4, MOV, WMV,
 // MKV with the right extensions) into 32-bit RGB frames. Only the methods WinDotra calls are typed; the rest keep
 // their vtable slots as placeholders.
 [StructLayout(LayoutKind.Sequential)] struct PropVariant { public ushort Type,R1,R2,R3; public long Value,Pad; }
 [ComImport,Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IMFAttributes {
  void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64(); void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
  [PreserveSig] int SetUINT32([In] ref Guid key,int value);
 }
 [ComImport,Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IMFMediaType {
  void GetItem(); void GetItemType(); void CompareItem(); void Compare();
  [PreserveSig] int GetUINT32([In] ref Guid key,out int value);
  [PreserveSig] int GetUINT64([In] ref Guid key,out long value);
  void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
  [PreserveSig] int SetUINT32([In] ref Guid key,int value);
  [PreserveSig] int SetUINT64([In] ref Guid key,long value);
  void SetDouble();
  [PreserveSig] int SetGUID([In] ref Guid key,[In] ref Guid value);
 }
 [ComImport,Guid("045FA593-8799-42b8-BC8D-8968C6453507"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IMFMediaBuffer {
  [PreserveSig] int Lock(out IntPtr buffer,out int max,out int current);
  [PreserveSig] int Unlock();
 }
 [ComImport,Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IMFSample {
  void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64(); void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString(); void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems(); void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID(); void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
  void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime(); void SetSampleTime(); void GetSampleDuration(); void SetSampleDuration(); void GetBufferCount(); void GetBufferByIndex();
  [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
 }
 [ComImport,Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IMFSourceReader {
  void GetStreamSelection();
  [PreserveSig] int SetStreamSelection(int stream,[MarshalAs(UnmanagedType.Bool)] bool selected);
  void GetNativeMediaType();
  [PreserveSig] int GetCurrentMediaType(int stream,out IMFMediaType type);
  [PreserveSig] int SetCurrentMediaType(int stream,IntPtr reserved,IMFMediaType type);
  [PreserveSig] int SetCurrentPosition([In] ref Guid format,[In] ref PropVariant position);
  [PreserveSig] int ReadSample(int stream,int flags,out int actual,out int streamFlags,out long timestamp,out IMFSample sample);
 }

 // Decodes one video file, looping, muted. Frames come out as bottom-up 32-bit BGRX in Frame (a classic DIB).
 public class VideoDecoder : IDisposable {
  [DllImport("mfplat.dll")] static extern int MFStartup(int version,int flags);
  [DllImport("mfplat.dll")] static extern int MFShutdown();
  [DllImport("mfplat.dll")] static extern int MFCreateAttributes(out IMFAttributes attributes,int size);
  [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out IMFMediaType type);
  [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode)] static extern int MFCreateSourceReaderFromURL(string url,IMFAttributes attributes,out IMFSourceReader reader);
  [DllImport("kernel32.dll",EntryPoint="RtlMoveMemory")] static extern void Copy(IntPtr destination,IntPtr source,IntPtr length);
  static Guid AdvancedProcessing=new Guid("0f81da2c-b537-4672-a8b2-a681b17307a3"),SimpleProcessing=new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d"),Hardware=new Guid("a634a91c-822b-41b9-a494-4de4643612b0"),MajorType=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),SubType=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
  static Guid Video=new Guid("73646976-0000-0010-8000-00AA00389B71"),Rgb32=new Guid("00000016-0000-0010-8000-00AA00389B71"),FrameSize=new Guid("1652c33d-d6b2-4012-b834-72030849a37d"),Stride=new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"),FrameRate=new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
  const int FirstVideo=-4,AllStreams=-2,EndOfStream=2;
  IMFSourceReader reader; bool started;
  public int Width,Height; public IntPtr Frame; public double DecodeMilliseconds; public double FrameSeconds=1/30.0; int stride;
  double start=-1,next; public bool HasFrame;
  static void Check(int hr,string what){if(hr<0)throw new InvalidOperationException("Windows couldn't play this video ("+what+", 0x"+hr.ToString("X8")+"). Try an MP4 (H.264) file.");}
  public VideoDecoder(string path) {
   Check(MFStartup(0x00020070,0),"startup");started=true;
   IMFAttributes attributes;Check(MFCreateAttributes(out attributes,2),"attributes");var key=SimpleProcessing;attributes.SetUINT32(ref key,1);var gpu=Hardware;attributes.SetUINT32(ref gpu,1);
   Check(MFCreateSourceReaderFromURL(path,attributes,out reader),"open");Marshal.ReleaseComObject(attributes);
   reader.SetStreamSelection(AllStreams,false);Check(reader.SetStreamSelection(FirstVideo,true),"no video stream");
   IMFMediaType native;Check(reader.GetCurrentMediaType(FirstVideo,out native),"format");long size;var fs=FrameSize;native.GetUINT64(ref fs,out size);long rate;var fr=FrameRate;
   if(native.GetUINT64(ref fr,out rate)>=0){long num=rate>>32,den=rate&0xFFFFFFFF;if(num>0&&den>0)FrameSeconds=Math.Max(1/120.0,Math.Min(0.5,den/(double)num));}
   Marshal.ReleaseComObject(native);
   int w=(int)(size>>32),h=(int)(size&0xFFFFFFFF);
   IMFMediaType output;Check(MFCreateMediaType(out output),"output");var mt=MajorType;var vid=Video;output.SetGUID(ref mt,ref vid);var st=SubType;var rgb=Rgb32;output.SetGUID(ref st,ref rgb);
   Check(reader.SetCurrentMediaType(FirstVideo,IntPtr.Zero,output),"convert to RGB");Marshal.ReleaseComObject(output);
   IMFMediaType actual;Check(reader.GetCurrentMediaType(FirstVideo,out actual),"output format");actual.GetUINT64(ref fs,out size);Width=(int)(size>>32);Height=(int)(size&0xFFFFFFFF);
   int s;var sk=Stride;stride=actual.GetUINT32(ref sk,out s)>=0&&s!=0?s:Width*4;Marshal.ReleaseComObject(actual);
   if(Width<=0||Height<=0)throw new InvalidOperationException("That video has no picture.");
   Frame=Marshal.AllocHGlobal(Width*Height*4);
  }
  // Reads frames until the video catches up with the clock. Returns true when a new frame is in Frame.
  public bool Update(double now) {
   if(start<0){start=now;next=0;}
   double local=now-start;bool changed=false;
   // At most two frames per tick: a video too heavy for this PC plays a little slower instead of stalling the desktop.
   for(int guard=0;guard<2&&local>=next;guard++){
    if(!Read()){var zero=Guid.Empty;var position=new PropVariant{Type=20};reader.SetCurrentPosition(ref zero,ref position);start=now;local=0;next=0;if(!Read())break;}
    changed=true;
   }
   return changed;
  }
  bool Read() {
   int actualStream,flags;long time;IMFSample sample;var watch=System.Diagnostics.Stopwatch.StartNew();
   try{return ReadOne(out actualStream,out flags,out time,out sample);}finally{DecodeMilliseconds=DecodeMilliseconds*0.8+watch.Elapsed.TotalMilliseconds*0.2;}
  }
  bool ReadOne(out int actualStream,out int flags,out long time,out IMFSample sample) {
   if(reader.ReadSample(FirstVideo,0,out actualStream,out flags,out time,out sample)<0)return false;
   if((flags&EndOfStream)!=0){if(sample!=null)Marshal.ReleaseComObject(sample);return false;}
   if(sample==null){next+=FrameSeconds;return true;}
   try {
    IMFMediaBuffer buffer;if(sample.ConvertToContiguousBuffer(out buffer)<0)return true;
    try {
     IntPtr data;int max,length;if(buffer.Lock(out data,out max,out length)<0)return true;
     try {
      int row=Width*4,pitch=Math.Abs(stride);if(pitch<row)pitch=row;
      if((long)pitch*Height>length&&stride>0)pitch=row;
      for(int y=0;y<Height;y++){long offset=stride>0?(long)y*pitch:(long)(Height-1-y)*pitch;if(offset+row>length)break;Copy(Frame+(Height-1-y)*row,data+(int)offset,new IntPtr(row));}
      HasFrame=true;
     } finally { buffer.Unlock(); }
    } finally { Marshal.ReleaseComObject(buffer); }
   } finally { Marshal.ReleaseComObject(sample); }
   next=time/1e7+FrameSeconds;return true;
  }
  public void Dispose(){if(reader!=null){Marshal.ReleaseComObject(reader);reader=null;}if(Frame!=IntPtr.Zero){Marshal.FreeHGlobal(Frame);Frame=IntPtr.Zero;}if(started){MFShutdown();started=false;}}
 }
}
