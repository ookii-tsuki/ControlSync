using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SIPSorceryMedia.FFmpeg
{

    // I had to rewrite class because the `ExternalVideoSourceRawSample` method
    // was commented out for some reason but i didn't get any problems with it so far
    public class FFmpegVideoEndPoint1 : IVideoSink, IVideoSource, IDisposable
    {
        public ILogger logger = SIPSorcery.LogFactory.CreateLogger<FFmpegVideoEndPoint1>();

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

#pragma warning disable CS0067
        //public event EncodedSampleDelegate? OnVideoSourceEncodedSample;
        //public event RawExtVideoSampleDelegate? OnVideoSourceRawExtSample;
        //public event RawVideoSampleDelegate? OnVideoSourceRawSample;
        //public event SourceErrorDelegate? OnVideoSourceError;
#pragma warning restore CS0067

        public FFmpegVideoEndPoint1(Dictionary<string, string>? encoderOptions = null)
        {
            _videoFormatManager = new MediaFormatManager<VideoFormat>(_supportedFormats);

            _ffmpegEncoder = new FFmpegVideoEncoder1(encoderOptions, GetAvailableHW());
            _ffmpegEncoder.SetThreadCount(1);
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

        public MediaEndPoints ToMediaEndPoints()
        {
            return new MediaEndPoints
            {
                //VideoSource = this,
                VideoSink = this
            };
        }

        public List<VideoFormat> GetVideoSinkFormats() => _videoFormatManager.GetSourceFormats();
        public void SetVideoSinkFormat(VideoFormat videoFormat) => _videoFormatManager.SetSelectedFormat(videoFormat);
        public void RestrictFormats(Func<VideoFormat, bool> filter) => _videoFormatManager.RestrictFormats(filter);
        public List<VideoFormat> GetVideoSourceFormats() => _videoFormatManager.GetSourceFormats();
        public void SetVideoSourceFormat(VideoFormat videoFormat) => _videoFormatManager.SetSelectedFormat(videoFormat);
        public void ForceKeyFrame() => _forceKeyFrame = true;
        public bool HasEncodedVideoSubscribers() => OnVideoSinkDecodedSampleFaster != null;
        public bool IsVideoSourcePaused() => _isPaused;
        public void GotVideoRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload) =>
            throw new ApplicationException("The FFmpeg Video End Point requires full video frames rather than individual RTP packets.");

        public void GotVideoFrame(IPEndPoint remoteEndPoint, uint timestamp, byte[] payload, VideoFormat format)
        {
            if ((!_isClosed) && (payload != null) && (OnVideoSinkDecodedSampleFaster != null))
            {
                AVCodecID? codecID = FFmpegConvert.GetAVCodecID(_videoFormatManager.SelectedFormat.Codec);
                if (codecID != null)
                {
                    var imageRawSamples = _ffmpegEncoder.DecodeFaster(codecID.Value, payload, out var width, out var height);

                    if (imageRawSamples == null || width == 0 || height == 0)
                    {
                        logger.LogWarning($"Decode of video sample failed, width {width}, height {height}.");
                    }
                    else
                    {
                        foreach (var imageRawSample in imageRawSamples)
                        {
                            OnVideoSinkDecodedSampleFaster?.Invoke(imageRawSample);
                        }
                    }
                }
            }
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
            }

            return Task.CompletedTask;
        }

        public Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                _ffmpegEncoder?.Dispose();
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
            _ffmpegEncoder?.Dispose();
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
