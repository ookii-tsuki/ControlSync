using ControlSync;
using ControlSync.Client;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D2D = SharpDX.Direct2D1;
using Device = SharpDX.Direct3D11.Device;
using Factory1 = SharpDX.DXGI.Factory1;

namespace SIPSorceryMedia.DirectX
{

    // I had to rewrite class because the `ExternalVideoSourceRawSample` method
    // was commented out for some reason but i didn't get any problems with it so far
    public class ScreenSource : IVideoSource, IDisposable
    {
        public ILogger logger = SIPSorcery.LogFactory.CreateLogger<ScreenSource>();

        public static readonly List<VideoFormat> _supportedFormats = new List<VideoFormat>
            {
                new VideoFormat(VideoCodecsEnum.VP8, 96),
                new VideoFormat(VideoCodecsEnum.H264, 100)
            };

        private FFmpegVideoEncoder1 _ffmpegEncoder;

        private MediaFormatManager<VideoFormat> _videoFormatManager;
        private bool _isStarted;
        private bool _isPaused;
        private bool _isClosed;
        private bool _forceKeyFrame;


#pragma warning disable CS0067
        public event VideoSinkSampleDecodedDelegate? OnVideoSinkDecodedSample;
#pragma warning restore CS0067

        public event VideoSinkSampleDecodedFasterDelegate? OnVideoSinkDecodedSampleFaster;
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;
        public event RawVideoSampleDelegate OnVideoSourceRawSample;
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
        public event SourceErrorDelegate OnVideoSourceError;

        public static bool downscale = true;
        // # of graphics card adapter
        const int numAdapter = 0;

        // # of output device (i.e. monitor)
        const int numOutput = 0;

        private Factory1 factory;
        private D2D.Factory1 d2dFactory;
        private Adapter1 adapter;
        private Device device;
        private SharpDX.DXGI.Device dxgiDevice;
        private D2D.Device d2dDevice;
        private D2D.DeviceContext frameDc;
        private Output output;
        private Output1 output1;
        private int streamWidth;
        private int streamHeight;
        private OutputDuplication duplicatedOutput;
        private Texture2D screenTexture;

        private Thread screenshareThread;

#pragma warning disable CS0067
        //public event EncodedSampleDelegate? OnVideoSourceEncodedSample;
        //public event RawExtVideoSampleDelegate? OnVideoSourceRawExtSample;
        //public event RawVideoSampleDelegate? OnVideoSourceRawSample;
        //public event SourceErrorDelegate? OnVideoSourceError;
#pragma warning restore CS0067

        public ScreenSource(Dictionary<string, string>? encoderOptions = null)
        {
            _videoFormatManager = new MediaFormatManager<VideoFormat>(_supportedFormats);

            _ffmpegEncoder = new FFmpegVideoEncoder1(encoderOptions, GetAvailableHW());
            _ffmpegEncoder.SetThreadCount(1);

            downscale = false;
        }

        public ScreenSource(int width, int height, Dictionary<string, string>? encoderOptions = null)
        {
            _videoFormatManager = new MediaFormatManager<VideoFormat>(_supportedFormats);

            _ffmpegEncoder = new FFmpegVideoEncoder1(encoderOptions, GetAvailableHW());
            _ffmpegEncoder.SetThreadCount(1);

            downscale = true;

            streamWidth = width;
            streamHeight = height;
        }

        private AVHWDeviceType GetAvailableHW()
        {
            var type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                return type;
            }
            return AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
        }


        public void RestrictFormats(Func<VideoFormat, bool> filter) => _videoFormatManager.RestrictFormats(filter);
        public List<VideoFormat> GetVideoSourceFormats() => _videoFormatManager.GetSourceFormats();
        public void SetVideoSourceFormat(VideoFormat videoFormat) => _videoFormatManager.SetSelectedFormat(videoFormat);
        public void ForceKeyFrame() => _forceKeyFrame = true;
        public bool HasEncodedVideoSubscribers() => OnVideoSinkDecodedSampleFaster != null;
        public bool IsVideoSourcePaused() => _isPaused;

        private void Init()
        {

            if (!Client.isHost)
                return;

            // Create DXGI Factory1
            factory = new Factory1();
            adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            device = new Device(adapter, DeviceCreationFlags.BgraSupport);

            // Get DXGI.Output
            output = adapter.GetOutput(numOutput);
            output1 = output.QueryInterface<Output1>();

            // Duplicate the output
            duplicatedOutput = output1.DuplicateOutput(device);

            dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>();

            d2dFactory = new D2D.Factory1();

            d2dDevice = new D2D.Device(d2dFactory, dxgiDevice);

            frameDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations);


