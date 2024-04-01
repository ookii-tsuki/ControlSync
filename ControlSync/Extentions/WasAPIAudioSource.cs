using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIPSorceryMedia.External
{
    /// <summary>
    /// An audio source that captures audio from the default audio device.
    /// </summary>
    public class WasAPIAudioSource : IAudioSource
    {
        private const int FRAME_DURATION_MS = 20;
        private const int FRAME_SIZE = 192000 * FRAME_DURATION_MS / 1000;
        private const int MAX_AUDIO_BUFFER = 20000;
        static private ILogger log = SIPSorcery.LogFactory.CreateLogger<WasAPIAudioSource>();


        private IAudioEncoder _audioEncoder;
        private MediaFormatManager<AudioFormat> _audioFormatManager;

        private List<byte> audioStream;
        private Thread audioSendThread;
        private WasapiLoopbackCapture captureDevice;
        private WaveOutEvent silence;


        private bool _isStarted = false;
        private bool _isPaused = true;
        private bool _isClosed = true;


        #region EVENT

        public event EncodedSampleDelegate? OnAudioSourceEncodedSample = null;
        public event RawAudioSampleDelegate? OnAudioSourceRawSample = null;
        public event SourceErrorDelegate? OnAudioSourceError = null;

        #endregion EVENT

        public WasAPIAudioSource(IAudioEncoder audioEncoder)
        {
            if (audioEncoder == null)
                throw new ApplicationException("Audio encoder provided is null");


            _audioFormatManager = new MediaFormatManager<AudioFormat>(audioEncoder.SupportedFormats);
            _audioEncoder = audioEncoder;


        }

        private void Init()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var renderDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            captureDevice = new WasapiLoopbackCapture(renderDevice);

            var silenceProvider = new SilenceProvider(captureDevice.WaveFormat);

            audioStream = new List<byte>();


            captureDevice.DataAvailable += (sender, args) =>
            {
                if (args.BytesRecorded == 0)
                    return;

                byte[] pcm = ToPCM16(args.Buffer, args.BytesRecorded, captureDevice.WaveFormat);

                audioStream.AddRange(pcm);
            };

            silence = new WaveOutEvent();
            silence.Init(silenceProvider);


            audioSendThread = new Thread(SendAudio);
        }


        private void SendAudio()
        {
            while (!_isClosed)
            {
                try
                {
                    if (audioStream.Count >= FRAME_SIZE)
                    {
                        byte[] pcm = new byte[FRAME_SIZE];
                        audioStream.CopyTo(0, pcm, 0, FRAME_SIZE);
                        audioStream.RemoveRange(0, FRAME_SIZE);

                        short[] shortPcm = pcm.Take(pcm.Length * 2).Where((x, i) => i % 2 == 0).Select((y, i) => BitConverter.ToInt16(pcm, i * 2)).ToArray();

                        OnAudioSourceRawSample?.Invoke(AudioSamplingRatesEnum.Rate16KHz, (uint)shortPcm.Length, shortPcm);

                        if (OnAudioSourceEncodedSample != null)
                        {
                            var encodedSample = _audioEncoder.EncodeAudio(shortPcm, _audioFormatManager.SelectedFormat);
                            if (encodedSample.Length > 0)
                                OnAudioSourceEncodedSample?.Invoke((uint)(shortPcm.Length * _audioFormatManager.SelectedFormat.RtpClockRate / _audioFormatManager.SelectedFormat.ClockRate), encodedSample);
                        }

                    }
                    if (audioStream.Count >= MAX_AUDIO_BUFFER) // to prevent overflowing
                        audioStream.Clear();
                }
                catch { }
                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// Converts an IEEE Floating Point audio buffer into a 16bit PCM compatible buffer.
        /// </summary>
        /// <param name="inputBuffer">The buffer in IEEE Floating Point format.</param>
        /// <param name="length">The number of bytes in the buffer.</param>
        /// <param name="format">The WaveFormat of the buffer.</param>
        /// <returns>A byte array that represents the given buffer converted into PCM format.</returns>
        private static byte[] ToPCM16(byte[] inputBuffer, int length, WaveFormat format)
        {
            if (length == 0)
                return new byte[0]; // No bytes recorded, return empty array.

            // Create a WaveStream from the input buffer.
            using var memStream = new MemoryStream(inputBuffer, 0, length);
            using var inputStream = new RawSourceWaveStream(memStream, format);

            // Convert the input stream to a WaveProvider in 16bit PCM format with sample rate of 48000 Hz.
            var convertedPCM = new SampleToWaveProvider16(
                new WdlResamplingSampleProvider(
                    new WaveToSampleProvider(inputStream),
                    48000)
                );

            byte[] convertedBuffer = new byte[length];

            using var stream = new MemoryStream();
            int read;

            // Read the converted WaveProvider into a buffer and turn it into a Stream.
            while ((read = convertedPCM.Read(convertedBuffer, 0, length)) > 0)
                stream.Write(convertedBuffer, 0, read);

            // Return the converted Stream as a byte array.
            return stream.ToArray();
        }


        private void RaiseAudioSourceError(String err)
        {
            CloseAudio();
            OnAudioSourceError?.Invoke(err);
        }

        public Task PauseAudio()
        {
            return Task.CompletedTask;
        }

        public Task ResumeAudio()
        {
            return Task.CompletedTask;
        }

        public bool IsAudioSourcePaused()
        {
            return _isPaused;
        }

        public Task StartAudio()
        {
            _isStarted = true;
            _isClosed = false;

            silence.Play();
            captureDevice.StartRecording();
            audioSendThread.Start();

            return Task.CompletedTask;
        }

        public Task CloseAudio()
        {
            _isClosed = true;
            _isStarted = false;

            captureDevice?.Dispose();
            audioStream?.Clear();
            silence?.Dispose();

            return Task.CompletedTask;
        }

        public List<AudioFormat> GetAudioSourceFormats()
        {
            if (_audioFormatManager != null)
                return _audioFormatManager.GetSourceFormats();
            return new List<AudioFormat>();
        }

        public void SetAudioSourceFormat(AudioFormat audioFormat)
        {
            if (_audioFormatManager != null)
            {
                log.LogDebug($"Setting audio source format to {audioFormat.FormatID}:{audioFormat.FormatName} {audioFormat.ClockRate}.");
                _audioFormatManager.SetSelectedFormat(audioFormat);

                Init();

                StartAudio();
            }
        }

        public void RestrictFormats(Func<AudioFormat, bool> filter)
        {
            if (_audioFormatManager != null)
                _audioFormatManager.RestrictFormats(filter);
        }

        public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            if (_isClosed)
                return;

            OnAudioSourceRawSample?.Invoke(samplingRate, (uint)sample.Length, sample);

            if (OnAudioSourceEncodedSample != null)
            {
                var encodedSample = _audioEncoder.EncodeAudio(sample, _audioFormatManager.SelectedFormat);
                if (encodedSample.Length > 0)
                    OnAudioSourceEncodedSample?.Invoke((uint)(sample.Length * _audioFormatManager.SelectedFormat.RtpClockRate / _audioFormatManager.SelectedFormat.ClockRate), encodedSample);
            }
        }

        public bool HasEncodedAudioSubscribers() => OnAudioSourceEncodedSample != null;
    }
}
