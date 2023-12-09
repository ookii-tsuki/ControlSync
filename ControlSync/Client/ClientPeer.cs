using Newtonsoft.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using SIPSorceryMedia.OpusCodec;
using SIPSorceryMedia.SDL2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ControlSync.Client
{
    public static class ClientPeer
    {
        private const string STUN_URL1 = "stun:stun.l.google.com:19302";
        private const string STUN_URL2 = "stun:stun1.l.google.com:19302";

        public static RTCPeerConnectionState ConnectionState => peerConnection != null ? peerConnection.connectionState : RTCPeerConnectionState.disconnected;

        private static RTCPeerConnection peerConnection;
        private static FFmpegVideoEndPoint VideoEncoder { get; set; }
        private static SDL2AudioEndPoint1 AudioEncoder { get; set; }

        private static readonly string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "FFMPEG");
        public static async void StartPeerConnection(string base64Offer)
        {

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_DEBUG, ffmpegPath);

            VideoEncoder = new FFmpegVideoEndPoint();

            SDL2Helper.InitSDL();
            var playbackDevice = SDL2Helper.GetAudioPlaybackDevices()[0];
            AudioEncoder = new SDL2AudioEndPoint1(playbackDevice, new OpusAudioEncoder());

            peerConnection = CreatePeerConnection();

            HandleOffer(base64Offer);

            var answerSdp = peerConnection.createAnswer(null);
            await peerConnection.setLocalDescription(answerSdp);

            var answerSerialised = JsonConvert.SerializeObject(answerSdp,
                 new Newtonsoft.Json.Converters.StringEnumConverter());
            var answerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(answerSerialised));

            ClientSend.PeerAnswer(answerBase64);

            Manager.ShowScreen();
        }

        public static void HandleOffer(string base64Offer)
        {
            string remoteOffer = Encoding.UTF8.GetString(Convert.FromBase64String(base64Offer));

            RTCSessionDescriptionInit offerInit = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(remoteOffer);

            peerConnection.setRemoteDescription(offerInit);

            ClientPg.Log("Received offer");
        }

        public static void AddICECandidate(string base64ICECandidate)
        {
            if (peerConnection == null)
                return;

            string remoteICECandidate = Encoding.UTF8.GetString(Convert.FromBase64String(base64ICECandidate));

            RTCIceCandidateInit iceCandidateInit = JsonConvert.DeserializeObject<RTCIceCandidateInit>(remoteICECandidate);

            peerConnection.addIceCandidate(iceCandidateInit);
        }

        public static void CloseConnection()
        {
            if (peerConnection == null) return;

            peerConnection.close();
            peerConnection = null;
            VideoEncoder.Dispose();
            VideoEncoder = null;
            AudioEncoder.CloseAudioSink();
            AudioEncoder = null;
        }
        private static RTCPeerConnection CreatePeerConnection()
        {
            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL1 }, new RTCIceServer { urls = STUN_URL2 } }
            };
            // Create a new peer connection.
            var peerConnection = new RTCPeerConnection(config);

            VideoEncoder.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);
            AudioEncoder.RestrictFormats(format => format.Codec == AudioCodecsEnum.OPUS);

            var videoTrack = new MediaStreamTrack(VideoEncoder.GetVideoSourceFormats(), MediaStreamStatusEnum.RecvOnly);
            var audioTrack = new MediaStreamTrack(AudioEncoder.GetAudioSinkFormats(), MediaStreamStatusEnum.RecvOnly);

            peerConnection.addTrack(videoTrack);
            peerConnection.addTrack(audioTrack);

            peerConnection.OnVideoFormatsNegotiated += (sdpFormat) => VideoEncoder.SetVideoSourceFormat(sdpFormat.First());
            peerConnection.OnAudioFormatsNegotiated += (sdpFormat) => AudioEncoder.SetAudioSinkFormat(sdpFormat.First());

            peerConnection.OnVideoFrameReceived += VideoEncoder.GotVideoFrame;


            VideoEncoder.OnVideoSinkDecodedSampleFaster += (RawImage img) =>
            {
                Manager.UpdateScreenView(img.GetBuffer(), img.Width, img.Height, img.Stride);
            };

            peerConnection.OnRtpPacketReceived += (IPEndPoint rep, SDPMediaTypesEnum media, RTPPacket rtpPkt) =>
            {
                //logger.LogDebug($"RTP {media} pkt received, SSRC {rtpPkt.Header.SyncSource}.");
                if (media == SDPMediaTypesEnum.audio)
                {
                    AudioEncoder.GotAudioRtp(rep, rtpPkt.Header.SyncSource, rtpPkt.Header.SequenceNumber, rtpPkt.Header.Timestamp, rtpPkt.Header.PayloadType, rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                }
            };

            // Add a handler for ICE candidate events.
            // These candidates need to be sent to the remote peer.
            peerConnection.onicecandidate += (candidate) =>
            {
                var jCandidate = candidate.toJSON();
                var base64ICECandidate = Convert.ToBase64String(Encoding.UTF8.GetBytes(jCandidate));

                ClientSend.ICECandidate(base64ICECandidate, 1);

            };


            // Add a handler for connection state change events.
            // This can be used to monitor the status of the WebRTC session.
            peerConnection.onconnectionstatechange += (state) =>
            {
                ClientPg.Log($"Peer connection state changed to {state}.");
            };

            peerConnection.OnTimeout += (mediaType) => ClientPg.Log($"Timeout on media {mediaType}.");
            peerConnection.oniceconnectionstatechange += (state) => ClientPg.Log($"ICE connection state changed to {state}.");

            return peerConnection;
        }



    }
}