            // Width/Height of desktop to capture
            if (!downscale)
            {
                streamWidth = output.Description.DesktopBounds.Right;
                streamHeight = output.Description.DesktopBounds.Bottom;
            }

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            screenTexture = new Texture2D(device, textureDesc);


            screenshareThread = new Thread(SendScreenBuffer);


            screenshareThread.Start();
        }

        /// <summary>
        /// Closes the screen capture and encoding components.
        /// </summary>
        public void Close()
        {
            factory?.Dispose();
            adapter?.Dispose();
            device?.Dispose();
            output?.Dispose();
            output1?.Dispose();
            duplicatedOutput?.Dispose();
        }

        /// <summary>
        /// This method is called on a separate thread to capture the screen and send it to the remote peer.
        /// </summary>
        private void SendScreenBuffer()
        {
            while (!_isClosed)
            {

                byte[] rgbBuffer = GetScreenShot();

                if (rgbBuffer == null) continue;


                try
                {
                    ExternalVideoSourceRawSample(16, streamWidth, streamHeight, rgbBuffer, VideoPixelFormatsEnum.Rgb);
                }
                catch { }

                Thread.Sleep(1);
            }
        }

        private byte[] GetScreenShot()
        {

            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time
                duplicatedOutput.TryAcquireNextFrame(5, out duplicateFrameInformation, out screenResource);

                if (screenResource == null)
                    return null;



                byte[] rgbBuffer = null;

                if (downscale)
                    rgbBuffer = ToByteArrayAndDownscale(screenResource);
                else
                    rgbBuffer = ToByteArrayNoDownscale(screenResource);



                try
                {
                    screenResource.Dispose();
                    duplicatedOutput.ReleaseFrame();
                }
                catch { }


                return rgbBuffer;


            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    ClientPg.Log(e.Message);
                }
            }


            return null;
        }

        private byte[] ToByteArrayNoDownscale(SharpDX.DXGI.Resource screenResource)
        {
            // copy resource into memory that can be accessed by the CPU
            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

            // Get the desktop capture texture
            var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

            // Allocate an RGB buffer
            int width = streamWidth;
            int height = streamHeight;
            int stride = width * 3; // 3 bytes per pixel in RGB format
            byte[] rgbBuffer = new byte[height * stride];

            // Copy the data from the texture to the RGB buffer
            unsafe
            {
                byte* sourcePtr = (byte*)mapSource.DataPointer;
                Parallel.For(0, height, y =>
                {
                    int yStride = y * stride;
                    int yPitch = y * mapSource.RowPitch;
                    for (int x = 0; x < width; x++)
                    {
                        int x4 = x * 4;
                        int x3 = x * 3;
                        rgbBuffer[yStride + x3] = sourcePtr[yPitch + x4]; // Red
                        rgbBuffer[yStride + x3 + 1] = sourcePtr[yPitch + x4 + 1]; // Green
                        rgbBuffer[yStride + x3 + 2] = sourcePtr[yPitch + x4 + 2]; // Blue
                    }
                });
            }

            device.ImmediateContext.UnmapSubresource(screenTexture, 0);

            return rgbBuffer;
        }

        private byte[] ToByteArrayAndDownscale(SharpDX.DXGI.Resource screenResource)
        {
            using var frameSurface = screenResource.QueryInterface<Surface>();
            using var frameBitmap = new D2D.Bitmap1(frameDc, frameSurface);


            // create a GPU resized texture/surface/bitmap
            var desc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None, // only GPU
                BindFlags = BindFlags.RenderTarget, // to use D2D
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            using var texture = new Texture2D(device, desc);

            using var textureDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations); // create a D2D device context
            using var textureSurface = texture.QueryInterface<Surface>(); // this texture is a DXGI surface
            using var textureBitmap = new D2D.Bitmap1(textureDc, textureSurface); // we can create a GPU bitmap on a DXGI surface

            // associate the DC with the GPU texture/surface/bitmap
            textureDc.Target = textureBitmap;

            // this is were we draw on the GPU texture/surface
            textureDc.BeginDraw();

            // this will automatically resize
            textureDc.DrawBitmap(
                frameBitmap,
                new RawRectangleF(0, 0, streamWidth, streamHeight),
                1,
                D2D.InterpolationMode.Linear, // change this for quality vs speed
                null,
                null);

            // commit draw
            textureDc.EndDraw();


            using var texture2dc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations); // create a D2D device context

            var desc2 = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read, // only GPU
                BindFlags = BindFlags.None, // to use D2D
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            using var texture2 = new Texture2D(device, desc2);

            using var texture2Surface = texture2.QueryInterface<Surface>();
            using Bitmap1 bitmap1 = new Bitmap1(texture2dc, texture2Surface);

            bitmap1.CopyFromBitmap(textureBitmap);
            // Get the desktop capture texture
            var mapSource = bitmap1.Map(MapOptions.Read);

            // Allocate an RGB buffer
            int width = desc2.Width;
            int height = desc2.Height;
            int stride = width * 3; // 3 bytes per pixel in RGB format
            byte[] rgbBuffer = new byte[height * stride];

            // Copy the data from the texture to the RGB buffer
            unsafe
            {
                byte* sourcePtr = (byte*)mapSource.DataPointer;
                Parallel.For(0, height, y =>
                {
                    int yStride = y * stride;
                    int yPitch = y * mapSource.Pitch;
                    for (int x = 0; x < width; x++)
                    {
                        int x4 = x * 4;
                        int x3 = x * 3;
                        rgbBuffer[yStride + x3] = sourcePtr[yPitch + x4]; // Red
                        rgbBuffer[yStride + x3 + 1] = sourcePtr[yPitch + x4 + 1]; // Green
                        rgbBuffer[yStride + x3 + 2] = sourcePtr[yPitch + x4 + 2]; // Blue
                    }
                });
            }

            try
            {
                bitmap1.Unmap();
            }

            catch { }

            return rgbBuffer;
        }




        public Task PauseVideo()
        {
            _isPaused = true;
            return Task.CompletedTask;
        }

        public Task ResumeVideo()
        {
            _isPaused = false;
            return Task.CompletedTask;
        }

        public Task StartVideo()
        {
            if (!_isStarted)
            {
                _isStarted = true;
                Init();
            }
            return Task.CompletedTask;
        }

        public Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                _ffmpegEncoder?.Dispose();
                Close();
            }

            return Task.CompletedTask;
        }

        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            if (!_isClosed)
            {
                if (OnVideoSourceEncodedSample != null)
                {

                    uint fps = (durationMilliseconds > 0) ? 1000 / durationMilliseconds : Helper.DEFAULT_VIDEO_FRAME_RATE;
                    if (fps == 0)
                    {
                        fps = 1;
                    }
                    unsafe
                    {
                        int stride = (pixelFormat == VideoPixelFormatsEnum.Bgra) ? 4 * width : 3 * width;
                        var i420Buffer = PixelConverter.ToI420(width, height, stride, sample, pixelFormat);
                        fixed (byte* i420BufferPtr = i420Buffer)
                        {
                            byte[]? encodedBuffer = _ffmpegEncoder.Encode(GetAVCodecID(_videoFormatManager.SelectedFormat.Codec), i420BufferPtr, width, height, (int)fps, _forceKeyFrame);

                            if (encodedBuffer != null)
                            {
                                //Console.WriteLine($"encoded buffer: {encodedBuffer.HexStr()}");
                                uint durationRtpTS = 90000 / fps;

                                // Note the event handler can be removed while the encoding is in progress.
                                OnVideoSourceEncodedSample?.Invoke(durationRtpTS, encodedBuffer);
                            }

                        }
                    }

                    if (_forceKeyFrame)
                    {
                        _forceKeyFrame = false;
                    }

                }


            }
        }

        public static AVCodecID GetAVCodecID(VideoCodecsEnum videoCodec)
        {
            AVCodecID result = AVCodecID.AV_CODEC_ID_H264;
            switch (videoCodec)
            {
                case VideoCodecsEnum.VP8:
                    result = AVCodecID.AV_CODEC_ID_VP8;
                    break;
                case VideoCodecsEnum.H264:
                    result = AVCodecID.AV_CODEC_ID_H264;
                    break;
            }

            return result;
        }

        public void Dispose()
        {

            CloseVideo();
        }

        public Task PauseVideoSink()
        {
            return Task.CompletedTask;
        }

        public Task ResumeVideoSink()
        {
            return Task.CompletedTask;
        }

        public Task StartVideoSink()
        {
            return Task.CompletedTask;
        }

        public Task CloseVideoSink()
        {
            return Task.CompletedTask;
        }

        public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
        {
            throw new NotImplementedException();
        }
    }
}
